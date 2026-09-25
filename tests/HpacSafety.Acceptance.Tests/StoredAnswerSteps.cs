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
///     What a stored answer holds — REQ-QB-018, a picker's answer is the words the
///     reporter saw, and REQ-QB-025, only consent is projected onto the report
///     (ADR-0072, ADR-0095, ADR-0117, ADR-0119).
/// </summary>
/// <remarks>
///     REQ-QB-018 goes through the booted API, because "the words the reporter saw"
///     is a claim about what the submission endpoint persists. REQ-QB-025 is a rule
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
	private readonly Dictionary<string, string[]> _chosen = [];
	private HttpClient? _admin;
	private string? _reportId;

	private readonly Dictionary<string, string> _ordinary = [];
	private Report? _report;

	// --- REQ-QB-018: an answer to a picker stores the words the reporter saw ---

	[Given(@"a reporter is shown a picker, type-ahead, or multi-select question")]
	public async Task GivenAPickerQuestion()
	{
		_admin = await BootedApi.SignedInAs(MemberRole.Administrator);

		foreach (var type in new[] { "single_select", "autocomplete", "multi_select" })
		{
			using var response = await _admin.PostAsJsonAsync(AdminQuestions, Request(type, Colours));
			response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
			_pickers[type] = await response.Content.ReadFromJsonAsync<JsonElement>();
		}
	}

	[When(@"the reporter chooses a value and submits")]
	public async Task WhenTheReporterChoosesAndSubmits()
	{
		// In French, as a reporter using the French form sees each choice.
		_chosen["single_select"] = ["Bleu"];
		_chosen["autocomplete"] = ["Rouge"];
		_chosen["multi_select"] = ["Bleu", "Rouge"];

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		using var response = await reporter.PostAsJsonAsync(Submit, new
		{
			language = "fr-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (string?)"no" },
				new { questionRevisionId = RevisionOf("single_select"), value = (string?)"Bleu" },
				new { questionRevisionId = RevisionOf("autocomplete"), value = (string?)"Rouge" },
				new { questionRevisionId = RevisionOf("multi_select"), optionCodes = _chosen["multi_select"] },
			},
		});

		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		_reportId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
	}

	[Then(@"the stored answer holds that value's label exactly as it was shown")]
	public async Task ThenTheStoredAnswerIsTheLabel()
	{
		foreach (var (type, values) in await StoredValues())
		{
			values.ShouldBe(_chosen[type], ignoreOrder: true);
		}
	}

	[Then(@"it holds no option code and no reference to an option row")]
	public async Task ThenItHoldsNoOptionCode()
	{
		(await StoredValues()).Values.SelectMany(values => values)
			.ShouldNotContain(value => value == "blue" || value == "red");

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var answer = database.Model.FindEntityType(typeof(ReportAnswer))!;

		answer.GetForeignKeys().ShouldNotContain(key => key.PrincipalEntityType.ClrType == typeof(QuestionChoice));
		answer.GetProperties().Select(property => property.Name)
			.ShouldNotContain(name => name.Contains("Option", StringComparison.Ordinal)
									  || name.Contains("Choice", StringComparison.Ordinal));
	}

	[Then(@"relabelling or removing that option afterwards leaves the stored answer unchanged")]
	public async Task ThenRelabellingLeavesTheAnswer()
	{
		// Blue is relabelled and Red removed, in place: a choices-only edit
		// revises nothing and forks nothing (ADR-0095).
		object[] edited = [new { code = "blue", labelEn = "Navy", labelFr = "Marine" }];

		foreach (var (type, view) in _pickers)
		{
			using var response = await _admin!.PutAsJsonAsync(
				new Uri($"/api/admin/questions/{view.GetProperty("id").GetString()}", UriKind.Relative),
				Request(type, edited));
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		}

		foreach (var (type, values) in await StoredValues())
		{
			values.ShouldBe(_chosen[type], ignoreOrder: true);
		}
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
		report.Answer(Question.CreateConsentPublish("Publish?", "Publier ?", Noon), "yes", Noon);
		report.Answer(Question.CreateConsentMedia("Show media?", "Montrer les médias ?", Noon), "yes", Noon);

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
			_report!.Answers.Single(answer => answer.QuestionKey == key).Value.ShouldBe(value);
		}
	}

	// --- helpers ---

	private string RevisionOf(string type)
	{
		return _pickers[type].GetProperty("revisionId").GetString()!;
	}

	/// <summary>The stored values of this scenario's report, by question type.</summary>
	private async Task<Dictionary<string, string[]>> StoredValues()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		var answers = await database.ReportAnswers.AsNoTracking().Where(answer => answer.ReportId == reportId).ToListAsync();

		return _pickers.ToDictionary(
			pair => pair.Key,
			pair => answers
				.Where(answer => answer.QuestionId.Value == pair.Value.GetProperty("id").GetString())
				.Select(answer => answer.Value!)
				.ToArray());
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
