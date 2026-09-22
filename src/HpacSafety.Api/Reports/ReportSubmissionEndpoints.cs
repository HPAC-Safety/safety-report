using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Api.RateLimiting;
using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Reports;

/// <summary>
///     The only reporter-facing write: one final multipart request, persisted
///     atomically, with no report state created before it. See issue #14 and
///     <c>features/report-submission/report-submission.feature</c>.
/// </summary>
/// <remarks>
///     Every answer's <c>value</c> and <c>locale</c> are written here and never
///     again — this endpoint is their only writer, ever. Their second language is
///     never touched here; it stays null until a later, off-request-path Worker
///     job (issue #271) fills it, or an administrator edits it by hand. See
///     ADR-0080. Nothing here calls a translation provider or the summarization
///     model.
/// </remarks>
public static class ReportSubmissionEndpoints
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>Maps the report-submission endpoint.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapReportSubmission(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/v1/reports").RequireAuthorization(HpacPolicies.Member);

		group.MapPost("/", SubmitAsync).RequireRateLimiting(RateLimitPolicies.PublicSubmission);

		return group;
	}

	private static async Task<IResult> SubmitAsync(
		HttpRequest request,
		HpacSafetyDbContext database,
		MediaIngestor ingestor,
		IBlobStore blobStore,
		IOptions<MediaPolicyOptions> mediaOptions,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(ingestor);
		ArgumentNullException.ThrowIfNull(blobStore);
		ArgumentNullException.ThrowIfNull(mediaOptions);
		ArgumentNullException.ThrowIfNull(clock);

		if (!request.HasFormContentType)
		{
			return Problem("A submission must be multipart/form-data.");
		}

		IFormCollection form;
		try
		{
			form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (InvalidDataException)
		{
			return Problem("The multipart request could not be read.");
		}

		if (form.Files.Count > mediaOptions.Value.MaxAttachmentCount)
		{
			return Problem("Too many attachments.");
		}

		var dto = ParseReportPart(form);
		if (dto is null)
		{
			return Problem("The submission must contain exactly one valid report part.");
		}

		if (!Locale.TryParse(dto.Language, out var locale))
		{
			return Problem("The submission's language must be en-CA or fr-CA.");
		}

		if (dto.Answers is not { Count: > 0 })
		{
			return Problem("The submission must answer at least the consent question.");
		}

		var revisionLookup = await LoadRevisionsAsync(database, cancellationToken).ConfigureAwait(false);

		var report = new Report(locale, clock.GetUtcNow());
		var seenRevisionIds = new HashSet<TinyId>();
		var mappedFileIndexes = new HashSet<int>();
		var fileAnswers = new List<(ReportAnswer Answer, IReadOnlyList<int> Indexes)>();

		foreach (var entry in dto.Answers)
		{
			var outcome = TryApplyAnswer(
				report, entry, revisionLookup, seenRevisionIds, mappedFileIndexes, form.Files.Count, clock, fileAnswers);

			if (outcome is { } problem)
			{
				return problem;
			}
		}

		if (mappedFileIndexes.Count != form.Files.Count)
		{
			return Problem("Every uploaded file must be referenced by exactly one answer.");
		}

		try
		{
			report.EnsureReadyForSubmission();
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		var attachmentProblem = await IngestFilesAsync(
				report, fileAnswers, form.Files, blobStore, ingestor, clock, cancellationToken)
			.ConfigureAwait(false);

		if (attachmentProblem is not null)
		{
			return attachmentProblem;
		}

		var at = clock.GetUtcNow();
		database.Reports.Add(report);
		database.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, at));
		database.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.TranslateAnswers, report.Id.Value, at));

		foreach (var file in report.Files)
		{
			database.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.ProcessAttachment, file.Id.Value, at));
		}

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.Accepted(
			$"/api/v1/reports/{report.Id.Value}",
			new SubmitReportResponse(report.Id.Value, "submitted"));
	}

	/// <summary>
	///     Applies one answer entry to the report, or returns the problem response
	///     to send instead. A file-upload answer's <see cref="ReportAnswer" /> row is
	///     created here with a null value; the files themselves are ingested in a
	///     second pass, once every entry has validated structurally.
	/// </summary>
	private static IResult? TryApplyAnswer(
		Report report,
		SubmitAnswerRequest entry,
		Dictionary<TinyId, (Question Question, QuestionRevision Revision)> revisionLookup,
		HashSet<TinyId> seenRevisionIds,
		HashSet<int> mappedFileIndexes,
		int fileCount,
		TimeProvider clock,
		List<(ReportAnswer Answer, IReadOnlyList<int> Indexes)> fileAnswers)
	{
		if (!TinyId.TryParse(entry.QuestionRevisionId, out var revisionId))
		{
			return Problem("An answer named an invalid revision.");
		}

		if (!seenRevisionIds.Add(revisionId))
		{
			return Problem("An answer named the same revision more than once.");
		}

		if (!revisionLookup.TryGetValue(revisionId, out var found))
		{
			return Problem("An answer named an unknown or deleted revision.");
		}

		var (question, revision) = found;

		if (revision.CollectsNoAnswer)
		{
			return Problem("A statement or group question collects no answer.");
		}

		var at = clock.GetUtcNow();

		if (revision.Type == QuestionType.MultiSelect)
		{
			if (entry.Value is not null || entry.AttachmentPartIndexes is { Count: > 0 })
			{
				return Problem("A multi-select answer carries option codes, not a value or file indexes.");
			}

			try
			{
				report.Answer(question, revision, entry.OptionCodes ?? [], at);
			}
			catch (DomainRuleViolationException cause)
			{
				return Problem(cause.Message);
			}

			return null;
		}

		if (revision.Type == QuestionType.FileUpload)
		{
			if (entry.Value is not null || entry.OptionCodes is { Count: > 0 })
			{
				return Problem("A file-upload answer carries attachment indexes, not a value or option codes.");
			}

			var indexes = entry.AttachmentPartIndexes ?? [];
			foreach (var index in indexes)
			{
				if (index < 0 || index >= fileCount)
				{
					return Problem("An answer referenced a file part that was not uploaded.");
				}

				if (!mappedFileIndexes.Add(index))
				{
					return Problem("An answer referenced the same file part more than once.");
				}
			}

			try
			{
				// The row is created now, with no value — files are linked to it
				// once every entry has validated and ingestion can safely begin.
				var answer = report.Answer(question, revision, value: null, at);
				fileAnswers.Add((answer, indexes));
			}
			catch (DomainRuleViolationException cause)
			{
				return Problem(cause.Message);
			}

			return null;
		}

		if (entry.OptionCodes is { Count: > 0 } || entry.AttachmentPartIndexes is { Count: > 0 })
		{
			return Problem("This answer's shape does not carry option codes or file indexes.");
		}

		try
		{
			report.Answer(question, revision, entry.Value, at);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		return null;
	}

	/// <summary>
	///     Streams every file-upload answer's referenced parts into quarantine,
	///     ingests each one, and links the accepted result to its answer. Runs only
	///     once every answer entry has validated structurally, so a malformed
	///     submission never quarantines a single byte.
	/// </summary>
	private static async Task<IResult?> IngestFilesAsync(
		Report report,
		IReadOnlyList<(ReportAnswer Answer, IReadOnlyList<int> Indexes)> fileAnswers,
		IFormFileCollection files,
		IBlobStore blobStore,
		MediaIngestor ingestor,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		foreach (var (answer, indexes) in fileAnswers)
		{
			foreach (var index in indexes)
			{
				var upload = files[index];
				var quarantineKey = BlobKey.For(report.Id.Value, MediaCompartment.Quarantine, TinyId.New().Value);

				await using (var content = upload.OpenReadStream())
				{
					await blobStore.WriteAsync(quarantineKey, content, upload.ContentType, cancellationToken)
						.ConfigureAwait(false);
				}

				var outcome = await ingestor.IngestAsync(quarantineKey, upload.ContentType, cancellationToken)
					.ConfigureAwait(false);

				if (!outcome.IsAccepted)
				{
					return Problem($"An attachment was refused: {outcome.RejectionReason}.");
				}

				var file = report.AddFile(outcome.OriginalKey.Value, outcome.ContentType.ContentType, outcome.ByteSize, clock.GetUtcNow());
				file.LinkToAnswer(answer.Id);
			}
		}

		return null;
	}

	private static async Task<Dictionary<TinyId, (Question Question, QuestionRevision Revision)>> LoadRevisionsAsync(
		HpacSafetyDbContext database, CancellationToken cancellationToken)
	{
		var questions = await Admin.QuestionEndpoints.LiveQuestions(database)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return questions
			.SelectMany(question => question.Revisions.Select(revision => (Question: question, Revision: revision)))
			.ToDictionary(pair => pair.Revision.Id, pair => pair);
	}

	private static SubmitReportRequest? ParseReportPart(IFormCollection form)
	{
		var reportPart = form["report"];

		if (reportPart.Count != 1 || string.IsNullOrWhiteSpace(reportPart[0]))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<SubmitReportRequest>(reportPart[0]!, JsonOptions);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>
	///     A safe, generic 400. Never built from reporter-submitted content — every
	///     caller passes a fixed, English, developer-authored string naming which
	///     structural rule failed, never an answer, filename, or token.
	/// </summary>
	private static IResult Problem(string detail)
	{
		return Results.Problem(
			title: "That submission was not accepted.",
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: "https://hpac.ca/problems/report-submission");
	}
}
