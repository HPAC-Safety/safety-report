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
///     The reporter's one report write: a final JSON request naming the
///     attachments already uploaded to quarantine, persisted atomically, with no
///     report state created before it. See issue #14, ADR-0096, and
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
public static partial class ReportSubmissionEndpoints
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>Maps the report-submission endpoint.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapReportSubmission(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/v1/reports").RequireAuthorization(HpacPolicies.Member);

		group.MapPost("/", Submit).RequireRateLimiting(RateLimitPolicies.PublicSubmission);

		return group;
	}

	private static async Task<IResult> Submit(
		HttpRequest request,
		HpacSafetyDbContext database,
		MediaIngestor ingestor,
		IBlobStore blobStore,
		IOptions<MediaPolicyOptions> mediaOptions,
		TimeProvider clock,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(ingestor);
		ArgumentNullException.ThrowIfNull(blobStore);
		ArgumentNullException.ThrowIfNull(mediaOptions);
		ArgumentNullException.ThrowIfNull(clock);

		if (!request.HasJsonContentType())
		{
			return Problem("A submission must be application/json.");
		}

		var dto = await ReadReport(request, cancellationToken).ConfigureAwait(false);
		if (dto is null)
		{
			return Problem("The submission must be one valid report.");
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
		var claimedUploads = new HashSet<UploadId>();
		var fileAnswers = new List<(ReportAnswer Answer, IReadOnlyList<(UploadId Upload, string? FileName)> Uploads)>();

		foreach (var entry in dto.Answers)
		{
			var outcome = TryApplyAnswer(
				report, entry, revisionLookup, seenRevisionIds, claimedUploads, clock, fileAnswers);

			if (outcome is { } problem)
			{
				return problem;
			}
		}

		if (claimedUploads.Count > mediaOptions.Value.MaxAttachmentCount)
		{
			return Problem("Too many attachments.");
		}

		try
		{
			report.EnsureReadyForSubmission();
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		try
		{
			RecordReporterChoices(report, revisionLookup);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		// Every named upload must still be waiting in quarantine before a single
		// byte is promoted, so an expired one refuses the submission cleanly and
		// the form can say exactly which files to attach again (ADR-0096).
		var expired = await FindExpired(claimedUploads, blobStore, cancellationToken).ConfigureAwait(false);
		if (expired.Count > 0)
		{
			return Results.Problem(
				title: "That submission was not accepted.",
				detail: "Some attachments are no longer available and must be attached again.",
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/report-submission",
				extensions: new Dictionary<string, object?> { ["expiredUploadIds"] = expired });
		}

		var attachmentProblem = await IngestFiles(report, fileAnswers, ingestor, clock, cancellationToken)
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

		await ReleaseClaimedUploads(claimedUploads, blobStore, loggerFactory.CreateLogger(nameof(ReportSubmissionEndpoints)))
			.ConfigureAwait(false);

		return Results.Accepted(
			$"/api/v1/reports/{report.Id.Value}",
			new SubmitReportResponse(report.Id.Value, "submitted"));
	}

	/// <summary>
	///     Applies one answer entry to the report, or returns the problem response
	///     to send instead. A file-upload answer's <see cref="ReportAnswer" /> row is
	///     created here with a null value; its uploads are claimed in a second pass,
	///     once every entry has validated structurally.
	/// </summary>
	private static IResult? TryApplyAnswer(
		Report report,
		SubmitAnswerRequest entry,
		Dictionary<TinyId, (Question Question, QuestionRevision Revision)> revisionLookup,
		HashSet<TinyId> seenRevisionIds,
		HashSet<UploadId> claimedUploads,
		TimeProvider clock,
		List<(ReportAnswer Answer, IReadOnlyList<(UploadId Upload, string? FileName)> Uploads)> fileAnswers)
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
			if (entry.Value is not null
				|| entry.Attachments is { Count: > 0 })
			{
				return Problem("A multi-select answer carries option codes, not a value or upload ids.");
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
			if (entry.Value is not null
				|| entry.OptionCodes is { Count: > 0 })
			{
				return Problem("A file-upload answer carries upload ids, not a value or option codes.");
			}

			var uploads = new List<(UploadId Upload, string? FileName)>();
			foreach (var attachment in entry.Attachments ?? [])
			{
				if (attachment is null
					|| !UploadId.TryParse(attachment.UploadId, out var uploadId))
				{
					return Problem("An answer named a malformed upload id.");
				}

				if (!claimedUploads.Add(uploadId))
				{
					return Problem("An answer named the same upload more than once.");
				}

				uploads.Add((uploadId, attachment.FileName));
			}

			try
			{
				// The row is created now, with no value — files are linked to it
				// once every entry has validated and ingestion can safely begin.
				var answer = report.Answer(question, revision, value: null, at);
				fileAnswers.Add((answer, uploads));
			}
			catch (DomainRuleViolationException cause)
			{
				return Problem(cause.Message);
			}

			return null;
		}

		if (entry.OptionCodes is { Count: > 0 }
			|| entry.Attachments is { Count: > 0 })
		{
			return Problem("This answer's shape does not carry option codes or upload ids.");
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
	///     The named uploads that are no longer in quarantine, in the order they
	///     were named, so the form can mark exactly those files.
	/// </summary>
	private static async Task<List<string>> FindExpired(
		IEnumerable<UploadId> uploads,
		IBlobStore blobStore,
		CancellationToken cancellationToken)
	{
		var expired = new List<string>();

		foreach (var upload in uploads)
		{
			if (!await blobStore.Exists(BlobKey.ForUpload(upload), cancellationToken).ConfigureAwait(false))
			{
				expired.Add(upload.Value);
			}
		}

		return expired;
	}

	/// <summary>
	///     Claims every file-upload answer's uploads: each is judged again and
	///     promoted into the report's own compartments, then linked to its answer.
	///     Runs only once every answer entry has validated structurally and every
	///     upload has been found.
	/// </summary>
	private static async Task<IResult?> IngestFiles(
		Report report,
		IReadOnlyList<(ReportAnswer Answer, IReadOnlyList<(UploadId Upload, string? FileName)> Uploads)> fileAnswers,
		MediaIngestor ingestor,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		foreach (var (answer, uploads) in fileAnswers)
		{
			foreach (var (upload, fileName) in uploads)
			{
				var fileId = TinyId.New();
				var outcome = await ingestor.Ingest(BlobKey.ForUpload(upload), report.Id, fileId, cancellationToken)
					.ConfigureAwait(false);

				if (!outcome.IsAccepted)
				{
					return Problem($"An attachment was refused: {outcome.RejectionReason}.");
				}

				var file = report.AddFile(
					fileId,
					outcome.OriginalKey.Value,
					outcome.ContentType.ContentType,
					outcome.ByteSize,
					fileName,
					clock.GetUtcNow());
				file.LinkToAnswer(answer.Id);

				if (outcome.IsViewable)
				{
					file.RecordStripped(outcome.DerivativeKey.Value, outcome.StrippedAt!.Value);
				}
			}
		}

		return null;
	}

	/// <summary>
	///     Removes claimed uploads from quarantine once the report has committed.
	///     Best effort, and deliberately not cancellable by the request: the report
	///     already holds its own copies, and an upload this fails to remove is
	///     expired by the lifecycle rule instead (REQ-SUB-042).
	/// </summary>
	private static async Task ReleaseClaimedUploads(
		IEnumerable<UploadId> uploads,
		IBlobStore blobStore,
		ILogger logger)
	{
		foreach (var upload in uploads)
		{
			try
			{
				await blobStore.Delete(BlobKey.ForUpload(upload), CancellationToken.None).ConfigureAwait(false);
			}
#pragma warning disable CA1031 // The lifecycle rule is the backstop; a failed tidy-up must not fail a committed report.
			catch (Exception cause)
#pragma warning restore CA1031
			{
				// No upload id or key in the message: an id is a capability, and
				// a key is private. The exception type is enough to notice a trend.
				LogUploadNotReleased(logger, cause.GetType().Name);
			}
		}
	}

	/// <summary>
	///     Adds each type-ahead value the reporter typed to that question's own
	///     choices, in the language they answered in only, inside the same
	///     transaction as the report (ADR-0063, ADR-0095). A value the question
	///     already offers, in either language or under the same code, is the
	///     existing choice and changes nothing. No other type takes an addition.
	/// </summary>
	private static void RecordReporterChoices(
		Report report,
		Dictionary<TinyId, (Question Question, QuestionRevision Revision)> revisionLookup)
	{
		foreach (var answer in report.Answers)
		{
			var (question, revision) = revisionLookup[answer.QuestionRevisionId];

			if (revision.TakesReporterAdditions
				&& question.TakesReporterAdditions
				&& !string.IsNullOrWhiteSpace(answer.Value))
			{
				question.AddChoiceFromReporter(answer.Value, report.Language);
			}
		}
	}

	private static async Task<Dictionary<TinyId, (Question Question, QuestionRevision Revision)>> LoadRevisionsAsync(
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		var questions = await Admin.QuestionEndpoints.LiveQuestions(database)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return questions
			.SelectMany(question => question.Revisions.Select(revision => (Question: question, Revision: revision)))
			.ToDictionary(pair => pair.Revision.Id, pair => pair);
	}

	private static async Task<SubmitReportRequest?> ReadReport(
		HttpRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			return await JsonSerializer
				.DeserializeAsync<SubmitReportRequest>(request.Body, JsonOptions, cancellationToken)
				.ConfigureAwait(false);
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

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "A claimed upload could not be removed from quarantine ({ExceptionType}); the lifecycle rule will expire it.")]
	private static partial void LogUploadNotReleased(ILogger logger,
													 string exceptionType);
}
