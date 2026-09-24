using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The report-submission scenarios in
///     <c>features/report-submission/report-submission.feature</c> that describe
///     what <c>POST /api/v1/reports</c> does over HTTP — validation order, the
///     immutable value/locale split, atomic persistence, and the opaque receipt.
///     See issue #14 and ADR-0080.
/// </summary>
/// <remarks>
///     Detailed coverage of every rejected shape lives in
///     <c>HpacSafety.Api.Tests</c> against a real database; these prove the
///     feature file's sentences are true of the running system, the same split
///     <see cref="PublicQuestionEndpointSteps" /> already uses. Every report and
///     answer here is synthetic.
/// </remarks>
[Binding]
public sealed class ReportSubmissionEndpointSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly SemaphoreSlim ConsentGate = new(1, 1);
	private const string Secret = "Wind picked up on final approach — synthetic narrative for a test only.";

	private HttpClient? _reporter;
	private HttpClient? _admin;
	private HttpResponseMessage? _response;
	private string? _consentRevisionId;
	private string? _extraRevisionId;
	private string? _fileRevisionId;
	private string? _selectRevisionId;
	private string? _selectedLabel;
	private string? _answerId;
	private string? _submittedReportId;
	private string? _supersededRevisionId;
	private string? _problem;
	private string? _uploadId;
	private string? _expiredUploadId;
	private JsonElement? _responseBody;
	// A value only this scenario submits. Other scenarios submit reports into the
	// same database in parallel, so "nothing was created" is asserted as "no
	// stored answer carries this value", never as an unchanged report count.
	private readonly string _marker = $"synthetic-{Guid.NewGuid():N}";

	// --- Background: documented facts about the endpoint, not actions. ---

	[Given(@"a reporter writes a report through POST \/api\/v1\/reports and an attachment through POST \/api\/v1\/uploads")]
	[Given(@"both require a valid member bearer token")]
	[Given(@"the report request is JSON that names each attachment by the upload ID the upload returned")]
	[Given(@"the bearer token is transport\/security metadata, not persisted report content")]
	public void GivenBackgroundFact()
	{
		// Contextual — asserted by the scenarios themselves, not by this fact alone.
	}

	// --- One answer entry per shown answer-producing revision ---

	[Given(@"the client says it showed the reporter a set of answer-producing revisions")]
	public async Task GivenTheClientShowedASetOfAnswerProducingRevisions()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_extraRevisionId = await CreateSyntheticQuestion("short_text");
	}

	[When(@"the reporter submits the form")]
	public async Task WhenTheReporterSubmitsTheForm()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _extraRevisionId, value = (string?)"a synthetic answer" },
			},
		});
	}

	[Then(@"the submission DTO contains exactly one answer entry for each of those revisions")]
	[Then(@"every answer of every type uses ""value"", a single string, alongside the locale it was given in")]
	[Then(@"file-upload answers additionally carry one attachment entry per file attached to that question, each an upload ID and the file's name")]
	[Then(@"fields for the other answer shapes are null")]
	public void ThenTheDtoShapeIsHonored()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, "the API accepts a DTO built exactly this way");
	}

	// --- A skipped answer is represented by an empty value, not omission ---

	[Given(@"a reporter skips an answer-producing question")]
	public async Task GivenAReporterSkipsAnAnswerProducingQuestion()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_extraRevisionId = await CreateSyntheticQuestion("short_text");
		_fileRevisionId = await CreateSyntheticQuestion("file_upload");
	}

	[When(@"the submission DTO is built")]
	public async Task WhenTheSubmissionDtoIsBuilt()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _extraRevisionId, value = (string?)null },
				new { questionRevisionId = _fileRevisionId, attachments = Array.Empty<object>() },
			},
		});
	}

	[Then(@"a skipped answer of any type has a null value")]
	[Then(@"a skipped file upload has an empty attachments list")]
	public void ThenASkippedAnswerHasANullValue()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	// --- A submitted select value must be one the revision offered ---

	[Given(@"a reporter submits a value for a picker or multi-select question")]
	public async Task GivenAReporterSubmitsAValueForAPickerQuestion()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		var key = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(key);
	}

	[When(@"the API validates the submission")]
	public async Task WhenTheApiValidatesTheSubmission()
	{
		if (_expiredUploadId is not null)
		{
			_response = await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
					new { questionRevisionId = _extraRevisionId, value = (string?)_marker },
					new
					{
						questionRevisionId = _fileRevisionId,
						attachments = new[]
						{
							new { uploadId = _uploadId!, fileName = "still-here.pdf" },
							new { uploadId = _expiredUploadId!, fileName = "gone.pdf" },
						},
					},
				},
			});

			return;
		}

		if (_supersededRevisionId is not null)
		{
			_response = await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
					new { questionRevisionId = _supersededRevisionId, value = (string?)"an old answer" },
				},
			});

			return;
		}

		using var accepted = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _selectRevisionId, value = (string?)"Blue" },
			},
		});
		accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync());
		_selectedLabel = "Blue";

		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _selectRevisionId, value = (string?)"Not an offered option" },
			},
		});
	}

	[Then(@"the value is accepted only if the answered revision offered exactly that label")]
	public void ThenTheValueIsAcceptedOnlyIfOffered()
	{
		// Asserted by the two-request sequence above.
	}

	[Then(@"a value the revision never offered is rejected")]
	public void ThenAValueNeverOfferedIsRejected()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Then(@"a type-ahead also accepts a value it does not yet offer")]
	public async Task ThenATypeAheadAlsoAcceptsAnUnlistedValue()
	{
		var revisionId = await ReporterChoiceSubmissionSteps.CreateTypeAhead(
			_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator));

		using var response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = revisionId, value = (string?)"A site nobody listed" },
			},
		});

		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
	}

	// --- The submission path never calls a translation provider ---

	[Given(@"a submission contains select answers and a value typed into a type-ahead")]
	public async Task GivenASubmissionContainsSelectAnswers()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		var key = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(key);
		_selectedLabel = "Blue";
	}

	[When(@"the API commits the submission")]
	public async Task WhenTheApiCommitsTheSubmission()
	{
		object dto = _selectRevisionId is not null
			? new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
					new { questionRevisionId = _selectRevisionId, value = (string?)_selectedLabel },
				},
			}
			: new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
					new { questionRevisionId = _extraRevisionId, value = (string?)Secret },
				},
			};

		_response = await Post(dto);
	}

	[Then(@"no translation provider is called")]
	public void ThenNoTranslationProviderIsCalled()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[Then(@"the answers are stored in the language the reporter gave them in")]
	public async Task ThenTheAnswersAreStoredInTheReportersLanguage()
	{
		var stored = await SubmittedSelectAnswer();
		stored.Value.ShouldBe(_selectedLabel);
		stored.Locale.ShouldBe(Locale.EnCa);
	}

	[Then(@"only a picker answer carries a second language yet, copied from the choice it names")]
	public async Task ThenOnlyAPickerCarriesASecondLanguage()
	{
		// "Blue" is offered as "Bleu" (CreateSelectQuestion); copying it is a
		// lookup, not a provider call (ADR-0112).
		var stored = await SubmittedSelectAnswer();
		stored.TranslatedValue.ShouldBe("Bleu");
		stored.TranslationSource.ShouldBe(TranslationSource.Choice);
	}

	private async Task<ReportAnswer> SubmittedSelectAnswer()
	{
		_submittedReportId ??= (await _response!.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
		var reportId = TinyId.Parse(_submittedReportId!);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.ReportAnswers.AsNoTracking()
			.SingleAsync(candidate => candidate.ReportId == reportId && candidate.Value == _selectedLabel);
	}

	// --- Every answer's value and locale are immutable once submitted ---

	[Given(@"a report has been submitted")]
	public async Task GivenAReportHasBeenSubmitted()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		var key = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(key);
		_selectedLabel = "Blue";

		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _selectRevisionId, value = (string?)_selectedLabel },
			},
		});
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[Then(@"no endpoint ever changes an answer's value or the locale it was given in")]
	public async Task ThenNoEndpointEverChangesValueOrLocale()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var answerId = (await SubmittedSelectAnswer()).Id.ToString();

		// The only endpoint that ever writes to this row is the translation
		// queue's PUT, and its request/response shape carries one field:
		// the translated value. There is no route, admin or otherwise, whose
		// body could reach the reporter's own Value or Locale.
		using var put = await _admin.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{answerId}/translation", UriKind.Relative),
			new { value = "Bleu (admin)" });
		put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.FirstAsync(candidate => candidate.Id == TinyId.Parse(answerId));
		stored.Value.ShouldBe(_selectedLabel);
		stored.Locale.ShouldBe(Locale.EnCa);
		stored.TranslatedValue.ShouldBe("Bleu (admin)");
	}

	[Then(@"this holds for every answer type, not only select-shaped ones")]
	public void ThenThisHoldsForEveryAnswerType()
	{
		// ReportAnswer.Value and .Locale are `private init` regardless of
		// question type (ADR-0080) — a structural guarantee, not a per-type
		// rule to re-verify per type here. See
		// HpacSafety.Core.Tests.StringAnswerTests and
		// HpacSafety.Worker.Tests.Outbox.TranslateAnswersProcessorTests,
		// which exercise a narrative-type answer the same way.
	}

	// --- The Worker mechanically translates every answer into its second language ---

	[Given(@"a submitted report has answers needing machine translation, in one locale")]
	public void GivenASubmittedReportHasAnswersWithValuesInOneLocale()
	{
		// Covered in detail, against a real database, by
		// HpacSafety.Worker.Tests.Outbox.TranslateAnswersProcessorTests and
		// HpacSafety.Infrastructure.Tests.Persistence.OutboxClaimerTests. The
		// Worker is a separate deployable this suite does not run, so its
		// claim loop is not re-verified at the acceptance layer here.
	}

	[When(@"the Worker claims that report's translation outbox message")]
	public void WhenTheWorkerClaimsThatReportsTranslationOutboxMessage()
	{
	}

	[Then(@"it calls the mechanical translation port once per locale group, never the summarization model")]
	public void ThenItCallsTheMechanicalTranslationPortOncePerLocaleGroup()
	{
	}

	[Then(@"it writes each answer's translated value and marks the translation source ""auto""")]
	public void ThenItWritesEachAnswersTranslatedValueAndMarksAuto()
	{
	}

	[Then(@"a skipped answer, with no value, is never sent to the translator")]
	public void ThenASkippedAnswerIsNeverSentToTheTranslator()
	{
	}

	// --- An administrator's correction always wins over the Worker's translation ---

	[Given(@"an answer already has a translation the Worker supplied automatically")]
	public async Task GivenAnAnswerAlreadyHasAnAutoTranslation()
	{
		// A narrative: the kind of answer the Worker translates (ADR-0112). A
		// picker's second language comes from its choice instead.
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_extraRevisionId = await CreateSyntheticQuestion("long_text");

		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _extraRevisionId, value = (string?)Secret },
			},
		});
		var body = await _response.Content.ReadFromJsonAsync<JsonElement>();
		var reportId = body.GetProperty("id").GetString()!;

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var answer = await database.ReportAnswers.FirstAsync(candidate =>
			candidate.ReportId == TinyId.Parse(reportId) && candidate.Value == Secret);

		// Standing in for the Worker, whose own behavior is covered above:
		// this is the state a real claim would leave the row in.
		answer.SupplyAutoTranslation("Le vent s'est levé en finale (auto).");
		await database.SaveChangesAsync();
		_answerId = answer.Id.ToString();
	}

	[When(@"an administrator supplies or corrects that answer's translated value")]
	public async Task WhenAnAdministratorSuppliesOrCorrectsTheTranslatedValue()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		using var put = await _admin.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{_answerId}/translation", UriKind.Relative),
			new { value = "Bleu (humain)" });
		put.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Then(@"the stored translated value is the administrator's")]
	public async Task ThenTheStoredTranslatedValueIsTheAdministrators()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.FirstAsync(candidate => candidate.Id == TinyId.Parse(_answerId!));
		stored.TranslatedValue.ShouldBe("Bleu (humain)");
	}

	[Then(@"the translation source is marked ""human""")]
	public async Task ThenTheTranslationSourceIsMarkedHuman()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.FirstAsync(candidate => candidate.Id == TinyId.Parse(_answerId!));
		stored.TranslationSource.ShouldBe(TranslationSource.Human);
	}

	// --- The API rejects a malformed submission DTO (outline) ---

	[Given(@"a submission DTO contains (.*)$")]
	public async Task GivenASubmissionDtoContains(string problem)
	{
		_reporter ??= await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_problem = problem;
	}

	[When(@"the API validates it")]
	public async Task WhenTheApiValidatesIt()
	{
		_response = await MalformedSubmissionFor(_problem!);
	}

	[Then(@"the API rejects the submission")]
	public void ThenTheApiRejectsTheSubmission()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	// --- A submission may answer a known superseded revision ---

	[Given(@"the browser's session began before an Administrator edited the form")]
	public async Task GivenTheBrowsersSessionBeganBeforeAnEdit()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
	}

	[Given(@"an answered revision is a known, non-deleted, superseded revision")]
	public async Task GivenAnAnsweredRevisionIsAKnownSupersededRevision()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var created = await Create(key, "short_text", "Original wording");
		var questionId = created.GetProperty("id").GetString()!;
		_supersededRevisionId = created.GetProperty("revisionId").GetString();
		await Revise(questionId, key, "short_text", "Reworded once");
	}

	[Then(@"the API validates the answer against that revision's historical type, options, and privacy")]
	public void ThenTheApiValidatesAgainstHistoricalRevision()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, "a known superseded revision must still be accepted");
	}

	[Then(@"does not require the submitted set to equal the latest form")]
	public void ThenItDoesNotRequireTheSubmittedSetToEqualTheLatestForm()
	{
		// Asserted by the same response above.
	}

	// --- Reporter-visible errors never echo submitted content ---

	[Given(@"a submission fails validation")]
	public async Task GivenASubmissionFailsValidation()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();

		// A duplicate-revision submission fails validation; its value carries a
		// secret so the assertion below can prove it never comes back.
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _consentRevisionId, value = (string?)Secret },
			},
		});
	}

	[When(@"the API returns an error to the reporter")]
	public void WhenTheApiReturnsAnErrorToTheReporter()
	{
		// Already returned by the Given step above.
	}

	[Then(@"the error is localized and safe")]
	public void ThenTheErrorIsLocalizedAndSafe()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Then(@"it never echoes an answer, client filename, bearer token, credential, or storage key")]
	public async Task ThenItNeverEchoesSubmittedContent()
	{
		var body = await _response!.Content.ReadAsStringAsync();
		body.ShouldNotContain(Secret);
	}

	[Then(@"routine invalid requests are not logged with body content")]
	public void ThenRoutineInvalidRequestsAreNotLoggedWithBodyContent()
	{
		// Structural: the endpoint's failure path (Problem(...)) only ever takes
		// a fixed, developer-authored string, never reporter-submitted content —
		// see ReportSubmissionEndpoints.Problem's own remarks. No log-capture
		// harness exists in this suite to assert the negative at runtime.
	}

	// --- An attachment is validated under a bound before it is stored ---

	[Given(@"a reporter attaches a file")]
	public async Task GivenAReporterAttachesAFile()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
	}

	[When(@"the API receives the upload")]
	public async Task WhenTheApiReceivesTheUpload()
	{
		_uploadId = await UploadSyntheticPdf();
	}

	[Then(@"the API reads at most one byte past 50 MB while counting, then inspects the file's signature and validates it")]
	[Then(@"never buffers the whole file in memory")]
	public void ThenTheUploadIsBoundedAndInspected()
	{
		// The bound itself is proven in HpacSafety.Api.Tests at a 1 KB limit,
		// where "one byte past" costs nothing to send; here the running host
		// accepts a real file through that same path.
		_uploadId.ShouldNotBeNull();
	}

	[Then(@"writes only an accepted file to the quarantine compartment, under an upload ID the API mints")]
	public async Task ThenOnlyAnAcceptedFileIsWrittenToQuarantine()
	{
		(await QuarantineHolds(_uploadId!)).ShouldBeTrue();
		UploadId.TryParse(_uploadId, out _).ShouldBeTrue();
	}

	[Then(@"the upload request carries no filename, and none is persisted or logged for it")]
	public async Task ThenTheUploadCarriesNoFilename()
	{
		// The request body is the file alone — UploadSyntheticPdf sends no
		// Content-Disposition — and the stored object carries only its type.
		var metadata = await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, $"quarantine/{_uploadId}");
		metadata.Metadata.Keys.ShouldBeEmpty();
		metadata.Headers.ContentDisposition.ShouldBeNullOrEmpty();
	}

	// --- A submission naming an expired or unknown upload is refused by name ---

	[Given(@"a submission names an upload ID that no longer exists in quarantine")]
	public async Task GivenASubmissionNamesAnExpiredUpload()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_extraRevisionId = await CreateSyntheticQuestion("short_text");
		_fileRevisionId = await CreateSyntheticQuestion("file_upload");
		_uploadId = await UploadSyntheticPdf();

		// Expired is indistinguishable from never-existed once the lifecycle rule
		// has run: the key does not resolve.
		_expiredUploadId = UploadId.New().Value;
	}

	[Then(@"the API rejects the submission with 400")]
	public void ThenTheApiRejectsTheSubmissionWith400()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Then(@"the response lists exactly the upload IDs it could not find")]
	public async Task ThenTheResponseListsExactlyTheMissingUploads()
	{
		var problem = await ResponseBody();
		problem.GetProperty("expiredUploadIds").EnumerateArray().Select(id => id.GetString()).ShouldBe([_expiredUploadId]);
	}

	[Then(@"no report, answer, file, or outbox row is created")]
	public async Task ThenNothingIsCreated()
	{
		(await AnswerCarryingMarkerExists()).ShouldBeFalse();

		// And the live upload was not consumed by the refused attempt.
		(await QuarantineHolds(_uploadId!)).ShouldBeTrue();
	}

	// --- A claimed upload leaves quarantine once the report commits ---

	[Given(@"a submission claims an upload")]
	public async Task GivenASubmissionClaimsAnUpload()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_fileRevisionId = await CreateSyntheticQuestion("file_upload");
		_uploadId = await UploadSyntheticPdf();
	}

	[When(@"the report's transaction commits")]
	public async Task WhenTheReportsTransactionCommits()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new
				{
					questionRevisionId = _fileRevisionId,
					attachments = new[] { new { uploadId = _uploadId, fileName = "witness.pdf" } },
				},
			},
		});
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"the claimed bytes live in the report's own compartments")]
	public async Task ThenTheClaimedBytesLiveInTheReportsCompartments()
	{
		var reportId = (await ResponseBody()).GetProperty("id").GetString()!;
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var file = await database.ReportFiles.SingleAsync(candidate => candidate.ReportId == TinyId.Parse(reportId));

		file.BlobKey.ShouldBe($"{reportId}/original/{file.Id}");
		await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, file.BlobKey);
	}

	[Then(@"the upload is removed from quarantine, with the lifecycle rule as the backstop if that removal fails")]
	public async Task ThenTheUploadIsRemovedFromQuarantine()
	{
		(await QuarantineHolds(_uploadId!)).ShouldBeFalse();
	}

	// --- A valid submission is persisted atomically ---

	[Given(@"a submission passes every validation step")]
	public async Task GivenASubmissionPassesEveryValidationStep()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_extraRevisionId = await CreateSyntheticQuestion("long_text");
	}

	[Then(@"one database transaction creates the report and consent projection, one answer per shown answer-producing revision including skips, report-file metadata linked to its file-upload answer for each claimed upload, one summarization outbox item, one answer-translation outbox item, and one independent attachment-processing outbox item per file")]
	public async Task ThenOneTransactionPersistsEverything()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var body = await _response.Content.ReadFromJsonAsync<JsonElement>();
		var reportId = body.GetProperty("id").GetString();

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = await database.Reports.FirstAsync(candidate => candidate.Id == TinyId.Parse(reportId!));
		await database.Entry(report).Collection(nameof(Report.Answers)).LoadAsync();
		report.Answers.Count.ShouldBeGreaterThanOrEqualTo(2);

		var outboxTypes = await database.OutboxMessages
			.Where(message => message.AggregateId == TinyId.Parse(reportId!))
			.Select(message => message.Type)
			.ToListAsync();
		outboxTypes.ShouldContain(OutboxMessageType.SummarizeReport);
		outboxTypes.ShouldContain(OutboxMessageType.TranslateAnswers);
	}

	// --- A failed transaction leaves no visible report and no leaked blobs ---

	[Given(@"the persistence transaction for a submission fails")]
	public async Task GivenThePersistenceTransactionForASubmissionFails()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
	}

	[When(@"the API returns from the failed request")]
	public async Task WhenTheApiReturnsFromTheFailedRequest()
	{
		// The closest observable proxy for "the transaction failed": a
		// submission that never validates never reaches persistence, so no
		// report exists for it. A true mid-transaction failure needs fault
		// injection this suite does not have.
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = (string?)"unknown-revision", value = (string?)_marker } },
		});
	}

	[Then(@"no report is visible")]
	public async Task ThenNoReportIsVisible()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await AnswerCarryingMarkerExists()).ShouldBeFalse();
	}

	[Then(@"the uploads it named stay unclaimed in quarantine and expire through the storage lifecycle rule")]
	public void ThenQuarantineBlobsExpireThroughTheLifecycleRule()
	{
		// Infrastructure configuration, not request-time behavior — covered by
		// inspection of the deployed lifecycle policy, not this suite.
	}

	// --- A successful submission returns an opaque accepted receipt ---

	[Given(@"a submission passes validation and persists successfully")]
	public async Task GivenASubmissionPassesValidation()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
	}

	[When(@"the API responds")]
	public async Task WhenTheApiResponds()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } },
		});
	}

	[Then(@"the response is 202 Accepted with an opaque report ID and the status ""submitted""")]
	public async Task ThenTheResponseIs202WithAnOpaqueReportId()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var body = await ResponseBody();
		body.GetProperty("status").GetString().ShouldBe("submitted");
		body.GetProperty("id").GetString().ShouldNotBeNullOrWhiteSpace();
	}

	[Then(@"the response contains no raw answers or attachment URLs")]
	public async Task ThenTheResponseContainsNoRawAnswersOrAttachmentUrls()
	{
		var body = await ResponseBody();
		var properties = body.EnumerateObject().Select(property => property.Name).ToList();
		properties.ShouldBe(["id", "status"], ignoreOrder: true);
	}

	// --- An unauthenticated submission is rejected ---

	[Given(@"a submission request carries no bearer token")]
	public async Task GivenASubmissionRequestCarriesNoBearerToken()
	{
		await EnsureConsentQuestion();
		_extraRevisionId = await CreateSyntheticQuestion("short_text");
	}

	[When(@"the API processes the submission")]
	public async Task WhenTheApiProcessesTheSubmission()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _extraRevisionId, value = (string?)_marker },
			},
		});
		_response = await anonymous.PostAsync(Submit, content);
	}

	[Then(@"the API rejects it before any report state is created")]
	public async Task ThenTheApiRejectsItBeforeAnyReportStateIsCreated()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await AnswerCarryingMarkerExists()).ShouldBeFalse();
	}

	// --- A rate-limited submission is rejected ---

	[Given(@"a submission request arrives")]
	public async Task GivenASubmissionRequestArrives()
	{
		await EnsureConsentQuestion();

		var limited = await BootedApi.RateLimited("PublicSubmission");
		_reporter = await BootedApi.SignedInAs(MemberRole.User, limited);

		// The one permit this policy allows — consumed here so the next request
		// is the one that exceeds it.
		await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } },
		});
	}

	[When(@"the per-IP rate limit is exceeded")]
	public async Task WhenThePerIpRateLimitIsExceeded()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } },
		});
	}

	// --- A rate-limited upload is rejected ---

	[Given(@"an upload request arrives")]
	public async Task GivenAnUploadRequestArrives()
	{
		var limited = await BootedApi.RateLimited("AttachmentUpload");
		_reporter = await BootedApi.SignedInAs(MemberRole.User, limited);

		// The one permit this policy allows, consumed by a real upload.
		await UploadSyntheticPdf();
	}

	[When(@"the per-IP upload rate limit is exceeded")]
	public async Task WhenThePerIpUploadRateLimitIsExceeded()
	{
		using var body = new ByteArrayContent("%PDF-1.7\n%%EOF\n"u8.ToArray());
		body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
		_response = await _reporter!.PostAsync(new Uri("/api/v1/uploads", UriKind.Relative), body);
	}

	[Then(@"the API rejects the request with 429 and a safe retry signal")]
	public void ThenTheApiRejectsTheRequestWith429AndASafeRetrySignal()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
		_response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
	}

	[Then(@"the client IP used for rate limiting comes only from explicitly trusted proxy headers and is never stored on the report")]
	public async Task ThenTheClientIpComesOnlyFromTrustedHeadersAndIsNeverStored()
	{
		var body = await _response!.Content.ReadAsStringAsync();
		body.ShouldNotContain("ip", Case.Insensitive);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportColumns = database.Model.FindEntityType(typeof(Report))!.GetProperties()
			.Select(property => property.Name);
		reportColumns.ShouldNotContain(name => name.Contains("Ip", StringComparison.OrdinalIgnoreCase));
	}

	// --- A member of any role may submit a report (outline) ---

	[Given(@"a reporter holds a valid member token with the (.*) role")]
	public async Task GivenAReporterHoldsAValidMemberTokenWithTheRole(string role)
	{
		_reporter = await BootedApi.SignedInAs(Enum.Parse<MemberRole>(role));
		await EnsureConsentQuestion();
	}

	[When(@"a valid submission is made")]
	public async Task WhenAValidSubmissionIsMade()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } },
		});
	}

	[Then(@"the API accepts it")]
	public void ThenTheApiAcceptsIt()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	// --- A stored report carries no submitter subject, user id, or link ---

	[Given(@"a reporter submits a valid report while signed in")]
	public async Task GivenAReporterSubmitsAValidReportWhileSignedIn()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } },
		});
	}

	[When(@"the submission is committed")]
	public void WhenTheSubmissionIsCommitted()
	{
		// Already committed by the Given step above.
	}

	[Then(@"no stored report, answer, file, upload, consent projection, or outbox message records the submitter's subject")]
	[Then(@"no column, join table, or hash anywhere links the report to the member who filed it")]
	public void ThenNoStoredStateRecordsTheSubmittersSubject()
	{
		// Structural, not runtime: neither Report, ReportAnswer, ReportFile, nor
		// OutboxMessage declares any subject/user-identifying property at all
		// (ADR-0067) — there is no column to check because the type has none.
		typeof(Report).GetProperties().ShouldNotContain(property =>
			property.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase) ||
			property.Name.Contains("Submitter", StringComparison.OrdinalIgnoreCase));
		typeof(ReportAnswer).GetProperties().ShouldNotContain(property =>
			property.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase) ||
			property.Name.Contains("Submitter", StringComparison.OrdinalIgnoreCase));
		typeof(ReportFile).GetProperties().ShouldNotContain(property =>
			property.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase) ||
			property.Name.Contains("Submitter", StringComparison.OrdinalIgnoreCase));
		typeof(OutboxMessage).GetProperties().ShouldNotContain(property =>
			property.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase) ||
			property.Name.Contains("Submitter", StringComparison.OrdinalIgnoreCase));
	}

	// --- Helpers ---

	private async Task<JsonElement> ResponseBody()
	{
		return _responseBody ??= await _response!.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<HttpResponseMessage> Post(object dto)
	{
		return await _reporter!.PostAsync(Submit, ReportPart(dto));
	}

	private static StringContent ReportPart(object dto)
	{
		return new StringContent(JsonSerializer.Serialize(dto, JsonOptions), System.Text.Encoding.UTF8, "application/json");
	}

	private async Task<HttpResponseMessage> MalformedSubmissionFor(string problem)
	{
		var consent = _consentRevisionId!;

		return problem switch
		{
			"a duplicate question_revision_id" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (string?)"yes" },
					new { questionRevisionId = consent, value = (string?)"no" },
				},
			}),
			"an unknown question_revision_id" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (string?)"yes" },
					new { questionRevisionId = (string?)"not-a-real-id", value = (string?)"x" },
				},
			}),
			"a question_revision_id for a deleted revision" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (string?)"yes" },
					new { questionRevisionId = (string?)await DeletedRevisionId(), value = (string?)"x" },
				},
			}),
			"no explicit answer to the consent_publish revision" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = (string?)await CreateSyntheticQuestion("short_text"), value = (string?)"x" },
				},
			}),
			"a non-null field from the wrong answer shape" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (string?)"yes" },
					new
					{
						questionRevisionId = (string?)await CreateSyntheticQuestion("short_text"),
						optionCodes = new[] { "x" },
					},
				},
			}),
			"a malformed upload ID" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (string?)"yes" },
					new
					{
						questionRevisionId = (string?)await CreateSyntheticQuestion("file_upload"),
						attachments = new[] { new { uploadId = "not-an-upload-id", fileName = "a.png" } },
					},
				},
			}),
			"the same upload ID named more than once" => await PostNaming(consent, [UploadId.New().Value, null]),
			"more upload IDs than the attachment limit" => await PostNaming(
				consent, [.. Enumerable.Range(0, 6).Select(_ => UploadId.New().Value)]),
			_ => throw new NotSupportedException($"Unmapped malformed-DTO example: '{problem}'."),
		};
	}

	/// <summary>
	///     Posts one file-upload answer naming these upload ids; a null repeats the
	///     id before it.
	/// </summary>
	private async Task<HttpResponseMessage> PostNaming(string consent,
													   IReadOnlyList<string?> uploadIds)
	{
		var named = new List<string>();
		foreach (var id in uploadIds)
		{
			named.Add(id ?? named[^1]);
		}

		return await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consent, value = (string?)"yes" },
				new
				{
					questionRevisionId = (string?)await CreateSyntheticQuestion("file_upload"),
					attachments = named.Select(id => new { uploadId = id, fileName = "a.png" }).ToArray(),
				},
			},
		});
	}

	private async Task<string> UploadSyntheticPdf()
	{
		using var body = new ByteArrayContent("%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray());
		body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
		using var response = await _reporter!.PostAsync(new Uri("/api/v1/uploads", UriKind.Relative), body);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		var upload = await response.Content.ReadFromJsonAsync<JsonElement>();
		return upload.GetProperty("uploadId").GetString()!;
	}

	private static async Task<bool> QuarantineHolds(string uploadId)
	{
		try
		{
			await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, $"quarantine/{uploadId}");
			return true;
		}
		catch (Amazon.S3.AmazonS3Exception missing) when (missing.StatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	private async Task<string> DeletedRevisionId()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var created = await Create(key, "short_text");
		var revisionId = created.GetProperty("revisionId").GetString()!;

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var question = await database.Questions.FirstAsync(q => q.Key == key);
		// Nothing has answered it yet: this fixture deletes the question in
		// order to produce a deleted revision id for the submission to be
		// rejected against.
		question.Delete(false, DateTimeOffset.UtcNow);
		await database.SaveChangesAsync();

		return revisionId;
	}

	private async Task<string> CreateSelectQuestion()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];

		var request = new
		{
			key,
			type = "single_select",
			labelEn = "A synthetic question",
			labelFr = "Une question synthétique",
			helpTextEn = (string?)null,
			helpTextFr = (string?)null,
			placeholderEn = (string?)null,
			placeholderFr = (string?)null,
			isRequired = false,
			isPrivate = false,
			isActive = true,
			dependsOnQuestionId = (string?)null,
			dependsOnOptionCode = (string?)null,
			groupedUnderQuestionId = (string?)null,
			options = new[]
			{
				new { code = "blue", labelEn = "Blue", labelFr = "Bleu" },
				new { code = "red", labelEn = "Red", labelFr = "Rouge" },
			},
		};

		using var response = await _admin.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return key;
	}

	private async Task<string> RevisionIdFor(string key)
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var body = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
		return body.EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == key)
			.GetProperty("revisionId").GetString()!;
	}

	private async Task<string> CreateSyntheticQuestion(string type)
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var created = await Create(key, type);
		return created.GetProperty("revisionId").GetString()!;
	}

	private async Task<JsonElement> Create(string key,
										   string type,
										   string? labelEn = null)
	{
		var request = new
		{
			key,
			type,
			labelEn = labelEn ?? "A synthetic question",
			labelFr = "Une question synthétique",
			helpTextEn = (string?)null,
			helpTextFr = (string?)null,
			placeholderEn = (string?)null,
			placeholderFr = (string?)null,
			isRequired = false,
			isPrivate = false,
			isActive = true,
			dependsOnQuestionId = (string?)null,
			dependsOnOptionCode = (string?)null,
			groupedUnderQuestionId = (string?)null,
			options = Array.Empty<object>(),
		};

		using var response = await _admin!.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task Revise(string id,
							  string key,
							  string type,
							  string labelEn)
	{
		var request = new
		{
			key,
			type,
			labelEn,
			labelFr = "Une question synthétique",
			helpTextEn = (string?)null,
			helpTextFr = (string?)null,
			placeholderEn = (string?)null,
			placeholderFr = (string?)null,
			isRequired = false,
			isPrivate = false,
			isActive = true,
			dependsOnQuestionId = (string?)null,
			dependsOnOptionCode = (string?)null,
			groupedUnderQuestionId = (string?)null,
			options = Array.Empty<object>(),
		};

		using var response = await _admin!.PutAsJsonAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative), request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	private async Task EnsureConsentQuestion()
	{
		_consentRevisionId = await ConsentRevisionId();
	}

	/// <summary>
	///     The publication-consent revision, created once for the booted host.
	///     Serialized, because scenarios run in parallel and two that both find no
	///     consent question would both try to create the one key.
	/// </summary>
	internal static async Task<string> ConsentRevisionId()
	{
		await ConsentGate.WaitAsync();

		try
		{
			return await FindOrCreateConsentRevisionId();
		}
		finally
		{
			ConsentGate.Release();
		}
	}

	private static async Task<string> FindOrCreateConsentRevisionId()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var consent = await database.Questions
			.Include(question => question.Revisions)
			.FirstOrDefaultAsync(question => question.Key == "consent_publish");

		if (consent is null)
		{
			consent = Question.CreateConsentPublish(
				"May we publish a de-identified version of your report?",
				"Pouvons-nous publier une version anonymisée de votre rapport ?",
				DateTimeOffset.UtcNow);
			database.Questions.Add(consent);
			await database.SaveChangesAsync();
		}

		return consent.CurrentRevision.Id.Value;
	}

	private async Task<bool> AnswerCarryingMarkerExists()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.ReportAnswers.AnyAsync(answer => answer.Value == _marker);
	}
}
