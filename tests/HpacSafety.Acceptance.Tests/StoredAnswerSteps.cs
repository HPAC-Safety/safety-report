using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     What a stored answer holds — REQ-QB-122, a choice answer names its choice
///     (ADR-0128); REQ-QB-019 and REQ-QB-118, every answer in its one invariant
///     written form or refused; and REQ-QB-025, only consent is projected onto the
///     report (ADR-0072, ADR-0095, ADR-0117, ADR-0119).
/// </summary>
/// <remarks>
///     REQ-QB-122, REQ-QB-019, and REQ-QB-118 go through the booted API, because
///     each is a claim about what the submission endpoint persists. REQ-QB-025 is a rule
///     of the <see cref="Report" /> aggregate and runs against it directly. Every
///     question, choice, and answer here is synthetic.
/// </remarks>
[Binding]
public sealed class StoredAnswerSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly DateTimeOffset Noon = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	// Codes and labels that differ in both languages, so a stored code could
	// never be mistaken for a stored label.
	private static readonly object[] Colours =
	[
		new { code = "blue", labelEn = "Blue", labelFr = "Bleu" },
		new { code = "red", labelEn = "Red", labelFr = "Rouge" },
	];

	// Parallel scenarios derive keys from wording; this keeps each one's own.
	private readonly string _run = Guid.NewGuid().ToString("N");
	private readonly Dictionary<string, JsonElement> _pickers = [];
	private string? _chosenId;
	private HttpClient? _admin;
	private string? _reportId;

	private const string Prose = "Wind picked up on final approach — synthetic.";

	private JsonElement _answered;
	private string? _submitted;

	// The value as JSON puts it on the wire: a string, or for "JSON true",
	// "JSON false", and "JSON null" that literal (ADR-0130).
	private object? _submittedValue;
	private Locale _language = Locale.EnCa;
	private HttpResponseMessage? _submission;

	private readonly Dictionary<string, string> _ordinary = [];
	private Report? _report;

	// --- REQ-QB-122: an answer names the choice it was given under ---

	[Given(@"^a reporter answering in English is shown an? (single_select|multi_select|autocomplete) question whose choices are written in both official languages$")]
	public async Task GivenAChoiceQuestion(string type)
	{
		_admin = await BootedApi.SignedInAs(MemberRole.Administrator);

		using var response = await _admin.PostAsJsonAsync(AdminQuestions, Request(type, Colours));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		_pickers[type] = await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	[When(@"the reporter chooses one of its choices and submits")]
	public async Task WhenTheReporterChoosesAndSubmits()
	{
		var (type, picker) = _pickers.Single();
		_chosenId = await ChoiceIdOf(picker, "Blue");

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		using var response = await reporter.PostAsJsonAsync(Submit, new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)false },
				new { questionRevisionId = RevisionOf(type), choices = new[] { _chosenId } },
			},
		});

		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		_reportId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
	}

	[Then(@"the stored answer references that choice by its identifier")]
	public async Task ThenTheStoredAnswerReferencesTheChoice()
	{
		(await StoredChoiceAnswer()).ChoiceId.ShouldBe(TinyId.Parse(_chosenId));
	}

	[Then(@"it stores no copy of the choice's wording")]
	public async Task ThenItStoresNoCopyOfTheWording()
	{
		var stored = await StoredChoiceAnswer();
		stored.Value.ShouldBeNull();
		stored.TranslatedValue.ShouldBeNull();
	}

	[Then(@"the answer reads as the choice's English label, with its French label as the second language")]
	public async Task ThenTheAnswerReadsAsTheChoice()
	{
		using var reviewer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var detail = await reviewer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		var key = _pickers.Single().Value.GetProperty("key").GetString();
		var shown = detail.GetProperty("answers").EnumerateArray()
			.Single(answer => answer.GetProperty("questionKey").GetString() == key)
			.GetProperty("values").EnumerateArray().Single();

		shown.GetProperty("value").GetString().ShouldBe("Blue");
		shown.GetProperty("translatedValue").GetString().ShouldBe("Bleu");
	}

	// --- REQ-QB-025: only consent is projected onto the report aggregate ---

	[Given(@"a submitted report has answers to several ordinary questions")]
	public void GivenAReportWithOrdinaryAnswers()
	{
		_ordinary["occurrence_date"] = "2026-09-21";
		_ordinary["occurrence_time"] = "14:30";
		_ordinary["province"] = "British Columbia";
		_ordinary["injury_severity"] = "Minor";
		_ordinary["aircraft_make"] = "Synthetic Wings";
	}

	[When(@"those answers are persisted")]
	public void WhenThoseAnswersArePersisted()
	{
		var report = new Report(Locale.EnCa, Noon);
		report.Answer(Question.CreateConsentPublish("Publish?", "Publier ?", Noon), true, Noon);
		report.Answer(Question.CreateConsentMedia("Show media?", "Montrer les médias ?", Noon), true, Noon);

		report.Answer(Ordinary("occurrence_date", QuestionType.Date), _ordinary["occurrence_date"], Noon);
		report.Answer(Ordinary("occurrence_time", QuestionType.Time), _ordinary["occurrence_time"], Noon);
		report.Answer(Picker("province", "British Columbia", "Colombie-Britannique"), _ordinary["province"], Noon);
		report.Answer(Picker("injury_severity", "Minor", "Mineure"), _ordinary["injury_severity"], Noon);
		report.Answer(Ordinary("aircraft_make", QuestionType.ShortText), _ordinary["aircraft_make"], Noon);

		_report = report;
	}

	[Then(@"only the consent_publish and consent_media answers are projected onto the report aggregate, with consent_documents derived from consent_media")]
	public void ThenOnlyConsentIsProjected()
	{
		_report!.ConsentPublish.ShouldBe(true);
		_report.ConsentMedia.ShouldBe(true);
		_report.ConsentDocuments.ShouldBe(_report.ConsentMedia);

		// Nothing else on the aggregate holds, or could hold, an ordinary answer.
		var projected = typeof(Report).GetProperties()
			.Where(property => property.Name != nameof(Report.Answers))
			.Select(property => property.GetValue(_report)?.ToString() ?? string.Empty)
			.ToList();

		foreach (var value in _ordinary.Values)
		{
			projected.ShouldNotContain(text => text.Contains(value, StringComparison.Ordinal));
		}

		typeof(Report).GetProperties()
			.Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType)
			.ShouldNotContain(type => type == typeof(DateOnly) || type == typeof(TimeOnly));
	}

	[Then(@"every other answer, including dates, times, provinces, injury severities, and aircraft details, remains a stored string read through its question key")]
	public void ThenEveryOtherAnswerIsAStoredString()
	{
		typeof(ReportAnswer).GetProperty(nameof(ReportAnswer.Value))!.PropertyType.ShouldBe(typeof(string));

		foreach (var (key, value) in _ordinary)
		{
			_report!.Answers.Single(answer => answer.QuestionKey == key).Text.ShouldBe(value);
		}
	}

	// --- REQ-QB-019, REQ-QB-118, REQ-QB-119: the written form, or a refusal ---

	[Given(@"^a reporter writing in (English|French) submits (.+) as the answer to a (\w+) question$")]
	public async Task GivenAReporterSubmitsAnAnswer(string language,
													string submitted,
													string type)
	{
		_language = language == "French" ? Locale.FrCa : Locale.EnCa;
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		using var response = await _admin.PostAsJsonAsync(AdminQuestions, Request(type, []));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		_answered = await response.Content.ReadFromJsonAsync<JsonElement>();
		_submitted = submitted switch
		{
			"an empty string" => string.Empty,
			"a line of prose" => Prose,
			"JSON true" => "true",
			"JSON false" => "false",
			"JSON null" => "null",
			_ => submitted,
		};
		_submittedValue = submitted switch
		{
			"JSON true" => true,
			"JSON false" => false,
			"JSON null" => null,
			_ => _submitted,
		};
	}

	[When(@"the answer is persisted")]
	public async Task WhenTheAnswerIsPersisted()
	{
		await WhenTheSubmissionIsMade();
		_submission!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _submission.Content.ReadAsStringAsync());
	}

	[When(@"the submission is made")]
	public async Task WhenTheSubmissionIsMade()
	{
		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		_submission = await reporter.PostAsJsonAsync(Submit, new
		{
			language = _language.Code,
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (object?)false },
				new { questionRevisionId = _answered.GetProperty("revisionId").GetString(), value = _submittedValue },
			},
		});
	}

	[Then(@"^the stored value is (.+)$")]
	public async Task ThenTheStoredValueIs(string stored)
	{
		var answer = (await AnswersToTheQuestion()).ShouldHaveSingleItem();

		switch (stored)
		{
			case "the boolean true" or "the boolean false":
				answer.BooleanValue.ShouldBe(stored == "the boolean true");
				answer.Value.ShouldBeNull();
				break;
			case "nothing, because the answer was skipped":
				answer.Value.ShouldBeNull();
				answer.BooleanValue.ShouldBeNull();
				break;
			default:
				answer.Value.ShouldBe(stored == "that line, as typed" ? Prose : stored);
				answer.BooleanValue.ShouldBeNull();
				break;
		}
	}

	[Then(@"it holds no words and no second language in either column, and its translation mode is none")]
	public async Task ThenItHoldsNoWordsAndNoSecondLanguage()
	{
		var answer = (await AnswersToTheQuestion()).ShouldHaveSingleItem();

		answer.BooleanValue.ShouldNotBeNull();
		answer.Value.ShouldBeNull();
		answer.TranslatedValue.ShouldBeNull();
		answer.TranslationSource.ShouldBeNull();
		answer.TranslationMode.ShouldBe(TranslationMode.None);
	}

	[Then(@"no translation provider was called and nothing waits for the Worker to translate it")]
	public async Task ThenNothingWaitsForTranslation()
	{
		// Written in the submission's own transaction, so there is nothing left
		// for the Worker's translator to pick up.
		(await AnswersToTheQuestion()).ShouldHaveSingleItem().NeedsTranslation.ShouldBeFalse();
	}

	[Then(@"the submission is rejected")]
	public async Task ThenTheSubmissionIsRejected()
	{
		_submission!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		// A refusal names the question, never the reporter's words. The key is
		// taken out first: a derived one can hold a word such as "yes".
		var problem = (await _submission.Content.ReadAsStringAsync())
			.Replace(_answered.GetProperty("key").GetString()!, string.Empty, StringComparison.Ordinal);
		problem.ShouldNotContain(_submitted!);
	}

	[Then(@"no stored answer carries that value")]
	public async Task ThenNoStoredAnswerCarriesIt()
	{
		(await AnswersToTheQuestion()).ShouldBeEmpty();
	}

	// --- helpers ---

	/// <summary>Every stored answer to this scenario's own question, from any report.</summary>
	private async Task<List<ReportAnswer>> AnswersToTheQuestion()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var questionId = TinyId.Parse(_answered.GetProperty("id").GetString());

		return await database.ReportAnswers
			.IgnoreQueryFilters()
			.AsNoTracking()
			.Where(answer => answer.QuestionId == questionId)
			.ToListAsync();
	}

	private string RevisionOf(string type)
	{
		return _pickers[type].GetProperty("revisionId").GetString()!;
	}

	/// <summary>This scenario's one choice answer, as stored.</summary>
	private async Task<ReportAnswer> StoredChoiceAnswer()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		var questionId = TinyId.Parse(_pickers.Single().Value.GetProperty("id").GetString());

		return await database.ReportAnswers.AsNoTracking()
			.SingleAsync(answer => answer.ReportId == reportId && answer.QuestionId == questionId);
	}

	/// <summary>A choice's identifier, as the public form offers it.</summary>
	private static async Task<string> ChoiceIdOf(JsonElement question,
												 string labelEn)
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var questions = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/questions/", UriKind.Relative));
		return questions.EnumerateArray()
			.Single(candidate => candidate.GetProperty("id").GetString() == question.GetProperty("id").GetString())
			.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("labelEn").GetString() == labelEn)
			.GetProperty("id").GetString()!;
	}

	private object Request(string type,
						   object[] options)
	{
		return new
		{
			key = (string?)null,
			type,
			labelEn = $"A synthetic {type} question {_run}",
			labelFr = $"Une question synthétique {type} {_run}",
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
			options,
		};
	}

	private static Question Ordinary(string key,
									 QuestionType type)
	{
		return Question.Create(key, type, $"Question {key}", $"Question {key} (fr)", Noon, isActive: true);
	}

	private static Question Picker(string key,
								   string labelEn,
								   string labelFr)
	{
		return Question.Create(
			key, QuestionType.SingleSelect, $"Question {key}", $"Question {key} (fr)", Noon, isActive: true,
			options: [new QuestionOptionInput(QuestionKey.Normalize(labelEn), labelEn, labelFr)]);
	}
}
