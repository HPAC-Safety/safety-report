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
	public async Task Process(OutboxMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var reportId = TinyId.Parse(message.Payload);
		var report = await database.Reports
			.SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken)
			.ConfigureAwait(false);

		// The report is gone (deleted — excluded by the default query filter) or
		// already past this stage (a previous attempt finished after all, or some
		// other path moved it on). Either way there is nothing left to do.
		if (report is null || report.Status is not (ReportStatus.Submitted or ReportStatus.Summarizing))
		{
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
			if (message.Attempts + 1 >= OutboxMessage.PoisonThreshold
				&& !await IsDeleted(report.Id, cancellationToken).ConfigureAwait(false))
			{
				report.FailSummarization(exception.Message);
			}

			throw;
		}
	}

	/// <summary>Whether the report has been soft-deleted since it was loaded, read past the default live-row filter.</summary>
	private async Task<bool> IsDeleted(TinyId reportId, CancellationToken cancellationToken)
	{
		return await database.Reports.IgnoreQueryFilters()
			.Where(candidate => candidate.Id == reportId)
			.Select(candidate => candidate.Deleted != null)
			.SingleAsync(cancellationToken)
			.ConfigureAwait(false);
	}

	private async Task<ReportForSummaryDto> LoadForSummary(TinyId reportId, Locale language, CancellationToken cancellationToken)
	{
		var rows = await database.ReportAnswers
			.Where(answer => answer.ReportId == reportId
				&& answer.Value != null
				&& answer.QuestionKey != QuestionKey.ConsentPublish)
			.Join(
				database.QuestionRevisions,
				answer => answer.QuestionRevisionId,
				revision => revision.Id,
				(answer, revision) => new
				{
					answer.QuestionKey,
					answer.Value,
					answer.IsPrivate,
					revision.Type,
					revision.LabelEn,
					revision.LabelFr
				})
			.Where(row => row.Type != QuestionType.FileUpload)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var fields = rows
			.Select(row => new ClassifiedReportField(
				new SummarizationField(row.QuestionKey, language == Locale.FrCa ? row.LabelFr : row.LabelEn, row.Value!),
				row.IsPrivate))
			.ToList();

		return new ReportForSummaryDto(reportId, language, fields);
	}
}
