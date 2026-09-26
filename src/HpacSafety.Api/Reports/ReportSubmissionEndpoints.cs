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
///     attachments already sent to quarantine, validating each, and persisting
///     the report atomically, with no report state created before it. See issue
///     #14, ADR-0096, ADR-0126, and
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
		IBlobStore blobStore,
		MediaIngestor ingestor,
		IOptions<MediaPolicyOptions> mediaOptions,
		TimeProvider clock,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(ingestor);
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(database);
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

		// A question whose choices depend on another's is checked against the
		// parent's answer, so every parent is answered first — its new type-ahead
		// value included, which a new child value is offered under (ADR-0145).
		foreach (var entry in dto.Answers.OrderBy(entry => IsDependent(entry, revisionLookup) ? 1 : 0))
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

		// Every named upload must still be waiting in quarantine, and must be what
		// it was declared as, before a single byte is copied. An expired or
		// refused one refuses the submission cleanly, and the form can say
		// exactly which files to attach again or why (ADR-0096, ADR-0126).
		var (stored, expired) = await DescribeUploads(claimedUploads, blobStore, cancellationToken).ConfigureAwait(false);
		var (accepted, refused) = await ValidateUploads(stored, blobStore, ingestor, cancellationToken).ConfigureAwait(false);
		if (expired.Count > 0 || refused.Count > 0)
		{
			return Results.Problem(
				title: "That submission was not accepted.",
				detail: expired.Count > 0
					? "Some attachments are no longer available and must be attached again."
					: "Some attachments were not accepted.",
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/report-submission",
				extensions: new Dictionary<string, object?>
				{
					["expiredUploadIds"] = expired,
					["refusedUploads"] = refused,
				});
		}

		await ClaimFiles(report, fileAnswers, accepted, blobStore, clock, cancellationToken).ConfigureAwait(false);

		var at = clock.GetUtcNow();
		database.Reports.Add(report);
		database.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, at));
		database.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.TranslateAnswers, report.Id.Value, at));

		foreach (var file in report.Files)
		{
			database.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.ProcessAttachment, file.Id.Value, at));
		}

		// A type-ahead value a reporter added holds only the language it was typed
		// in. The Worker supplies the other, on the value itself; nothing here calls
		// a translation provider (ADR-0129).
		foreach (var added in database.ChangeTracker.Entries<QuestionChoice>()
					 .Where(entry => entry.State == EntityState.Added && entry.Entity.NeedsTranslation)
					 .Select(entry => entry.Entity)
					 .ToList())
		{
			database.OutboxMessages.Add(new OutboxMessage(added.QuestionId, OutboxMessageType.TranslateChoice, added.Id.Value, at));
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

		if (revision.StoresLocalizedValue)
		{
			return TryApplyChoiceAnswer(report, entry, question, revision, at, revisionLookup);
		}

		if (revision.Type == QuestionType.FileUpload)
		{
			if (entry.Value is not null
				|| entry.Choices is { Count: > 0 })
			{
				return Problem("A file-upload answer carries upload ids, not a value or choices.");
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

		if (entry.Choices is { Count: > 0 }
			|| entry.Attachments is { Count: > 0 })
		{
			return Problem("This answer's shape does not carry choices or upload ids.");
		}

		// A yes/no or checkbox answer is a JSON boolean and everything else a JSON
		// string (ADR-0130). The domain refuses a boolean for any other type and
		// text for a yes/no, so neither is converted here.
		try
		{
			switch (entry.Value?.ValueKind)
			{
				case null or JsonValueKind.Null:
					report.Answer(question, revision, (string?)null, at);
					break;
				case JsonValueKind.True or JsonValueKind.False:
					report.Answer(question, revision, entry.Value.Value.GetBoolean(), at);
					break;
				case JsonValueKind.String:
					var text = entry.Value.Value.GetString();

					// Core holds a phone answer to E.164; whether the number is real
					// for its country is libphonenumber's call (ADR-0137). The refusal
					// names the question, never the number.
					if (revision.Type == QuestionType.Phone
						&& !string.IsNullOrWhiteSpace(text)
						&& !PhoneAnswer.IsValid(text))
					{
						return Problem($"'{question.Key}' must be answered with a phone number valid for its country, written in E.164.");
					}

					report.Answer(question, revision, text, at);
					break;
				default:
					return Problem("An answer's value must be a string or a boolean.");
			}
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		return null;
	}

	/// <summary>
	///     A single-select, multi-select, or type-ahead answer names the question's
	///     live choices by identifier (ADR-0128). Only a type-ahead may instead carry
	///     the text a reporter typed, which names or becomes one of its values
	///     (ADR-0129).
	/// </summary>
	private static IResult? TryApplyChoiceAnswer(
		Report report,
		SubmitAnswerRequest entry,
		Question question,
		QuestionRevision revision,
		DateTimeOffset at,
		Dictionary<TinyId, (Question Question, QuestionRevision Revision)> revisionLookup)
	{
		if (entry.Attachments is { Count: > 0 })
		{
			return Problem("A choice answer carries choices, not upload ids.");
		}

		if (entry.Value?.ValueKind is not (null or JsonValueKind.Null or JsonValueKind.String))
		{
			return Problem("A choice answer's typed text must be a string.");
		}

		var text = entry.Value?.ValueKind == JsonValueKind.String ? entry.Value.Value.GetString() : null;
		var typed = !string.IsNullOrWhiteSpace(text);

		if (typed && entry.Choices is { Count: > 0 })
		{
			return Problem("A choice answer names its choices or carries typed text, not both.");
		}

		if (typed && !revision.TakesReporterAdditions)
		{
			return Problem("A choice answer names the question's choices by identifier.");
		}

		var choiceIds = new List<TinyId>();
		foreach (var choice in entry.Choices ?? [])
		{
			if (!TinyId.TryParse(choice, out var choiceId))
			{
				return Problem("An answer named an invalid choice.");
			}

			choiceIds.Add(choiceId);
		}

		TinyId? parentChoiceId = null;

		if ((typed || choiceIds.Count > 0)
			&& FilteringParent(question, revisionLookup) is { } parent)
		{
			// The form offers a child's choices only under the parent's answer, and
			// only once the parent is answered; the API holds a submission to the
			// same, whatever the form did (ADR-0145). The refusal names the
			// questions by key, never an answer.
			parentChoiceId = report.Answers.FirstOrDefault(answer => answer.QuestionId == parent.Id)?.ChoiceId;

			if (parentChoiceId is null)
			{
				return Problem($"'{question.Key}' can be answered only once '{parent.Key}' is.");
			}

			if (choiceIds.Exists(choiceId => question.OfferedChoice(choiceId) is { } choice && choice.ParentChoiceId != parentChoiceId))
			{
				return Problem($"'{question.Key}' named a choice that is not offered for the answer to '{parent.Key}'.");
			}
		}

		try
		{
			if (typed)
			{
				report.Answer(question, revision, text, at, parentChoiceId);
			}
			else
			{
				report.AnswerChoices(question, revision, choiceIds, at);
			}
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		return null;
	}

	/// <summary>
	///     The question whose answer filters <paramref name="question" />'s choices,
	///     when it is on the form. A parent the form does not ask filters nothing,
	///     so it is not checked (ADR-0145).
	/// </summary>
	private static Question? FilteringParent(Question question,
											 Dictionary<TinyId, (Question Question, QuestionRevision Revision)> revisionLookup)
	{
		return question.ChoicesDependOnQuestionId is { } parentId
			? revisionLookup.Values.Select(pair => pair.Question).FirstOrDefault(candidate => candidate.Id == parentId && candidate.IsActive)
			: null;
	}

	private static bool IsDependent(SubmitAnswerRequest entry,
									Dictionary<TinyId, (Question Question, QuestionRevision Revision)> revisionLookup)
	{
		return TinyId.TryParse(entry.QuestionRevisionId, out var revisionId)
			   && revisionLookup.TryGetValue(revisionId, out var found)
			   && found.Question.ChoicesDependOnQuestionId is not null;
	}

	/// <summary>
	///     What storage holds for each named upload, and the ids of those it no
	///     longer holds, in the order they were named, so the form can mark exactly
	///     those files.
	/// </summary>
	private static async Task<(Dictionary<UploadId, StoredBlob> Stored, List<string> Expired)> DescribeUploads(
		IEnumerable<UploadId> uploads,
		IBlobStore blobStore,
		CancellationToken cancellationToken)
	{
		var stored = new Dictionary<UploadId, StoredBlob>();
		var expired = new List<string>();

		foreach (var upload in uploads)
		{
			if (await blobStore.Describe(BlobKey.ForUpload(upload), cancellationToken).ConfigureAwait(false) is { } blob)
			{
				stored[upload] = blob;
			}
			else
			{
				expired.Add(upload.Value);
			}
		}

		return (stored, expired);
	}

	/// <summary>
	///     Sniffs and validates every stored upload a submission claims (ADR-0126).
	///     Each is read through a <see cref="BlobRangeStream" />, so only its stored
	///     size and the bytes sniffing asks for are fetched — never the whole file,
	///     never into memory. The size is held to the limit of the kind the bytes
	///     really are.
	/// </summary>
	/// <returns>
	///     The type each accepted upload was validated as, and each refused upload's
	///     id with its safe reason, in the order they were named.
	/// </returns>
	private static async Task<(Dictionary<UploadId, (MediaType Type, long ByteSize)> Accepted, List<RefusedUpload> Refused)> ValidateUploads(
		Dictionary<UploadId, StoredBlob> stored,
		IBlobStore blobStore,
		MediaIngestor ingestor,
		CancellationToken cancellationToken)
	{
		var accepted = new Dictionary<UploadId, (MediaType Type, long ByteSize)>();
		var refused = new List<RefusedUpload>();

		foreach (var (upload, blob) in stored)
		{
			await using var content = new BlobRangeStream(blobStore, BlobKey.ForUpload(upload), blob.ByteSize);

			// The stored type is the one the upload URL was signed for: what the
			// browser declared, and nothing else could have been sent.
			var verdict = await ingestor.Inspect(content, blob.ContentType, cancellationToken).ConfigureAwait(false);

			if (verdict.IsAccepted)
			{
				accepted[upload] = (verdict.Type, blob.ByteSize);
			}
			else
			{
				refused.Add(new RefusedUpload(upload.Value, EnumCode.Of(verdict.RejectionReason)));
			}
		}

		return (accepted, refused);
	}

	/// <summary>
	///     Claims every file-upload answer's uploads: each is copied, unchanged and
	///     inside storage, to the report's original compartment under the report
	///     file's own id, and recorded with the type its bytes were validated as and
	///     its stored size. Nothing is decoded here — the Worker processes each
	///     file from its outbox message (ADR-0098).
	/// </summary>
	private static async Task ClaimFiles(
		Report report,
		IReadOnlyList<(ReportAnswer Answer, IReadOnlyList<(UploadId Upload, string? FileName)> Uploads)> fileAnswers,
		Dictionary<UploadId, (MediaType Type, long ByteSize)> accepted,
		IBlobStore blobStore,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		foreach (var (answer, uploads) in fileAnswers)
		{
			foreach (var (upload, fileName) in uploads)
			{
				var fileId = TinyId.New();
				var originalKey = BlobKey.For(report.Id.Value, MediaCompartment.Original, fileId.Value);
				var (type, byteSize) = accepted[upload];

				await blobStore.Copy(BlobKey.ForUpload(upload), originalKey, cancellationToken).ConfigureAwait(false);

				var file = report.AddFile(fileId, originalKey.Value, type.ContentType, byteSize, fileName, clock.GetUtcNow());
				file.LinkToAnswer(answer.Id);
			}
		}
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
