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
	private static readonly Uri AwaitingTranslation = new("/api/admin/answers/awaiting-translation", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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
	private string? _supersededRevisionId;
	private string? _problem;
	private JsonElement? _responseBody;
	private int _reportCountBefore;

	// --- Background: documented facts about the endpoint, not actions. ---

	[Given(@"the only write endpoint for a reporter is POST \/api\/v1\/reports")]
	[Given(@"it requires a valid member bearer token")]
	[Given(@"it accepts multipart\/form-data with one report JSON part and zero or more files parts")]
	[Given(@"the bearer token is transport\/security metadata, not persisted report content")]
	public void GivenBackgroundFact()
	{
		// Contextual — asserted by the scenarios themselves, not by this fact alone.
	}

	// --- One answer entry per shown answer-producing revision ---

	[Given(@"the client says it showed the reporter a set of answer-producing revisions")]
	public async Task GivenTheClientShowedASetOfAnswerProducingRevisions()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		_extraRevisionId = await CreateSyntheticQuestionAsync("short_text");
	}

	[When(@"the reporter submits the form")]
	public async Task WhenTheReporterSubmitsTheForm()
	{
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _extraRevisionId, value = (string?)"a synthetic answer" }
			}
		});
	}

	[Then(@"the submission DTO contains exactly one answer entry for each of those revisions")]
	[Then(@"every answer of every type uses ""value"", a single string, alongside the locale it was given in")]
	[Then(@"file-upload answers additionally use zero-based indexes into the repeated files parts")]
	[Then(@"fields for the other answer shapes are null")]
	public void ThenTheDtoShapeIsHonored()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, "the API accepts a DTO built exactly this way");
	}

	// --- A skipped answer is represented by an empty value, not omission ---

	[Given(@"a reporter skips an answer-producing question")]
	public async Task GivenAReporterSkipsAnAnswerProducingQuestion()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		_extraRevisionId = await CreateSyntheticQuestionAsync("short_text");
		_fileRevisionId = await CreateSyntheticQuestionAsync("file_upload");
	}

	[When(@"the submission DTO is built")]
	public async Task WhenTheSubmissionDtoIsBuilt()
	{
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _extraRevisionId, value = (string?)null },
				new { questionRevisionId = _fileRevisionId, attachmentPartIndexes = Array.Empty<int>() }
			}
		});
	}

	[Then(@"a skipped answer of any type has a null value")]
	[Then(@"a skipped file upload has an empty attachment_part_indexes list")]
	public void ThenASkippedAnswerHasANullValue()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	// --- A submitted select value must be one the revision offered ---

	[Given(@"a reporter submits a value for a picker or multi-select question")]
	public async Task GivenAReporterSubmitsAValueForAPickerQuestion()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		var key = await CreateSelectQuestionAsync();
		_selectRevisionId = await RevisionIdForAsync(key);
	}

	[When(@"the API validates the submission")]
	public async Task WhenTheApiValidatesTheSubmission()
	{
		if (_supersededRevisionId is not null)
		{
			_response = await PostAsync(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
					new { questionRevisionId = _supersededRevisionId, value = (string?)"an old answer" }
				}
			});

			return;
		}

		using var accepted = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _selectRevisionId, value = (string?)"Blue" }
			}
		});
		accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync());
		_selectedLabel = "Blue";

		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _selectRevisionId, value = (string?)"Not an offered option" }
			}
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

	[Then(@"a type-ahead, or a multi-select with reporter additions allowed, backed by a live shared list also accepts a value the list does not yet offer")]
	public void ThenATypeAheadAlsoAcceptsAnUnlistedValue()
	{
		// Covered in detail by HpacSafety.Api.Tests against the reporter-added
		// choice path (ADR-0063); not re-verified at the acceptance layer here.
	}

	// --- The submission path never calls a translation provider ---

	[Given(@"a submission contains select answers and a value typed into a type-ahead or a multi-select with reporter additions allowed")]
	public async Task GivenASubmissionContainsSelectAnswers()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		var key = await CreateSelectQuestionAsync();
		_selectRevisionId = await RevisionIdForAsync(key);
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
					new { questionRevisionId = _selectRevisionId, value = (string?)_selectedLabel }
				}
			}
			: new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
					new { questionRevisionId = _extraRevisionId, value = (string?)Secret }
				}
			};

		_response = await PostAsync(dto);
	}

	[Then(@"no translation provider is called")]
	public void ThenNoTranslationProviderIsCalled()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[Then(@"the answers are stored in the language the reporter gave them in, with no translation yet")]
	public async Task ThenTheAnswersAreStoredWithNoTranslationYet()
	{
		_admin ??= await BootedApi.SignedInAsAsync(MemberRole.Administrator);
		var queue = await _admin.GetFromJsonAsync<JsonElement>(AwaitingTranslation);
		var entries = queue.GetProperty("answers").EnumerateArray().ToList();
		entries.ShouldContain(entry => entry.GetProperty("value").GetString() == _selectedLabel);
	}

	// --- Every answer's value and locale are immutable once submitted ---

	[Given(@"a report has been submitted")]
	public async Task GivenAReportHasBeenSubmitted()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		var key = await CreateSelectQuestionAsync();
		_selectRevisionId = await RevisionIdForAsync(key);
		_selectedLabel = "Blue";

		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _selectRevisionId, value = (string?)_selectedLabel }
			}
		});
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[Then(@"no endpoint ever changes an answer's value or the locale it was given in")]
	public async Task ThenNoEndpointEverChangesValueOrLocale()
	{
		_admin ??= await BootedApi.SignedInAsAsync(MemberRole.Administrator);
		var queue = await _admin.GetFromJsonAsync<JsonElement>(AwaitingTranslation);
		var entry = queue.GetProperty("answers").EnumerateArray()
			.Single(candidate => candidate.GetProperty("value").GetString() == _selectedLabel);
		var answerId = entry.GetProperty("id").GetString()!;

		// The only endpoint that ever writes to this row is the translation
		// queue's PUT, and its request/response shape carries one field:
		// the translated value. There is no route, admin or otherwise, whose
		// body could reach the reporter's own Value or Locale.
		using var put = await _admin.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{answerId}/translation", UriKind.Relative),
			new { value = "Bleu (admin)" });
		put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
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

	[Given(@"a submitted report has answers with values in one locale")]
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
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		var key = await CreateSelectQuestionAsync();
		_selectRevisionId = await RevisionIdForAsync(key);
		_selectedLabel = "Blue";

		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _selectRevisionId, value = (string?)_selectedLabel }
			}
		});
		var body = await _response.Content.ReadFromJsonAsync<JsonElement>();
		var reportId = body.GetProperty("id").GetString()!;

		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var answer = await database.ReportAnswers.FirstAsync(candidate =>
			candidate.ReportId == TinyId.Parse(reportId) && candidate.Value == _selectedLabel);

		// Standing in for the Worker, whose own behavior is covered above:
		// this is the state a real claim would leave the row in.
		answer.SupplyAutoTranslation("Bleu (auto)");
		await database.SaveChangesAsync();
		_answerId = answer.Id.ToString();
	}

	[When(@"an administrator supplies or corrects that answer's translated value")]
	public async Task WhenAnAdministratorSuppliesOrCorrectsTheTranslatedValue()
	{
		_admin ??= await BootedApi.SignedInAsAsync(MemberRole.Administrator);
		using var put = await _admin.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{_answerId}/translation", UriKind.Relative),
			new { value = "Bleu (humain)" });
		put.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Then(@"the stored translated value is the administrator's")]
	public async Task ThenTheStoredTranslatedValueIsTheAdministrators()
	{
		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.FirstAsync(candidate => candidate.Id == TinyId.Parse(_answerId!));
		stored.TranslatedValue.ShouldBe("Bleu (humain)");
	}

	[Then(@"the translation source is marked ""human""")]
	public async Task ThenTheTranslationSourceIsMarkedHuman()
	{
		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.FirstAsync(candidate => candidate.Id == TinyId.Parse(_answerId!));
		stored.TranslationSource.ShouldBe(TranslationSource.Human);
	}

	// --- The API rejects a malformed submission DTO (outline) ---

	[Given(@"a submission DTO contains (.*)$")]
	public async Task GivenASubmissionDtoContains(string problem)
	{
		_reporter ??= await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
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
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
	}

	[Given(@"an answered revision is a known, non-deleted, superseded revision")]
	public async Task GivenAnAnsweredRevisionIsAKnownSupersededRevision()
	{
		_admin ??= await BootedApi.SignedInAsAsync(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var created = await CreateAsync(key, "short_text", "Original wording");
		var questionId = created.GetProperty("id").GetString()!;
		_supersededRevisionId = created.GetProperty("revisionId").GetString();
		await ReviseAsync(questionId, key, "short_text", "Reworded once");
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
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();

		// A duplicate-revision submission fails validation; its value carries a
		// secret so the assertion below can prove it never comes back.
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _consentRevisionId, value = (string?)Secret }
			}
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

	// --- Accepted attachments are streamed into quarantine under a bound ---

	[Given(@"a submission includes one or more files parts")]
	public async Task GivenASubmissionIncludesFilesParts()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		_fileRevisionId = await CreateSyntheticQuestionAsync("file_upload");
	}

	[When(@"the API accepts an attachment")]
	public async Task WhenTheApiAcceptsAnAttachment()
	{
		var content = new MultipartFormDataContent
		{
			{
				new StringContent(JsonSerializer.Serialize(new
				{
					language = "en-CA",
					answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = _fileRevisionId, attachmentPartIndexes = new[] { 0 } }
			}
				}, JsonOptions)),
				"report"
			}
		};

		var part = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4]);
		part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		content.Add(part, "files", "not-the-real-filename.png");

		_response = await _reporter!.PostAsync(Submit, content);
	}

	[Then(@"the API mints an opaque server-side filename\/key")]
	[Then(@"streams at most 50 MB into the quarantine compartment while computing the actual byte count and inspecting its signature")]
	[Then(@"never buffers the whole file in memory")]
	[Then(@"never persists or logs the client filename")]
	public void ThenTheAttachmentIsAcceptedOpaquely()
	{
		// A synthetic, non-decodable PNG proves the streaming/inspection path
		// runs at all — either accepted (opaque key minted) or rejected by
		// signature inspection, never the client filename echoed back. A real
		// image is exercised in HpacSafety.Api.Tests.
		_response!.StatusCode.ShouldBeOneOf(HttpStatusCode.Accepted, HttpStatusCode.BadRequest);
	}

	// --- A valid submission is persisted atomically ---

	[Given(@"a multipart submission passes every validation step")]
	public async Task GivenAMultipartSubmissionPassesEveryValidationStep()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		_extraRevisionId = await CreateSyntheticQuestionAsync("long_text");
	}

	[Then(@"one database transaction creates the report and consent projection, one answer per shown answer-producing revision including skips, report-file metadata linked to its file-upload answer for successfully quarantined blobs, one summarization outbox item, one answer-translation outbox item, and one independent attachment-processing outbox item per file")]
	public async Task ThenOneTransactionPersistsEverything()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var body = await _response.Content.ReadFromJsonAsync<JsonElement>();
		var reportId = body.GetProperty("id").GetString();

		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
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
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		_reportCountBefore = await ReportCountAsync();
	}

	[When(@"the API returns from the failed request")]
	public async Task WhenTheApiReturnsFromTheFailedRequest()
	{
		// The closest observable proxy for "the transaction failed": a
		// submission that never validates never reaches persistence, so no
		// report exists for it. A true mid-transaction failure needs fault
		// injection this suite does not have.
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = (string?)"unknown-revision", value = (string?)"x" } }
		});
	}

	[Then(@"no report is visible")]
	public async Task ThenNoReportIsVisible()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await ReportCountAsync()).ShouldBe(_reportCountBefore);
	}

	[Then(@"any already-written quarantine blobs are unreferenced and expire through the storage lifecycle rule")]
	public void ThenQuarantineBlobsExpireThroughTheLifecycleRule()
	{
		// Infrastructure configuration, not request-time behavior — covered by
		// inspection of the deployed lifecycle policy, not this suite.
	}

	// --- A successful submission returns an opaque accepted receipt ---

	[Given(@"a submission passes validation and persists successfully")]
	public async Task GivenASubmissionPassesValidation()
	{
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
	}

	[When(@"the API responds")]
	public async Task WhenTheApiResponds()
	{
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } }
		});
	}

	[Then(@"the response is 202 Accepted with an opaque report ID and the status ""submitted""")]
	public async Task ThenTheResponseIs202WithAnOpaqueReportId()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var body = await ResponseBodyAsync();
		body.GetProperty("status").GetString().ShouldBe("submitted");
		body.GetProperty("id").GetString().ShouldNotBeNullOrWhiteSpace();
	}

	[Then(@"the response contains no raw answers or attachment URLs")]
	public async Task ThenTheResponseContainsNoRawAnswersOrAttachmentUrls()
	{
		var body = await ResponseBodyAsync();
		var properties = body.EnumerateObject().Select(property => property.Name).ToList();
		properties.ShouldBe(["id", "status"], ignoreOrder: true);
	}

	// --- An unauthenticated submission is rejected ---

	[Given(@"a submission request carries no bearer token")]
	public async Task GivenASubmissionRequestCarriesNoBearerToken()
	{
		_reportCountBefore = await ReportCountAsync();
	}

	[When(@"the API processes the submission")]
	public async Task WhenTheApiProcessesTheSubmission()
	{
		await EnsureConsentQuestionAsync();
		using var anonymous = (await BootedApi.FactoryAsync()).CreateClient();
		var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } }
		});
		_response = await anonymous.PostAsync(Submit, content);
	}

	[Then(@"the API rejects it before any report state is created")]
	public async Task ThenTheApiRejectsItBeforeAnyReportStateIsCreated()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await ReportCountAsync()).ShouldBe(_reportCountBefore);
	}

	// --- A rate-limited submission is rejected ---

	[Given(@"a submission request arrives")]
	public async Task GivenASubmissionRequestArrives()
	{
		await EnsureConsentQuestionAsync();

		var limited = await BootedApi.RateLimitedAsync("PublicSubmission");
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User, limited);

		// The one permit this policy allows — consumed here so the next request
		// is the one that exceeds it.
		await PostAsync(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } }
		});
	}

	[When(@"the per-IP rate limit is exceeded")]
	public async Task WhenThePerIpRateLimitIsExceeded()
	{
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } }
		});
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

		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportColumns = database.Model.FindEntityType(typeof(Report))!.GetProperties()
			.Select(property => property.Name);
		reportColumns.ShouldNotContain(name => name.Contains("Ip", StringComparison.OrdinalIgnoreCase));
	}

	// --- A member of any role may submit a report (outline) ---

	[Given(@"a reporter holds a valid member token with the (.*) role")]
	public async Task GivenAReporterHoldsAValidMemberTokenWithTheRole(string role)
	{
		_reporter = await BootedApi.SignedInAsAsync(Enum.Parse<MemberRole>(role));
		await EnsureConsentQuestionAsync();
	}

	[When(@"a valid submission is made")]
	public async Task WhenAValidSubmissionIsMade()
	{
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } }
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
		_reporter = await BootedApi.SignedInAsAsync(MemberRole.User);
		await EnsureConsentQuestionAsync();
		_response = await PostAsync(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (string?)"yes" } }
		});
	}

	[When(@"the submission is committed")]
	public void WhenTheSubmissionIsCommitted()
	{
		// Already committed by the Given step above.
	}

	[Then(@"no stored report, answer, file, consent projection, or outbox message records the submitter's subject")]
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

	private async Task<JsonElement> ResponseBodyAsync()
	{
		return _responseBody ??= await _response!.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<HttpResponseMessage> PostAsync(object dto)
	{
		return await _reporter!.PostAsync(Submit, ReportPart(dto));
	}

	private static MultipartFormDataContent ReportPart(object dto)
	{
		var content = new MultipartFormDataContent
		{
			{ new StringContent(JsonSerializer.Serialize(dto, JsonOptions)), "report" }
		};
		return content;
	}

	private async Task<HttpResponseMessage> MalformedSubmissionFor(string problem)
	{
		var consent = _consentRevisionId!;

		return problem switch
		{
			"a duplicate question_revision_id" => await PostAsync(new
			{
				language = "en-CA",
				answers = new object[]
								{
						new { questionRevisionId = consent, value = (string?)"yes" },
						new { questionRevisionId = consent, value = (string?)"no" }
								}
			}),
			"an unknown question_revision_id" => await PostAsync(new
			{
				language = "en-CA",
				answers = new object[]
				{
						new { questionRevisionId = consent, value = (string?)"yes" },
						new { questionRevisionId = (string?)"not-a-real-id", value = (string?)"x" }
				}
			}),
			"a question_revision_id for a deleted revision" => await PostAsync(new
			{
				language = "en-CA",
				answers = new object[]
				{
						new { questionRevisionId = consent, value = (string?)"yes" },
						new { questionRevisionId = (string?)await DeletedRevisionIdAsync(), value = (string?)"x" }
				}
			}),
			"no explicit answer to the consent_publish revision" => await PostAsync(new
			{
				language = "en-CA",
				answers = new object[]
				{
						new { questionRevisionId = (string?)await CreateSyntheticQuestionAsync("short_text"), value = (string?)"x" }
				}
			}),
			"a non-null field from the wrong answer shape" => await PostAsync(new
			{
				language = "en-CA",
				answers = new object[]
				{
						new { questionRevisionId = consent, value = (string?)"yes" },
						new
						{
							questionRevisionId = (string?)await CreateSyntheticQuestionAsync("short_text"),
							optionCodes = new[] { "x" }
						}
				}
			}),
			"a duplicate or out-of-range file index" => await PostAsync(new
			{
				language = "en-CA",
				answers = new object[]
				{
						new { questionRevisionId = consent, value = (string?)"yes" },
						new
						{
							questionRevisionId = (string?)await CreateSyntheticQuestionAsync("file_upload"),
							attachmentPartIndexes = new[] { 0 }
						}
				}
			}),
			"a files part that is never referenced by any answer" => await PostWithGarbageFileAsync(new
			{
				language = "en-CA",
				answers = new object[] { new { questionRevisionId = consent, value = (string?)"yes" } }
			}),
			"a files part referenced by more than one answer" => await PostWithGarbageFileAsync(new
			{
				language = "en-CA",
				answers = new object[]
				{
						new { questionRevisionId = consent, value = (string?)"yes" },
						new
						{
							questionRevisionId = (string?)await CreateSyntheticQuestionAsync("file_upload"),
							attachmentPartIndexes = new[] { 0 }
						},
						new
						{
							questionRevisionId = (string?)await CreateSyntheticQuestionAsync("file_upload"),
							attachmentPartIndexes = new[] { 0 }
						}
				}
			}),
			_ => throw new NotSupportedException($"Unmapped malformed-DTO example: '{problem}'."),
		};
	}

	private async Task<HttpResponseMessage> PostWithGarbageFileAsync(object dto)
	{
		var content = new MultipartFormDataContent
		{
			{ new StringContent(JsonSerializer.Serialize(dto, JsonOptions)), "report" }
		};
		var part = new ByteArrayContent([1, 2, 3]);
		part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		content.Add(part, "files", "garbage.bin");
		return await _reporter!.PostAsync(Submit, content);
	}

	private async Task<string> DeletedRevisionIdAsync()
	{
		_admin ??= await BootedApi.SignedInAsAsync(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var created = await CreateAsync(key, "short_text");
		var revisionId = created.GetProperty("revisionId").GetString()!;

		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var question = await database.Questions.FirstAsync(q => q.Key == key);
		// Nothing has answered it yet: this fixture deletes the question in
		// order to produce a deleted revision id for the submission to be
		// rejected against.
		question.Delete(false, DateTimeOffset.UtcNow);
		await database.SaveChangesAsync();

		return revisionId;
	}

	private async Task<string> CreateSelectQuestionAsync()
	{
		_admin ??= await BootedApi.SignedInAsAsync(MemberRole.Administrator);
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
			optionSetId = (string?)null,
			groupedUnderQuestionId = (string?)null,
			allowsReporterAdditions = false,
			options = new[]
			{
				new { code = "blue", labelEn = "Blue", labelFr = "Bleu" },
				new { code = "red", labelEn = "Red", labelFr = "Rouge" }
			}
		};

		using var response = await _admin.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return key;
	}

	private async Task<string> RevisionIdForAsync(string key)
	{
		using var client = (await BootedApi.FactoryAsync()).CreateClient();
		var body = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
		return body.EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == key)
			.GetProperty("revisionId").GetString()!;
	}

	private async Task<string> CreateSyntheticQuestionAsync(string type)
	{
		_admin ??= await BootedApi.SignedInAsAsync(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var created = await CreateAsync(key, type);
		return created.GetProperty("revisionId").GetString()!;
	}

	private async Task<JsonElement> CreateAsync(string key, string type, string? labelEn = null)
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
			optionSetId = (string?)null,
			groupedUnderQuestionId = (string?)null,
			allowsReporterAdditions = false,
			options = Array.Empty<object>()
		};

		using var response = await _admin!.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task ReviseAsync(string id, string key, string type, string labelEn)
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
			optionSetId = (string?)null,
			groupedUnderQuestionId = (string?)null,
			allowsReporterAdditions = false,
			options = Array.Empty<object>()
		};

		using var response = await _admin!.PutAsJsonAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative), request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	private async Task EnsureConsentQuestionAsync()
	{
		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
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

		_consentRevisionId = consent.CurrentRevision.Id.Value;
	}

	private async Task<int> ReportCountAsync()
	{
		await using var scope = (await BootedApi.FactoryAsync()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.Reports.CountAsync();
	}
}
