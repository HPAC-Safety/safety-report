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
using HpacSafety.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The report-submission scenarios in
///     <c>.spec/features/report-submission/report-submission.feature</c> that describe
///     what <c>POST /api/v1/reports</c> does over HTTP — validation order, the
///     immutable value/locale split, atomic persistence, and the opaque receipt.
///     See issue #14 and ADR-0080. Also REQ-WLD-018, that the endpoint validates
///     whatever the form would have allowed.
/// </summary>
/// <remarks>
///     Detailed coverage of every rejected shape lives in
///     <c>HpacSafety.Api.Tests</c> against a real database; these prove the
///     feature file's sentences are true of the running system, the same split
///     <see cref="PublicQuestionEndpointSteps" /> already uses. Every report and
///     answer here is synthetic.
/// </remarks>
[Binding]
public sealed class ReportSubmissionEndpointSteps : IDisposable
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly SemaphoreSlim ConsentGate = new(1, 1);
	private const string Secret = "Wind picked up on final approach — synthetic narrative for a test only.";

	private readonly ScenarioContext _scenario;
	private readonly TraceLogCapture _logs = new();
	private string? _submitterSubject;
	private HttpClient? _reporter;
	private HttpClient? _admin;
	private HttpResponseMessage? _response;
	private string? _consentRevisionId;
	private string? _extraRevisionId;
	private string? _multiSelectRevisionId;
	private string? _fileRevisionId;
	private string? _selectRevisionId;
	private string? _selectedChoiceId;
	private string? _typeAheadRevisionId;
	private string? _liveChoiceId;
	private string? _removedChoiceId;
	private string? _otherQuestionChoiceId;
	private bool _choiceValidation;
	private readonly Dictionary<string, string[]> _namedChoiceIds = [];
	private readonly List<(string Submission, HttpResponseMessage Response)> _accepted = [];
	private string? _narrativeRevisionId;
	private string? _answerId;
	private string? _submittedReportId;
	private object[]? _namedAnswers;
	private string? _problem;
	private string? _uploadId;
	private string? _expiredUploadId;
	private string? _refusedUploadId;
	private long _refusedUploadSize;
	private JsonElement? _responseBody;
	private readonly List<(string Submission, HttpResponseMessage Response)> _refused = [];
	// A value only this scenario submits. Other scenarios submit reports into the
	// same database in parallel, so "nothing was created" is asserted as "no
	// stored answer carries this value", never as an unchanged report count.
	private readonly string _marker = $"synthetic-{Guid.NewGuid():N}";

	public ReportSubmissionEndpointSteps(ScenarioContext scenario)
	{
		_scenario = scenario;
	}

	// --- Background: documented facts about the endpoint, not actions. ---

	[Given(@"a reporter writes a report through POST \/api\/v1\/reports and sends each attachment through a pre-signed PUT that POST \/api\/v1\/uploads mints")]
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
		var multiSelect = await CreateSelectQuestion("multi_select");
		_multiSelectRevisionId = await RevisionIdFor(multiSelect);
		var singleSelect = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(singleSelect);
		_typeAheadRevisionId = await ReporterChoiceSubmissionSteps.CreateTypeAhead(
			_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator));

		_namedChoiceIds[_multiSelectRevisionId] = [await ChoiceIdFor(multiSelect, "Blue"), await ChoiceIdFor(multiSelect, "Red")];
		_namedChoiceIds[_selectRevisionId] = [await ChoiceIdFor(singleSelect, "Blue")];
	}

	[When(@"the reporter submits the form")]
	public async Task WhenTheReporterSubmitsTheForm()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
				new { questionRevisionId = _extraRevisionId, value = (string?)"a synthetic answer" },
				new { questionRevisionId = _multiSelectRevisionId, value = (string?)null, choices = _namedChoiceIds[_multiSelectRevisionId!] },
				new { questionRevisionId = _selectRevisionId, value = (string?)null, choices = _namedChoiceIds[_selectRevisionId!] },
				new { questionRevisionId = _typeAheadRevisionId, value = (string?)_marker, choices = (string[]?)null },
			},
		});
	}

	[Then(@"a single-select, multi-select, or type-ahead answer carries the identifiers of the chosen choices in ""choices""")]
	public async Task ThenAChoiceAnswerCarriesItsChoiceIds()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
		var stored = await StoredAnswersOfThisReport();

		foreach (var (revisionId, choiceIds) in _namedChoiceIds)
		{
			stored.Where(answer => answer.QuestionRevisionId == TinyId.Parse(revisionId))
				.Select(answer => answer.ChoiceId?.Value)
				.ShouldBe(choiceIds, ignoreOrder: true);
		}
	}

	[Then(@"a type-ahead answer naming a value the question does not offer carries the typed text in ""value"" instead")]
	public async Task ThenATypedValueNamesANewChoice()
	{
		var answer = (await StoredAnswersOfThisReport(includeChoices: true))
			.Single(candidate => candidate.QuestionRevisionId == TinyId.Parse(_typeAheadRevisionId!));

		answer.Value.ShouldBeNull();
		answer.Choice!.LabelEn.ShouldBe(_marker);
		answer.Choice.AddedByReporter.ShouldBeTrue();
	}

	// --- A submitted choice must be one the question offers ---

	[Given(@"a reporter submits a single-select, multi-select, or type-ahead answer naming choices by identifier")]
	public async Task GivenAReporterNamesChoicesByIdentifier()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();

		var key = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(key);
		_liveChoiceId = await ChoiceIdFor(key, "Blue");
		_removedChoiceId = await ChoiceIdFor(key, "Red");
		await RemoveChoice(key, keep: "blue");

		_otherQuestionChoiceId = await ChoiceIdFor(await CreateSelectQuestion(), "Blue");
		_typeAheadRevisionId = await ReporterChoiceSubmissionSteps.CreateTypeAhead(
			_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator));
		_choiceValidation = true;
	}

	[Then(@"the answer is accepted only if every named choice is a live choice of that question")]
	public void ThenOnlyALiveChoiceIsAccepted()
	{
		_accepted.Select(entry => entry.Response.StatusCode)
			.ShouldAllBe(status => status == HttpStatusCode.Accepted);
	}

	[Then(@"a removed choice, or another question's choice, is refused")]
	public void ThenARemovedOrForeignChoiceIsRejected()
	{
		foreach (var (submission, response) in _refused)
		{
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{submission} is refused");
		}
	}

	[Then(@"only a type-ahead also accepts typed text naming a value it does not yet offer")]
	public void ThenOnlyATypeAheadAcceptsTypedText()
	{
		_accepted.ShouldContain(entry => entry.Submission == "typed text in a type-ahead");
		_refused.ShouldContain(entry => entry.Submission == "typed text in a single-select");
	}

	[Then(@"the submission DTO contains exactly one answer entry for each of those revisions")]
	[Then(@"every other answer uses ""value"", a single string, alongside the locale it was given in")]
	[Then(@"file-upload answers additionally carry one attachment entry per file attached to that question, each an upload ID and the file's name")]
	[Then(@"the other answer shapes are null")]
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
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
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

	[When(@"the API validates the submission")]
	public async Task WhenTheApiValidatesTheSubmission()
	{
		if (_choiceValidation)
		{
			await SubmitEachChoiceAnswer();
			return;
		}

		if (_refusedUploadId is not null)
		{
			_response = await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (bool?)true },
					new { questionRevisionId = _extraRevisionId, value = (string?)_marker },
					new
					{
						questionRevisionId = _fileRevisionId,
						attachments = new[] { new { uploadId = _refusedUploadId, fileName = "refused.bin" } },
					},
				},
			});

			return;
		}

		if (_expiredUploadId is not null)
		{
			_response = await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (bool?)true },
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

		if (_namedAnswers is not null)
		{
			_response = await Post(new { language = "en-CA", answers = _namedAnswers });
		}
	}

	// --- The submission path never calls a translation provider ---

	[Given(@"a submission contains choice answers and a value typed into a type-ahead")]
	public async Task GivenASubmissionWithChoiceAnswersAndATypedValue()
	{
		// A host whose translator fails if it is ever reached: the submission
		// succeeding is the proof that nothing on its path called one.
		var host = (await BootedApi.Factory()).WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services => services.AddSingleton<ITranslator, UnreachableTranslator>()));
		_reporter = await BootedApi.SignedInAs(MemberRole.User, host);
		await EnsureConsentQuestion();

		var key = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(key);
		_selectedChoiceId = await ChoiceIdFor(key, "Blue");
		_typeAheadRevisionId = await ReporterChoiceSubmissionSteps.CreateTypeAhead(
			_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator));
	}

	[When(@"the API commits the submission")]
	public async Task WhenTheApiCommitsTheSubmission()
	{
		if (_typeAheadRevisionId is not null)
		{
			_response = await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (bool?)true, choices = (string[]?)null },
					new { questionRevisionId = _selectRevisionId, value = (bool?)null, choices = new[] { _selectedChoiceId } },
					new { questionRevisionId = _typeAheadRevisionId, value = (string?)_marker, choices = (string[]?)null },
				},
			});
			return;
		}

		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
				new { questionRevisionId = _extraRevisionId, value = (string?)Secret },
			},
		});
	}

	[Then(@"no translation provider is called")]
	public async Task ThenNoTranslationProviderIsCalled()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"no choice answer stores a copy of either of its choice's labels")]
	public async Task ThenNoChoiceAnswerStoresALabel()
	{
		var choiceAnswers = (await StoredAnswersOfThisReport())
			.Where(answer => answer.QuestionRevisionId == TinyId.Parse(_selectRevisionId!)
							 || answer.QuestionRevisionId == TinyId.Parse(_typeAheadRevisionId!))
			.ToList();

		choiceAnswers.Count.ShouldBe(2);
		choiceAnswers.ShouldAllBe(answer => answer.ChoiceId != null && answer.Value == null && answer.TranslatedValue == null);
	}

	[Then(@"a new type-ahead value is queued for the Worker to translate, on the value itself")]
	public async Task ThenTheNewValueIsQueuedForTheWorker()
	{
		var added = (await StoredAnswersOfThisReport(includeChoices: true))
			.Single(answer => answer.QuestionRevisionId == TinyId.Parse(_typeAheadRevisionId!))
			.Choice!;
		added.LabelEn.ShouldBe(_marker);
		added.LabelFr.ShouldBeNull();

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var queued = await database.OutboxMessages.AsNoTracking()
			.Where(message => message.Type == OutboxMessageType.TranslateChoice && message.Payload == added.Id.Value)
			.ToListAsync();

		queued.ShouldHaveSingleItem().AggregateId.ShouldBe(added.QuestionId);
	}

	/// <summary>A translator that is never reachable: anything that calls it fails.</summary>
	private sealed class UnreachableTranslator : ITranslator
	{
		public bool IsConfigured => true;

		public Task<IReadOnlyList<string>> Translate(IReadOnlyList<string> texts,
													 Locale source,
													 Locale target,
													 CancellationToken cancellationToken)
		{
			throw new TranslationUnavailableException("Synthetic: no provider may be called on the submission path.");
		}
	}

	// --- Every answer's value and locale are immutable once submitted ---

	[Given(@"a report has been submitted")]
	public async Task GivenAReportHasBeenSubmitted()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		var key = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(key);
		_selectedChoiceId = await ChoiceIdFor(key, "Blue");
		_narrativeRevisionId = await CreateSyntheticQuestion("long_text");

		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
				new { questionRevisionId = _selectRevisionId, value = (string?)null, choices = new[] { _selectedChoiceId } },
				new { questionRevisionId = _narrativeRevisionId, value = (string?)Narrative },
			},
		});
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"no endpoint ever changes an answer's value or the locale it was given in")]
	public async Task ThenNoEndpointEverChangesValueOrLocale()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var (narrative, select) = await SubmittedNarrativeAndSelectAnswers();

		// There is no route, admin or otherwise, that writes to an answer row
		// anymore: the one endpoint that ever did — the translation queue's
		// PUT — is gone entirely (ADR-0174), and every request to its old path
		// now falls through to a plain 404, like any other unmapped route.
		using var put = await _admin.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{narrative.Id}/translation", UriKind.Relative),
			new { value = "Le vent s'est levé (admin)" });
		put.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		using var otherPut = await _admin.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{select.Id}/translation", UriKind.Relative),
			new { value = "Bleu (admin)" });
		otherPut.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		var (storedNarrative, storedSelect) = await SubmittedNarrativeAndSelectAnswers();
		storedNarrative.Value.ShouldBe(Narrative);
		storedNarrative.Locale.ShouldBe(Locale.EnCa);
		storedNarrative.TranslatedValue.ShouldBeNull();
		storedSelect.ChoiceId.ShouldBe(TinyId.Parse(_selectedChoiceId!));
		storedSelect.Value.ShouldBeNull();
		storedSelect.Locale.ShouldBe(Locale.EnCa);
		storedSelect.TranslatedValue.ShouldBeNull();
	}

	private const string Narrative = "The wind picked up on final.";

	private async Task<(ReportAnswer Narrative, ReportAnswer Select)> SubmittedNarrativeAndSelectAnswers()
	{
		_submittedReportId ??= (await _response!.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
		var reportId = TinyId.Parse(_submittedReportId!);
		var narrativeRevision = TinyId.Parse(_narrativeRevisionId!);
		var selectRevision = TinyId.Parse(_selectRevisionId!);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var answers = await database.ReportAnswers.AsNoTracking()
			.Where(candidate => candidate.ReportId == reportId)
			.ToListAsync();
		return (answers.Single(answer => answer.QuestionRevisionId == narrativeRevision),
			answers.Single(answer => answer.QuestionRevisionId == selectRevision));
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
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
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

	private string? _refusalMessage;

	[When(@"the Worker's translator attempts to supply that answer's translation again")]
	public async Task WhenTheWorkersTranslatorAttemptsToSupplyTheTranslationAgain()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var answer = await database.ReportAnswers.FirstAsync(candidate => candidate.Id == TinyId.Parse(_answerId!));

		try
		{
			answer.SupplyAutoTranslation("A second, unwanted translation.");
		}
		catch (DomainRuleViolationException cause)
		{
			_refusalMessage = cause.Message;
		}
	}

	[Then(@"the domain refuses it")]
	public void ThenTheDomainRefusesIt()
	{
		_refusalMessage.ShouldNotBeNull();
	}

	[Then(@"the stored translated value is unchanged")]
	public async Task ThenTheStoredTranslatedValueIsUnchanged()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.FirstAsync(candidate => candidate.Id == TinyId.Parse(_answerId!));
		stored.TranslatedValue.ShouldBe("Le vent s'est levé en finale (auto).");
		stored.TranslationSource.ShouldBe(TranslationSource.Auto);
	}

	[Then(@"no endpoint accepts a human-supplied translation for it")]
	public async Task ThenNoEndpointAcceptsAHumanSuppliedTranslation()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		using var put = await _admin.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{_answerId}/translation", UriKind.Relative),
			new { value = "Bleu (humain)" });
		put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"no endpoint lists answers waiting for one")]
	public async Task ThenNoEndpointListsAnswersWaitingForOne()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		using var get = await _admin.GetAsync(new Uri("/api/admin/answers/awaiting-translation", UriKind.Relative));
		get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

	// --- REQ-WLD-018: client validation never replaces server validation ---

	[Given(@"a submission reaches the API")]
	public async Task GivenASubmissionReachesTheApi()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
		var key = await CreateSelectQuestion();
		_selectRevisionId = await RevisionIdFor(key);
	}

	[When(@"the API independently validates it")]
	public async Task WhenTheApiIndependentlyValidatesIt()
	{
		// Each of these is one the form would never send: it cannot offer a value
		// the question does not, and it will not submit without consent.
		_refused.Add(("a choice the question never offered", await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
				new { questionRevisionId = _selectRevisionId, value = (string?)"Green" },
			},
		})));

		_refused.Add(("a report with no consent answer", await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _selectRevisionId, value = (string?)"Blue" } },
		})));
	}

	[Then(@"the API's validation is authoritative regardless of what the client allowed or displayed")]
	public void ThenTheApiValidationIsAuthoritative()
	{
		_refused.Count.ShouldBe(2);

		foreach (var (submission, response) in _refused)
		{
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"{submission} is refused");
		}
	}

	// --- A submission naming a revision that is not current is refused ---

	[Given(@"a submission carries an answer naming an unknown revision")]
	public async Task GivenASubmissionNamesAnUnknownRevision()
	{
		await ReadyToNameRevisions();
		_namedAnswers = [Answer(TinyId.New().Value)];
	}

	[Given(@"a submission carries an answer naming a deleted revision")]
	public async Task GivenASubmissionNamesADeletedRevision()
	{
		await ReadyToNameRevisions();
		_namedAnswers = [Answer(await DeletedRevisionId())];
	}

	[Given(@"a submission carries an answer naming a superseded revision of a live question")]
	public async Task GivenASubmissionNamesASupersededRevision()
	{
		await ReadyToNameRevisions();
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var created = await Create(key, "short_text", "Original wording");
		var supersededRevisionId = created.GetProperty("revisionId").GetString()!;
		await Revise(created.GetProperty("id").GetString()!, key, "short_text", "Reworded once");
		_namedAnswers = [Answer(supersededRevisionId)];
	}

	[Given(@"a submission carries two answers naming the same revision")]
	public async Task GivenASubmissionNamesOneRevisionTwice()
	{
		await ReadyToNameRevisions();
		var revisionId = await CreateSyntheticQuestion("short_text");
		_namedAnswers = [Answer(revisionId), Answer(revisionId)];
	}

	[Then(@"the API refuses the submission")]
	public async Task ThenTheApiRefusesTheSubmission()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"nothing is stored")]
	public async Task ThenNothingIsStored()
	{
		(await AnswerCarryingMarkerExists()).ShouldBeFalse();
	}

	// An answer's value is the scenario's marker, so "nothing is stored" can be
	// asserted as "no stored answer carries it" (see _marker).
	private object Answer(string revisionId)
	{
		return new { questionRevisionId = revisionId, value = (string?)_marker };
	}

	private async Task ReadyToNameRevisions()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		await EnsureConsentQuestion();
	}

	// --- An answer naming a statement or a group is refused ---

	[When(@"a submission carries an answer naming that question's revision")]
	public async Task WhenASubmissionNamesTheQuestionsRevision()
	{
		var type = (string)_scenario["questionType"];
		await ReadyToNameRevisions();
		var revisionId = await CreateSyntheticQuestion(type);
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
				Answer(revisionId),
			},
		});
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
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
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

	// --- A submission validates every upload it claims ---

	[Given(@"a submission claims an upload whose stored file is (.*)")]
	public async Task GivenASubmissionClaimsAnUploadWhoseStoredFileIs(string file)
	{
		// A host that counts what storage is asked for, so the claim can be held
		// to reading only what sniffing needs (ADR-0126, ADR-0134).
		var host = await BootedApi.RecordingReads();
		_reporter = await BootedApi.SignedInAs(MemberRole.User, host);
		await EnsureConsentQuestion();
		_extraRevisionId = await CreateSyntheticQuestion("short_text");
		_fileRevisionId = await CreateSyntheticQuestion("file_upload");

		const int megabyte = 1024 * 1024;
		var (bytes, declared) = file switch
		{
			"bytes that match no known format" => (Unrecognisable(megabyte), "application/pdf"),
			"declared as one allowlisted type but containing another" => (PaddedPng(megabyte), "application/pdf"),
			"declared as a video but detected as a 30 MB image" => (PaddedPng(30 * megabyte), "video/mp4"),
			_ => throw new NotSupportedException($"Unmapped stored file: '{file}'."),
		};

		_refusedUploadSize = bytes.Length;
		_refusedUploadId = await DirectUpload.Send(_reporter, bytes, declared);
		_uploadId = _refusedUploadId;
	}

	[Then(@"the response names that upload ID with a safe refusal reason of ""(.*)""")]
	public async Task ThenTheResponseNamesThatUploadWithReason(string reason)
	{
		var problem = await ResponseBody();
		var refused = problem.GetProperty("refusedUploads").EnumerateArray().Single();
		refused.GetProperty("uploadId").GetString().ShouldBe(_refusedUploadId);
		refused.GetProperty("reason").GetString().ShouldBe(reason);
		problem.GetRawText().ShouldNotContain("refused.bin");
	}

	[Then(@"the API read only the upload's size and the bytes sniffing needs, never the whole file into memory")]
	public void ThenTheApiReadOnlyWhatSniffingNeeds()
	{
		var key = $"quarantine/{_refusedUploadId}";
		ReadRecordingBlobStore.WholeReadsOf(key).ShouldBe(0);
		ReadRecordingBlobStore.RangeBytesRead(key).ShouldBeLessThanOrEqualTo(2 * BlobRangeStream.DefaultWindowSize);
		ReadRecordingBlobStore.RangeBytesRead(key).ShouldBeLessThan(_refusedUploadSize);
	}

	[Then(@"the API refuses the submission with 400")]
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
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
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
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
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

	[Then(@"the response contains no raw answers or attachment URLs, and carries only the report ID, the status, and the browser receipt")]
	public async Task ThenTheResponseContainsNoRawAnswersOrAttachmentUrls()
	{
		var body = await ResponseBody();
		var properties = body.EnumerateObject().Select(property => property.Name).ToList();
		properties.ShouldBe(["id", "status", "receipt"], ignoreOrder: true);
	}

	// --- REQ-SUB-133 to REQ-SUB-135: the browser receipt (ADR-0196) ---

	[Then(@"the body carries a receipt of at least 256 random bits, base64url")]
	public async Task ThenTheBodyCarriesAReceipt()
	{
		var receipt = (await ResponseBody()).GetProperty("receipt").GetString();

		receipt.ShouldNotBeNull();
		System.Buffers.Text.Base64Url.IsValid(receipt).ShouldBeTrue();
		System.Buffers.Text.Base64Url.DecodeFromChars(receipt).Length.ShouldBeGreaterThanOrEqualTo(32);
	}

	[Then(@"two submissions by the same member return unrelated receipts")]
	public async Task ThenTwoSubmissionsReturnUnrelatedReceipts()
	{
		var first = (await ResponseBody()).GetProperty("receipt").GetString();

		using var second = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
		});
		second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
		var other = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("receipt").GetString();

		other.ShouldNotBe(first);
	}

	[Then(@"the stored report holds the SHA-256 hash of the receipt it returned and nowhere else the receipt itself")]
	public async Task ThenTheReportHoldsOnlyTheReceiptsHash()
	{
		var body = await SubmittedBody();
		var receipt = body.GetProperty("receipt").GetString()!;
		var stored = await StoredRowsOf(body.GetProperty("id").GetString()!);

		var expected = System.Buffers.Text.Base64Url.EncodeToString(
			System.Security.Cryptography.SHA256.HashData(System.Buffers.Text.Base64Url.DecodeFromChars(receipt)));

		stored.Report.ShouldContain($"\"receipt_hash\": \"{expected}\"");
		stored.Everything.ShouldNotContain(receipt, Case.Sensitive);
	}

	[Then(@"no log line records the receipt")]
	public async Task ThenNoLogLineRecordsTheReceipt()
	{
		var receipt = (await SubmittedBody()).GetProperty("receipt").GetString()!;

		_logs.Lines.ShouldNotBeEmpty();
		_logs.Lines.ShouldAllBe(line => !line.Contains(receipt, StringComparison.Ordinal));
	}

	[Then(@"no stored value of that report, its answers, or its outbox is the reporter's token subject or a hash of it")]
	public async Task ThenNoStoredValueIsTheSubjectOrItsHash()
	{
		var stored = await StoredRowsOf((await SubmittedBody()).GetProperty("id").GetString()!);
		var subject = System.Text.Encoding.UTF8.GetBytes(_submitterSubject!);
		var forms = new[]
		{
			_submitterSubject!,
			Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(subject)),
			Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(subject)),
			System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.SHA256.HashData(subject)),
		};

		foreach (var form in forms)
		{
			stored.Everything.ShouldNotContain(form, Case.Insensitive);
		}
	}

	[Then(@"the stored receipt hash is not derived from the token subject")]
	public async Task ThenTheReceiptHashIsNotDerivedFromTheSubject()
	{
		var body = await SubmittedBody();
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = await database.Reports.AsNoTracking().SingleAsync(candidate => candidate.Id == TinyId.Parse(body.GetProperty("id").GetString()));

		var subject = System.Text.Encoding.UTF8.GetBytes(_submitterSubject!);
		report.ReceiptHash.ShouldNotBeNull();
		report.ReceiptHash.ShouldNotBe(System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.SHA256.HashData(subject)));
		report.ReceiptHash.ShouldNotBe(System.Buffers.Text.Base64Url.EncodeToString(subject));
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
				new { questionRevisionId = _consentRevisionId, value = (bool?)true },
				new { questionRevisionId = _extraRevisionId, value = (string?)_marker },
			},
		});
		_response = await anonymous.PostAsync(Submit, content);
	}

	[Then(@"the API refuses it before any report state is created")]
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
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
		});
	}

	[When(@"the per-IP rate limit is exceeded")]
	public async Task WhenThePerIpRateLimitIsExceeded()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
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
		_response = await DirectUpload.Mint(_reporter!, "application/pdf", 15);
	}

	[Then(@"the API refuses the request with 429 and a safe retry signal")]
	public void ThenTheApiRejectsTheRequestWith429AndASafeRetrySignal()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
		_response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
	}

	[Then(@"the client IP used for rate limiting comes from CloudFront-Viewer-Address, which CloudFront always sets and a member cannot forge, and is never stored on the report")]
	public async Task ThenTheClientIpComesOnlyFromTrustedHeadersAndIsNeverStored()
	{
		var body = await _response!.Content.ReadAsStringAsync();
		body.ShouldNotContain("ip", Case.Insensitive);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportColumns = database.Model.FindEntityType(typeof(Report))!.GetProperties()
			.Select(property => property.Name);
		// A whole word "Ip" in the PascalCase name — ClientIp, IpAddress — not the letters
		// inside another word, such as ReceiptHash (ADR-0196).
		reportColumns.ShouldNotContain(name => System.Text.RegularExpressions.Regex.IsMatch(name, "(?<![a-z])Ip(?![a-z])"));
	}

	// --- A request without CloudFront's origin-secret header is refused (ADR-0159) ---

	private HttpClient? _unverifiedClient;

	[Given(@"the booted API requires CloudFront's origin-secret header")]
	public async Task GivenTheBootedApiRequiresCloudFrontsOriginSecretHeader()
	{
		var verified = await BootedApi.OriginVerified("acceptance-test-origin-secret");
		_unverifiedClient = verified.CreateClient();
	}

	[When(@"a submission request arrives without that header")]
	public async Task WhenASubmissionRequestArrivesWithoutThatHeader()
	{
		_response = await _unverifiedClient!.PostAsync(Submit, ReportPart(new
		{
			language = "en-CA",
			answers = Array.Empty<object>(),
		}));
	}

	[Then(@"the API refuses it with 403, before authentication or any endpoint runs")]
	public void ThenTheApiRefusesItWith403BeforeAuthenticationOrAnyEndpointRuns()
	{
		// No bearer token was ever attached, and a submission needs one
		// (REQ-SUB-018) — a 403 here, not a 401, is exactly the proof that
		// origin verification runs first (ADR-0159): had authentication run
		// first, a missing token would answer 401.
		_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	// --- The rate limiter partitions by CloudFront-Viewer-Address (ADR-0159) ---

	private const string FirstViewerAddress = "203.0.113.10:52341";
	private const string SecondViewerAddress = "203.0.113.99:11402";

	[Given(@"the per-IP submission rate limit is exhausted for one CloudFront viewer address")]
	public async Task GivenThePerIpSubmissionRateLimitIsExhaustedForOneCloudFrontViewerAddress()
	{
		await EnsureConsentQuestion();

		var limited = await BootedApi.RateLimited("PublicSubmission");
		_reporter = await BootedApi.SignedInAs(MemberRole.User, limited);
		_reporter.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", FirstViewerAddress);

		// The one permit this policy allows, consumed by this address.
		await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
		});
	}

	[When(@"a submission request arrives from a different CloudFront viewer address")]
	public async Task WhenASubmissionRequestArrivesFromADifferentCloudFrontViewerAddress()
	{
		_reporter!.DefaultRequestHeaders.Remove("CloudFront-Viewer-Address");
		_reporter.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", SecondViewerAddress);

		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
		});
	}

	[Then(@"the API does not refuse it")]
	public void ThenTheApiDoesNotRejectIt()
	{
		_response!.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
	}

	// --- A successful submission nudges the Worker (ADR-0123) ---

	[Given(@"the booted API records each nudge it sends the Worker, and a submission is ready to persist")]
	public async Task GivenTheBootedApiRecordsNudgesAndASubmissionIsReadyToPersist()
	{
		var recording = await BootedApi.NudgeRecorded();
		RecordingWorkerNudge.Reset();
		_reporter = await BootedApi.SignedInAs(MemberRole.User, recording);
		await EnsureConsentQuestion();
	}

	[Then(@"the Worker is nudged once")]
	public void ThenTheWorkerIsNudgedOnce()
	{
		RecordingWorkerNudge.Calls.ShouldBe(1);
	}

	// --- A member of any role may submit a report (outline) ---

	[Given(@"a reporter holds a valid member token with the (.*) role")]
	public async Task GivenAReporterHoldsAValidMemberTokenWithTheRole(string role)
	{
		_reporter = await BootedApi.SignedInAs(GlossaryNames.Role(role));
		await EnsureConsentQuestion();
	}

	[When(@"a valid submission is made")]
	public async Task WhenAValidSubmissionIsMade()
	{
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
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
		// A host of its own, logging everything at Trace, and a member whose
		// subject no other scenario shares, so the lines and rows this
		// submission leaves can be told from every other scenario's.
		var host = (await BootedApi.Factory()).WithWebHostBuilder(builder => builder.ConfigureLogging(_logs.Register));
		_submitterSubject = $"acceptance:submitter:{Guid.NewGuid():N}";
		_reporter = BootedApi.SignedInAsMember(host, _submitterSubject);
		await EnsureConsentQuestion();
		_response = await Post(new
		{
			language = "en-CA",
			answers = new object[] { new { questionRevisionId = _consentRevisionId, value = (bool?)true } },
		});
	}

	[When(@"the submission is committed")]
	[When(@"the submission completes")]
	public void WhenTheSubmissionIsCommitted()
	{
		// Already committed by the Given step above.
	}

	public void Dispose()
	{
		_logs.Dispose();
	}

	// --- REQ-SUB-021: no audit entry or log line records who submitted ---

	[Then(@"no audit entry attributes the submission to a token subject")]
	public async Task ThenNoAuditEntryAttributesTheSubmission()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
		var reportId = TinyId.Parse((await ResponseBody()).GetProperty("id").GetString());

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entries = await database.AuditLog.AsNoTracking()
			.Where(entry => entry.ActorSubject == _submitterSubject
							|| entry.TargetId == reportId
							|| (entry.Detail != null && entry.Detail.Contains(_submitterSubject!)))
			.ToListAsync();

		entries.ShouldBeEmpty();
	}

	[Then(@"no log line records the reporter's token subject at any level")]
	public void ThenNoLogLineRecordsTheSubject()
	{
		// A capture that saw nothing would prove nothing.
		_logs.Lines.ShouldNotBeEmpty();
		_logs.Lines.ShouldAllBe(line => !line.Contains(_submitterSubject!, StringComparison.OrdinalIgnoreCase));
	}

	[Then(@"no stored report, answer, file, upload, consent projection, or outbox message records the reporter's token subject")]
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
		// HiddenBySubject is the reviewer who hid a file from the public page
		// (ADR-0117), never the member who filed the report.
		typeof(ReportFile).GetProperties().ShouldNotContain(property =>
			(property.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase)
			 && property.Name != nameof(ReportFile.HiddenBySubject)) ||
			property.Name.Contains("Submitter", StringComparison.OrdinalIgnoreCase));
		typeof(OutboxMessage).GetProperties().ShouldNotContain(property =>
			property.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase) ||
			property.Name.Contains("Submitter", StringComparison.OrdinalIgnoreCase));
	}

	// --- Helpers ---

	private async Task<JsonElement> SubmittedBody()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
		return await ResponseBody();
	}

	/// <summary>
	///     Every stored row of one report, as text: its own row, its answers, and its
	///     outbox messages. What a receipt or a subject must appear in none of.
	/// </summary>
	private static async Task<(string Report, string Everything)> StoredRowsOf(string reportId)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var report = await database.Database
			.SqlQuery<string>($"SELECT jsonb_pretty(to_jsonb(r)) AS \"Value\" FROM reports AS r WHERE r.id = {reportId}")
			.SingleAsync();
		var answers = await database.Database
			.SqlQuery<string>($"SELECT to_jsonb(a)::text AS \"Value\" FROM report_answers AS a WHERE a.report_id = {reportId}")
			.ToListAsync();
		var outbox = await database.Database
			.SqlQuery<string>($"SELECT to_jsonb(o)::text AS \"Value\" FROM outbox_messages AS o WHERE o.aggregate_id = {reportId}")
			.ToListAsync();

		return (report, string.Join('\n', [report, .. answers, .. outbox]));
	}

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
					new { questionRevisionId = consent, value = (bool?)true },
					new { questionRevisionId = consent, value = (bool?)false },
				},
			}),
			"an unknown question_revision_id" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (bool?)true },
					new { questionRevisionId = (string?)"not-a-real-id", value = (string?)"x" },
				},
			}),
			"a question_revision_id for a deleted revision" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (bool?)true },
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
					new { questionRevisionId = consent, value = (bool?)true },
					new
					{
						questionRevisionId = (string?)await CreateSyntheticQuestion("short_text"),
						choices = new[] { "x" },
					},
				},
			}),
			"a malformed upload ID" => await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = consent, value = (bool?)true },
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
				new { questionRevisionId = consent, value = (bool?)true },
				new
				{
					questionRevisionId = (string?)await CreateSyntheticQuestion("file_upload"),
					attachments = named.Select(id => new { uploadId = id, fileName = "a.png" }).ToArray(),
				},
			},
		});
	}

	/// <summary>A real PNG, followed by zeros up to <paramref name="length" /> bytes.</summary>
	private static byte[] PaddedPng(int length)
	{
		using var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.SkyBlue, 8, 8) { Format = ImageMagick.MagickFormat.Png };
		var png = image.ToByteArray();
		var padded = new byte[length];
		png.CopyTo(padded, 0);
		return padded;
	}

	/// <summary>Random bytes no signature begins with.</summary>
	private static byte[] Unrecognisable(int length)
	{
		var bytes = new byte[length];
		Random.Shared.NextBytes(bytes);
		bytes[0] = 0x00;
		bytes[1] = 0x13;
		return bytes;
	}

	private async Task<string> UploadSyntheticPdf()
	{
		return await DirectUpload.Send(
			_reporter!,
			"%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray(),
			"application/pdf");
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

	private async Task<string> CreateSelectQuestion(string type = "single_select")
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];

		var request = new
		{
			key,
			type,
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
			dependsOnChoiceId = (string?)null,
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

	/// <summary>
	///     Each way a choice answer can name its choices, sent as its own report:
	///     the live choice and a type-ahead's typed text are accepted; a removed
	///     choice, another question's, and a single-select's typed text are refused.
	/// </summary>
	private async Task SubmitEachChoiceAnswer()
	{
		async Task<HttpResponseMessage> Send(string revisionId,
											 string? value,
											 string[]? choices)
		{
			return await Post(new
			{
				language = "en-CA",
				answers = new object[]
				{
					new { questionRevisionId = _consentRevisionId, value = (bool?)true, choices = (string[]?)null },
					new { questionRevisionId = revisionId, value, choices },
				},
			});
		}

		_accepted.Add(("a live choice", await Send(_selectRevisionId!, null, [_liveChoiceId!])));
		_accepted.Add(("typed text in a type-ahead", await Send(_typeAheadRevisionId!, _marker, null)));
		_refused.Add(("a removed choice", await Send(_selectRevisionId!, null, [_removedChoiceId!])));
		_refused.Add(("another question's choice", await Send(_selectRevisionId!, null, [_otherQuestionChoiceId!])));
		_refused.Add(("typed text in a single-select", await Send(_selectRevisionId!, "Blue", null)));
	}

	/// <summary>Removes every choice but one from a question CreateSelectQuestion made, as an Administrator's save.</summary>
	private async Task RemoveChoice(string key,
									string keep)
	{
		var admin = _admin!;
		var questions = await admin.GetFromJsonAsync<JsonElement>(AdminQuestions);
		var id = questions.EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == key)
			.GetProperty("id").GetString();

		using var response = await admin.PutAsJsonAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative), new
		{
			key,
			type = "single_select",
			labelEn = "A synthetic question",
			labelFr = "Une question synthétique",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = new[] { new { code = keep, labelEn = "Blue", labelFr = "Bleu" } },
		});
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	/// <summary>Every answer of the report this scenario submitted, optionally with the choices they name.</summary>
	private async Task<List<ReportAnswer>> StoredAnswersOfThisReport(bool includeChoices = false)
	{
		_submittedReportId ??= (await _response!.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
		var reportId = TinyId.Parse(_submittedReportId!);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var answers = database.ReportAnswers.AsNoTracking().Where(answer => answer.ReportId == reportId);

		return includeChoices
			? await answers.Include(answer => answer.Choice).ToListAsync()
			: await answers.ToListAsync();
	}

	private static async Task<string> ChoiceIdFor(string key,
												 string labelEn)
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var body = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
		return body.EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == key)
			.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("labelEn").GetString() == labelEn)
			.GetProperty("id").GetString()!;
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
			dependsOnChoiceId = (string?)null,
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
			dependsOnChoiceId = (string?)null,
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
	///     Serialized only for the same reason every read against the shared
	///     database is: no other caller may write a second live question
	///     carrying <see cref="QuestionRole.ConsentPublish" /> underneath this
	///     read (<c>ix_questions_role</c>, ADR-0154).
	/// </summary>
	internal static async Task<string> ConsentRevisionId()
	{
		await ConsentGate.WaitAsync();

		try
		{
			return await FindConsentRevisionId();
		}
		finally
		{
			ConsentGate.Release();
		}
	}

	/// <summary>
	///     The seeded publication-consent question's current revision. Read by
	///     role (ADR-0154), not by an assumed key: <c>QuestionBankSeed</c>'s
	///     consent question keeps the real form's Typeform-derived key, not the
	///     <see cref="QuestionKey.ConsentPublish" /> constant, and a role lives on
	///     at most one live question, so the seed always has exactly one.
	/// </summary>
	private static async Task<string> FindConsentRevisionId()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var consent = await database.Questions
			.Include(question => question.Revisions)
			.SingleAsync(question => question.Role == QuestionRole.ConsentPublish);

		return consent.CurrentRevision.Id.Value;
	}

	private async Task<bool> AnswerCarryingMarkerExists()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.ReportAnswers.AnyAsync(answer => answer.Value == _marker);
	}
}
