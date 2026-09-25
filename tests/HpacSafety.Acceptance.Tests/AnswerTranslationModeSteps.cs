using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Which answers get a second language, and where it comes from (ADR-0112):
///     REQ-SUB-069, REQ-SUB-070, REQ-SUB-071, REQ-MOD-077, and REQ-QB-108..110.
/// </summary>
/// <remarks>
///     <para>
///         The submission and the admin report view run through the booted API. The
///         Worker is a separate deployable, so its translation step runs here as the
///         real <see cref="TranslateAnswersProcessor" /> against the booted database,
///         with a translator that records what it was sent instead of calling a
///         provider.
///     </para>
///     <para>Every question, answer, and report here is synthetic.</para>
/// </remarks>
[Binding]
public sealed class AnswerTranslationModeSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

	// Each answer this scenario submits, by a name the steps use, with the
	// revision it answers and the value (or values) given.
	private readonly Dictionary<string, (string RevisionId, string? Value, string[]? Values)> _answers = [];
	private readonly RecordingTranslator _translator = new();

	private HttpClient? _admin;
	private string? _reportId;
	private JsonElement _detail;

	// Domain-level state for the question-bank claims.
	private Question? _question;
	private QuestionRevision? _revision;
	private Action? _attempt;
	private Question? _live;

	// ── REQ-SUB-071: only free text marked for translation is sent ──────────

	[Given(@"a submitted report answers a long-text question marked for translation")]
	public async Task GivenALongTextAnswerMarkedForTranslation()
	{
		_answers["long_text"] = (await CreateQuestion("long_text", isTranslatable: true), "The wind picked up on final.", null);
	}

	[Given(@"it answers a short-text question not marked for translation")]
	public async Task GivenAShortTextAnswerNotMarked()
	{
		_answers["short_text"] = (await CreateQuestion("short_text"), "Avery", null);
	}

	[Given(@"^it answers an email, a phone number, a date, a time, a number, and a yes/no question$")]
	public async Task GivenTheInvariantTypes()
	{
		_answers["email"] = (await CreateQuestion("email"), "avery@example.test", null);
		_answers["phone"] = (await CreateQuestion("phone"), "555-0100", null);
		_answers["date"] = (await CreateQuestion("date"), "2026-09-21", null);
		_answers["time"] = (await CreateQuestion("time"), "14:30", null);
		_answers["number"] = (await CreateQuestion("number"), "3", null);
		_answers["yes_no"] = (await CreateQuestion("yes_no"), "yes", null);
	}

	[When(@"the Worker translates that report's answers")]
	public async Task WhenTheWorkerTranslatesThatReportsAnswers()
	{
		await SubmitInEnglish();
		await RunTheWorker();
	}

	[Then(@"only the long-text answer is sent to the translator")]
	public void ThenOnlyTheLongTextAnswerIsSent()
	{
		_translator.Sent.ShouldBe(["The wind picked up on final."]);
	}

	[Then(@"^the yes/no answer has only its fixed counterpart, written at submission$")]
	public async Task ThenTheYesNoAnswerHasItsFixedCounterpart()
	{
		var answer = (await StoredAnswers()).Single(answer => answer.QuestionRevisionId == TinyId.Parse(_answers["yes_no"].RevisionId));

		answer.TranslatedValue.ShouldBe("oui");
		answer.TranslationMode.ShouldBe(TranslationMode.Fixed);
		answer.TranslationSource.ShouldBe(TranslationSource.Fixed);
	}

	[Then(@"every other answer keeps no second language")]
	public async Task ThenEveryOtherAnswerKeepsNoSecondLanguage()
	{
		var stored = await StoredAnswers();

		foreach (var answer in stored.Where(answer => answer.QuestionRevisionId != TinyId.Parse(_answers["long_text"].RevisionId)
													  && answer.QuestionRevisionId != TinyId.Parse(_answers["yes_no"].RevisionId)
													  && answer.QuestionKey != QuestionKey.ConsentPublish))
		{
			answer.TranslatedValue.ShouldBeNull(answer.QuestionKey);
			answer.TranslationMode.ShouldBe(TranslationMode.None, answer.QuestionKey);
		}
	}

	// ── REQ-SUB-069: a picker takes its choice's other label ────────────────

	[Given(@"a single-select and a multi-select question offer choices written in both official languages")]
	public async Task GivenPickersWithBilingualChoices()
	{
		_answers["single_select"] = (await CreateQuestion("single_select", options: [("Paraglider", "Parapente"), ("Hang glider", "Aile delta")]), "Paraglider", null);
		_answers["multi_select"] = (await CreateQuestion("multi_select", options: [("P3", "P3"), ("Tandem instructor", "Instructeur tandem")]), null, ["Tandem instructor"]);
	}

	[When(@"a reporter answering in English picks one choice from each and submits")]
	public async Task WhenAReporterPicksOneChoiceFromEach()
	{
		await SubmitInEnglish();
		await RunTheWorker();
	}

	[Then(@"each stored answer holds the French label of the choice picked, as its second language")]
	public async Task ThenEachStoredAnswerHoldsTheFrenchLabel()
	{
		(await StoredFor("single_select")).Single().TranslatedValue.ShouldBe("Parapente");
		(await StoredFor("multi_select")).Single().TranslatedValue.ShouldBe("Instructeur tandem");
	}

	[Then(@"the translation source is marked ""choice""")]
	public async Task ThenTheTranslationSourceIsMarkedChoice()
	{
		(await StoredFor("single_select")).Concat(await StoredFor("multi_select"))
			.ShouldAllBe(answer => answer.TranslationSource == TranslationSource.Choice);
	}

	[Then(@"no translation provider is called and nothing is sent to the Worker's translator")]
	public void ThenNothingIsSentToTheTranslator()
	{
		// The submission path has no translator to call at all (REQ-SUB-007);
		// the Worker ran and found nothing to send.
		_translator.Sent.ShouldBeEmpty();
	}

	// ── REQ-SUB-070: a type-ahead uses its choice when it names one ─────────

	[Given(@"a type-ahead question offers a choice written in both official languages")]
	public async Task GivenATypeAheadWithABilingualChoice()
	{
		_answers["autocomplete"] = (await CreateQuestion("autocomplete", options: [("Mount Seven", "Mont Sept")]), null, null);
	}

	[When(@"^a reporter answering in English submits (that choice's English label|words the question does not offer)$")]
	public async Task WhenAReporterSubmitsATypeAheadValue(string answer)
	{
		var value = answer == "that choice's English label" ? "Mount Seven" : "A ridge nobody listed";
		_answers["autocomplete"] = (_answers["autocomplete"].RevisionId, value, null);

		await SubmitInEnglish();
		await RunTheWorker();
	}

	[Then(@"^the answer's second language comes from (the choice's French label, at submission|the Worker's machine translation)$")]
	public async Task ThenTheSecondLanguageComesFrom(string source)
	{
		ArgumentNullException.ThrowIfNull(source);
		var stored = (await StoredFor("autocomplete")).Single();

		if (source.StartsWith("the choice's", StringComparison.Ordinal))
		{
			stored.TranslatedValue.ShouldBe("Mont Sept");
			stored.TranslationSource.ShouldBe(TranslationSource.Choice);
			_translator.Sent.ShouldBeEmpty();
		}
		else
		{
			stored.TranslatedValue.ShouldBe("[fr-CA] A ridge nobody listed");
			stored.TranslationSource.ShouldBe(TranslationSource.Auto);
		}
	}

	// ── REQ-MOD-077: the detail view hides a second language that isn't one ─

	[Given(@"a submitted report answered a first name, an email, a date, a picker, and a narrative marked for translation")]
	public async Task GivenAReportForTheDetailView()
	{
		_answers["first_name"] = (await CreateQuestion("short_text"), "Avery", null);
		_answers["email"] = (await CreateQuestion("email"), "avery@example.test", null);
		_answers["date"] = (await CreateQuestion("date"), "2026-09-21", null);
		_answers["single_select"] = (await CreateQuestion("single_select", options: [("Paraglider", "Parapente")]), "Paraglider", null);
		_answers["long_text"] = (await CreateQuestion("long_text", isTranslatable: true), "The wind picked up on final.", null);

		await SubmitInEnglish();

		// A row written before ADR-0112, when every answer was machine-translated:
		// its copy is still in the database, and must still not be shown.
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var email = TinyId.Parse(_answers["email"].RevisionId);
		await database.Database.ExecuteSqlAsync(
			$"UPDATE report_answers SET translated_value = 'avery@example.test', translation_source = 'auto' WHERE question_revision_id = {email.Value}");
	}

	[Given(@"the Worker has translated the narrative")]
	public async Task GivenTheWorkerHasTranslatedTheNarrative()
	{
		await RunTheWorker();
	}

	[When(@"a reviewer opens the report's detail view")]
	public async Task WhenAReviewerOpensTheDetailView()
	{
		using var reviewer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_detail = await reviewer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
	}

	[Then(@"the picker and the narrative each carry their second language")]
	public async Task ThenThePickerAndNarrativeCarryTheirSecondLanguage()
	{
		(await TranslationShownFor("single_select")).ShouldBe("Parapente");
		(await TranslationShownFor("long_text")).ShouldBe("[fr-CA] The wind picked up on final.");
	}

	[Then(@"the first name, the email, and the date carry none, even if one was stored before this rule")]
	public async Task ThenTheInvariantAnswersCarryNone()
	{
		(await TranslationShownFor("first_name")).ShouldBeNull();
		(await TranslationShownFor("email")).ShouldBeNull();
		(await TranslationShownFor("date")).ShouldBeNull();
	}

	// ── REQ-QB-108: only free text can need translation, by default ─────────

	[Given(@"^an Administrator authors an? (\w+) question without saying whether it needs translation$")]
	public void GivenAQuestionAuthoredWithoutTheFlag(string type)
	{
		ArgumentNullException.ThrowIfNull(type);
		EnumCode.TryParse<QuestionType>(type, out var parsed).ShouldBeTrue(type);
		_question = Question.Create("synthetic", parsed, "A question", "Une question", Noon);
		_revision = _question.CurrentRevision;
	}

	[Then(@"^the new revision records that its answers (need|do not need) translation$")]
	public void ThenTheRevisionRecordsTheDefault(string need)
	{
		_revision!.IsTranslatable.ShouldBe(need == "need");
	}

	// ── REQ-QB-109: marking a non-text question is rejected ─────────────────

	[Given(@"^an Administrator authors an email, date, yes/no, or select question$")]
	public void GivenANonTextQuestion()
	{
		// Asserted for each in turn in the Then.
	}

	[When(@"they mark it as needing translation")]
	public void WhenTheyMarkItAsNeedingTranslation()
	{
		_attempt = () =>
		{
			foreach (var type in new[] { QuestionType.Email, QuestionType.Date, QuestionType.YesNo, QuestionType.SingleSelect })
			{
				Should.Throw<DomainRuleViolationException>(
					() => Question.Create("synthetic", type, "A question", "Une question", Noon, isTranslatable: true),
					type.ToString());
			}
		};
	}

	[Then(@"saving that question is rejected")]
	public void ThenSavingIsRejected()
	{
		_attempt!.Invoke();
	}

	// ── REQ-QB-110: the flag is a revision field ────────────────────────────

	[Given(@"a short-text question that does not need translation")]
	public void GivenAShortTextQuestionNotNeedingTranslation()
	{
		_question = Question.Create("synthetic", QuestionType.ShortText, "What went wrong?", "Qu'est-ce qui n'a pas fonctionné?", Noon, isActive: true);
		_question.CurrentRevision.IsTranslatable.ShouldBeFalse();
	}

	[When(@"an Administrator marks it as needing translation while nobody has answered it")]
	public void WhenMarkedWhileUnanswered()
	{
		_live = Edit(_question!, hasBeenAnswered: false, isTranslatable: true);
	}

	[Then(@"a new revision records that it needs translation")]
	public void ThenANewRevisionRecordsIt()
	{
		_live.ShouldBeSameAs(_question);
		_question!.CurrentRevision.RevisionNumber.ShouldBe(2);
		_question.CurrentRevision.IsTranslatable.ShouldBeTrue();
		_question.Revisions.Single(revision => revision.RevisionNumber == 1).IsTranslatable.ShouldBeFalse();
	}

	[When(@"an Administrator changes it back after it has been answered")]
	public void WhenChangedBackAfterAnswered()
	{
		_live = Edit(_question!, hasBeenAnswered: true, isTranslatable: false);
	}

	[Then(@"the question is retired and replaced, so each answer keeps the setting it was given under")]
	public void ThenRetiredAndReplaced()
	{
		_live.ShouldNotBeSameAs(_question);
		_question!.Deleted.ShouldNotBeNull();
		_question.CurrentRevision.IsTranslatable.ShouldBeTrue();
		_live!.Key.ShouldBe(_question.Key);
		_live.CurrentRevision.IsTranslatable.ShouldBeFalse();
	}

	// ── helpers ─────────────────────────────────────────────────────────────

	private static Question Edit(Question question,
								 bool hasBeenAnswered,
								 bool isTranslatable)
	{
		var current = question.CurrentRevision;

		return question.ApplyEdit(
			hasBeenAnswered, current.Type, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Noon.AddHours(1), isTranslatable: isTranslatable);
	}

	private async Task<string> CreateQuestion(string type,
											  bool? isTranslatable = null,
											  (string En, string Fr)[]? options = null)
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);

		using var response = await _admin.PostAsJsonAsync(AdminQuestions, new
		{
			key = $"synthetic_{Guid.NewGuid():N}"[..40],
			type,
			labelEn = "A synthetic question",
			labelFr = "Une question synthétique",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			isTranslatable,
			options = (options ?? []).Select(option => new { code = (string?)null, labelEn = option.En, labelFr = option.Fr }).ToArray(),
		});

		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		var created = await response.Content.ReadFromJsonAsync<JsonElement>();
		return created.GetProperty("revisionId").GetString()!;
	}

	private async Task SubmitInEnglish()
	{
		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		var consent = await ReportSubmissionEndpointSteps.ConsentRevisionId();

		var answers = new List<object> { new { questionRevisionId = consent, value = (string?)"yes", choices = (string[]?)null } };
		answers.AddRange(_answers.Values.Select(answer => new
		{
			questionRevisionId = answer.RevisionId,
			value = answer.Values is null ? answer.Value : null,
			choices = answer.Values,
		}));

		using var response = await reporter.PostAsJsonAsync(Submit, new { language = "en-CA", answers });
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		_reportId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
	}

	private async Task RunTheWorker()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var processor = new TranslateAnswersProcessor(database, _translator);
		await processor.Process(
			new OutboxMessage(TinyId.Parse(_reportId!), OutboxMessageType.TranslateAnswers, _reportId!, Noon),
			CancellationToken.None);
		await database.SaveChangesAsync();
	}

	private async Task<List<ReportAnswer>> StoredAnswers()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId!);

		return await database.ReportAnswers.AsNoTracking().Where(answer => answer.ReportId == reportId).ToListAsync();
	}

	private async Task<List<ReportAnswer>> StoredFor(string name)
	{
		var revisionId = TinyId.Parse(_answers[name].RevisionId);
		return [.. (await StoredAnswers()).Where(answer => answer.QuestionRevisionId == revisionId)];
	}

	private async Task<string?> TranslationShownFor(string name)
	{
		var key = (await StoredFor(name)).First().QuestionKey;
		var entry = _detail.GetProperty("answers").EnumerateArray()
			.Single(answer => answer.GetProperty("questionKey").GetString() == key);
		var shown = entry.GetProperty("values").EnumerateArray().Single().GetProperty("translatedValue");

		return shown.ValueKind == JsonValueKind.Null ? null : shown.GetString();
	}

	/// <summary>A translator that records what it was sent and answers without a provider.</summary>
	private sealed class RecordingTranslator : ITranslator
	{
		public List<string> Sent { get; } = [];

		public bool IsConfigured => true;

		public Task<IReadOnlyList<string>> Translate(
			IReadOnlyList<string> texts,
			Locale source,
			Locale target,
			CancellationToken cancellationToken)
		{
			Sent.AddRange(texts);
			return Task.FromResult<IReadOnlyList<string>>([.. texts.Select(text => $"[{target.Code}] {text}")]);
		}
	}
}
