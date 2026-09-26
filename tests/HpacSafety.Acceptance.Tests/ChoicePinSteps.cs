using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The non-<c>@ui</c> scenarios for pinning a choice first or last (ADR-0136):
///     what the API sends every reader, that a reporter's new value is not pinned,
///     and that pinning never revises or forks. The alphabetical order within each
///     group is the browser's, proven by the <c>@ui</c> scenarios. Every choice
///     here is synthetic.
/// </summary>
[Binding]
public sealed class ChoicePinSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset Noon = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);

	private string? _questionId;
	private readonly List<JsonElement> _readOptions = [];
	private Question _question = null!;
	private Question _live = null!;
	private TinyId _revisionId;
	private QuestionChoice? _added;

	// ------------------------------------------------------------ over HTTP --

	[Given(@"an Administrator saves a single-select question with ""(.*)"" pinned last, ""(.*)"" and ""(.*)"" pinned first, and ""(.*)"" and ""(.*)"" not pinned")]
	public async Task GivenAPinnedQuestionIsSaved(string last,
												  string firstA,
												  string firstB,
												  string noneA,
												  string noneB)
	{
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);

		var options = new[] { (last, "last"), (firstA, "first"), (noneA, "none"), (firstB, "first"), (noneB, (string?)null) }
			.Select(option => new { code = (string?)null, labelEn = option.Item1, labelFr = $"{option.Item1} (fr)", pin = option.Item2 });

		using var response = await admin.PostAsJsonAsync(AdminQuestions, new
		{
			key = $"country_{Guid.NewGuid():N}"[..30],
			type = "single_select",
			labelEn = "Which country were you flying in?",
			labelFr = "Dans quel pays voliez-vous ?",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options,
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		_questionId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
	}

	[When(@"the report form's questions and the editor's questions are read")]
	public async Task WhenBothViewsAreRead()
	{
		var host = await BootedApi.Factory();
		using var reader = host.CreateClient();
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);

		foreach (var list in new[]
				 {
					 await reader.GetFromJsonAsync<JsonElement>(PublicQuestions),
					 await admin.GetFromJsonAsync<JsonElement>(AdminQuestions),
				 })
		{
			_readOptions.Add(list.EnumerateArray().Single(question => question.GetProperty("id").GetString() == _questionId)
				.GetProperty("options"));
		}
	}

	[Then(@"each lists that question's pinned-first choices, then its unpinned choices, then its pinned-last choices")]
	public void ThenEachListsTheGroupsInTurn()
	{
		_readOptions.Count.ShouldBe(2);

		foreach (var options in _readOptions)
		{
			var pins = options.EnumerateArray().Select(option => option.GetProperty("pin").GetString()).ToList();
			pins.Take(2).ShouldAllBe(pin => pin == "first");
			pins.Skip(2).Take(2).ShouldAllBe(pin => pin == "none");
			pins.Skip(4).ShouldBe(["last"]);
		}
	}

	[Then(@"each choice carries its pin as ""(.*)"", ""(.*)"", or ""(.*)""")]
	public void ThenEachChoiceCarriesItsPin(string first,
											string none,
											string last)
	{
		foreach (var options in _readOptions)
		{
			var pins = options.EnumerateArray()
				.ToDictionary(option => option.GetProperty("labelEn").GetString()!, option => option.GetProperty("pin").GetString());

			pins.ShouldBe(new Dictionary<string, string?>
			{
				["Canada"] = first,
				["United States"] = first,
				["Mexico"] = none,
				["Brazil"] = none,
				["Other"] = last,
			}, ignoreOrder: true);
		}
	}

	// ------------------------------------------------------ against the domain --

	[Given(@"a type-ahead question offers ""(.*)"" pinned last and ""(.*)"" not pinned")]
	public void GivenATypeAheadWithAPinnedValue(string last,
												string none)
	{
		_question = Question.Create(
			"launch_site", QuestionType.Autocomplete, "Where did you launch?", "D'où avez-vous décollé ?", Noon, isActive: true,
			options:
			[
				new QuestionOptionInput(QuestionKey.Normalize(last), last, last, Pin: ChoicePin.Last),
				new QuestionOptionInput(QuestionKey.Normalize(none), none, none),
			]);
	}

	[When(@"a reporter answering in English submits ""(.*)"" for it, which the question does not offer")]
	public void WhenAReporterAddsAValue(string value)
	{
		var report = new Report(Locale.EnCa, Noon);
		_added = _question.AddChoiceFromReporter(value, Locale.EnCa, Noon);
		report.AnswerChoices(_question, _question.CurrentRevision, [_added.Id], Noon);
	}

	[Then(@"the new value is not pinned")]
	public void ThenTheNewValueIsNotPinned()
	{
		_added.ShouldNotBeNull().Pin.ShouldBe(ChoicePin.None);
	}

	[Then(@"the question offers it among its unpinned choices, before ""(.*)""")]
	public void ThenItIsOfferedAmongTheUnpinned(string last)
	{
		var offered = _question.Choices.ToList();

		offered.Last().LabelEn.ShouldBe(last);
		offered.ShouldContain(_added!);
		offered.Where(choice => choice.Pin == ChoicePin.None).ShouldContain(_added!);
		offered.IndexOf(_added!).ShouldBeLessThan(offered.Count - 1);
	}

	[Given(@"an answered single-select question offers ""(.*)"", ""(.*)"", and ""(.*)"", none pinned")]
	public void GivenAnAnsweredSingleSelect(string first,
											string second,
											string third)
	{
		_question = Question.Create(
			"country", QuestionType.SingleSelect, "Which country were you flying in?", "Dans quel pays voliez-vous ?", Noon, isActive: true,
			options: [.. new[] { first, second, third }.Select(label => new QuestionOptionInput(QuestionKey.Normalize(label), label, $"{label} (fr)"))]);
		new Report(Locale.EnCa, Noon).AnswerChoices(_question, _question.CurrentRevision, [_question.Choices[0].Id], Noon);
		_revisionId = _question.CurrentRevision.Id;
	}

	[When(@"an Administrator pins ""(.*)"" first and ""(.*)"" last")]
	public void WhenAnAdministratorPins(string first,
										string last)
	{
		var current = _question.CurrentRevision;

		// What the editor sends: every choice as it stands, only the pins changed.
		var options = _question.Choices
			.Select(choice => new QuestionOptionInput(
				choice.Code, choice.LabelEn, choice.LabelFr,
				Pin: choice.LabelEn == first ? ChoicePin.First : choice.LabelEn == last ? ChoicePin.Last : ChoicePin.None))
			.ToList();

		_live = _question.ApplyEdit(
			true, current.Type, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Noon.AddHours(1), options: options);
	}

	[Then(@"""(.*)"" is pinned first, ""(.*)"" is pinned last, and ""(.*)"" is not pinned")]
	public void ThenThePinsAreSaved(string first,
									string last,
									string none)
	{
		PinOf(first).ShouldBe(ChoicePin.First);
		PinOf(last).ShouldBe(ChoicePin.Last);
		PinOf(none).ShouldBe(ChoicePin.None);
	}

	[Then(@"the pinned question keeps its identifier and its current revision")]
	public void ThenThePinnedQuestionIsKept()
	{
		_live.ShouldBeSameAs(_question);
		_question.Deleted.ShouldBeNull();
		_question.CurrentRevision.Id.ShouldBe(_revisionId);
	}

	private ChoicePin PinOf(string label)
	{
		return _question.Choices.Single(choice => choice.LabelEn == label).Pin;
	}
}
