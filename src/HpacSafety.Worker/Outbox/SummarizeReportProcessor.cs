using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     Builds a report's <see cref="ReportForSummaryDto" />, applies the
///     deterministic marking pass, and makes the Worker's one model call. See
///     <c>anonymize-hpac-reports</c> and ADR-0082.
/// </summary>
/// <remarks>
///     <see cref="OutboxClaimer" /> owns the transaction, and marks the message
///     processed or failed around this call — this method only mutates tracked
///     entities and lets an exception propagate. The one thing it does beyond
///     that generic contract: on a failure that is about to exhaust the outbox's
///     retry budget, it also moves the report itself to <c>SummaryFailed</c> so a
///     human always finds it, rather than leaving that solely to the poisoned
///     outbox row.
/// </remarks>
public sealed class SummarizeReportProcessor(HpacSafetyDbContext database, ISummarizer summarizer, TimeProvider clock)
	: IOutboxMessageProcessor
{
	/// <inheritdoc />
	public OutboxMessageType HandlesType => OutboxMessageType.SummarizeReport;

	/// <inheritdoc />
	public async Task Process(OutboxMessage message,
							  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var reportId = TinyId.Parse(message.Payload);
		var report = await database.Reports
			.SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken)
			.ConfigureAwait(false);

		// The report is gone (deleted — excluded by the default query filter) or
		// already past this stage (a previous attempt finished after all, or some
		// other path moved it on). Either way there is nothing left to do.
		if (report is null
			|| report.Status is not (ReportStatus.Submitted or ReportStatus.Summarizing))
		{
			return;
		}

		// Only a consented report ever reaches the model. One without consent is
		// Unpublished for good, with no summary and no model call, so its content
		// never leaves this system (REQ-DOM-006, REQ-DOM-015, REQ-AI-027).
		if (report.ConsentPublish is not true)
		{
			report.KeepUnpublished();
			return;
		}

		report.BeginSummarizing();

		var dto = await LoadForSummary(report.Id, report.Language, cancellationToken).ConfigureAwait(false);
		var input = SummarizationInput.Partition(dto.Fields);

		try
		{
			var draft = await summarizer.Summarize(input, cancellationToken).ConfigureAwait(false);

			// The model call takes real time, during which a safety officer may
			// have soft-deleted this report. Reread rather than trust the entity
			// loaded before the call, so a deletion mid-flight is never overwritten
			// by output that arrives after it (REQ-DOM-007).
			if (await IsDeleted(report.Id, cancellationToken).ConfigureAwait(false))
			{
				Abandon(report);
				return;
			}

			var summary = Summary.Generate(report.Id, draft.TextEn, draft.TextFr, draft.Model, draft.PromptVersion, clock.GetUtcNow());
			report.AttachSummary(summary);
			report.AwaitReview();
			database.Summaries.Add(summary);
		}
		catch (SummarizationFailedException exception)
		{
			// OutboxClaimer records this same failure on the message after this
			// method returns/throws — Attempts is still the pre-failure count here,
			// so +1 is what it will become. Once that reaches the poison threshold
			// the message stops being retried, so the report must not be left
			// silently stuck in Summarizing — unless it was deleted while this
			// attempt was in flight, in which case there is nothing left to update.
			if (await IsDeleted(report.Id, cancellationToken).ConfigureAwait(false))
			{
				Abandon(report);
			}
			else if (message.Attempts + 1 >= OutboxMessage.PoisonThreshold)
			{
				report.FailSummarization(exception.Message);
			}

			throw;
		}
	}

	/// <summary>
	///     Drops this attempt's own change to a report deleted while it ran. The
	///     report row carries a row version (ADR-0105), so writing Summarizing back
	///     over the deletion would fail the whole save; and a deleted report should
	///     not be touched anyway (REQ-DOM-007).
	/// </summary>
	private void Abandon(Report report)
	{
		database.Entry(report).State = EntityState.Unchanged;
	}

	/// <summary>Whether the report has been soft-deleted since it was loaded, read past the default live-row filter.</summary>
	private async Task<bool> IsDeleted(TinyId reportId,
									   CancellationToken cancellationToken)
	{
		return await database.Reports.IgnoreQueryFilters()
			.Where(candidate => candidate.Id == reportId)
			.Select(candidate => candidate.Deleted != null)
			.SingleAsync(cancellationToken)
			.ConfigureAwait(false);
	}

	private async Task<ReportForSummaryDto> LoadForSummary(TinyId reportId,
														   Locale language,
														   CancellationToken cancellationToken)
	{
		// A consent answer is never an occurrence fact. It is left out by its
		// question's role, not its key: a question seeded from the Typeform form
		// keeps the key the import gave it, and a key is an Administrator's to
		// choose. Privacy is the second guard (ADR-0082), never the first. A
		// choice answer's words are its choice's (ADR-0128), in the report's
		// language — which a private choice's marking then matches too.
		var rows = await database.ReportAnswers
			.Where(answer => answer.ReportId == reportId
							 && (answer.Value != null || answer.BooleanValue != null || answer.ChoiceId != null)
							 && !database.Questions.IgnoreQueryFilters()
								 .Any(question => question.Id == answer.QuestionId && question.Role != QuestionRole.None))
			.Join(
				database.QuestionRevisions,
				answer => answer.QuestionRevisionId,
				revision => revision.Id,
				(answer,
				 revision) => new
				 {
					 answer.QuestionKey,
					 answer.Value,
					 answer.BooleanValue,
					 // A merged value reads as the one it was merged into (ADR-0129).
					 ChoiceEn = answer.Choice!.MergedInto != null ? answer.Choice.MergedInto.LabelEn : answer.Choice.LabelEn,
					 ChoiceFr = answer.Choice.MergedInto != null ? answer.Choice.MergedInto.LabelFr : answer.Choice.LabelFr,
					 HasChoice = answer.ChoiceId != null,
					 answer.IsPrivate,
					 revision.Type,
					 revision.LabelEn,
					 revision.LabelFr,
				 })
			.Where(row => row.Type != QuestionType.FileUpload)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		// A yes/no reaches the model as `true` or `false`, never as words in either
		// language (ADR-0130).
		var fields = rows
			.Select(row => new ClassifiedReportField(
				new SummarizationField(
					row.QuestionKey,
					language == Locale.FrCa ? row.LabelFr : row.LabelEn,
					row.BooleanValue is { } boolean
						? (boolean ? "true" : "false")
						: row.HasChoice
							? (language == Locale.FrCa ? row.ChoiceFr ?? row.ChoiceEn : row.ChoiceEn ?? row.ChoiceFr)!
							: row.Value!,
					row.BooleanValue is not null),
				row.IsPrivate))
			.ToList();

		return new ReportForSummaryDto(reportId, language, fields);
	}
}
