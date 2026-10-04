using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     A question's choices may depend on another question's answer, each choice under
///     one or more parent choices (<c>REQ-QB-179</c> to <c>REQ-QB-221</c>,
///     <c>REQ-SUB-113</c> to <c>REQ-SUB-115</c>, ADR-0146, ADR-0151).
/// </summary>
/// <remarks>
///     Every claim here is about what the admin, review, public-form, or submission
///     endpoint does, so each runs through the booted API. The bank is shared by
///     scenarios running in parallel, so every question is worded with this
///     scenario's own run marker, and is found by that. Every question, choice, and
///     answer is synthetic.
/// </remarks>
[Binding]
public sealed class DependentChoiceSteps
{
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions/", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);

	private readonly Dictionary<string, string> _ids = new(StringComparer.Ordinal);
	private readonly Dictionary<string, JsonElement> _before = new(StringComparer.Ordinal);
	private readonly List<(string Question, string ChoiceId)> _answered = [];
	private readonly string _run = Guid.NewGuid().ToString("N")[..10];
	private HttpClient? _admin;
	private HttpClient? _officer;
	private HttpResponseMessage? _response;
	private string? _parentType;
	private string? _childName;
	private string? _lastChoice;
	private (TinyId Id, TinyId? ChoiceId)[] _answersBefore = [];

	// ---- REQ-QB-179: which types may take part ----

	[Given(@"a {word} question, offering ""Niviuk"" and ""Ozone"" when its type has choices")]
	public async Task GivenAParentOfType(string named)
	{
		var type = GlossaryNames.QuestionTypeCode(named);
		_parentType = type;
		var takesChoices = type is "single_select" or "multi_select" or "autocomplete";
		await Create("Make", type, takesChoices ? [("Niviuk", null), ("Ozone", null)] : []);
	}

	[When(@"an Administrator makes a {word} question's choices depend on it, linking each choice to one of its choices")]
	public async Task WhenAChildOfTypeDependsOnIt(string named)
	{
		var type = GlossaryNames.QuestionTypeCode(named);
		var make = await View("Make");
		var first = make.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("id").GetString()).FirstOrDefault();
		_response = await Post(Request("Model", type, [Option("Mentor 7", first)], _ids["Make"]));
	}

	[Then(@"the dependency is {word}")]
	public async Task ThenTheDependencyIs(string outcome)
	{
		var body = await _response!.Content.ReadAsStringAsync();
		_response.StatusCode.ShouldBe(outcome == "accepted" ? HttpStatusCode.Created : HttpStatusCode.BadRequest, body);

		if (outcome == "accepted")
		{
			JsonDocument.Parse(body).RootElement.GetProperty("choicesDependOnQuestionId").GetString().ShouldBe(_ids["Make"], _parentType);
		}
	}

	// ---- The usual make and model ----

	[Given(@"the {string} question's choices depend on the {string} question")]
	[Given(@"the {string} question's choices depend on the {string} question, which comes before it")]
	public Task GivenModelDependsOnMake(string child,
									   string parent)
	{
		return Arrange(parent, "single_select", child, "autocomplete");
	}

	[Given(@"the {string} question's choices depend on a {word} {string} question")]
	public Task GivenModelDependsOnATypedMake(string child,
											 string parentType,
											 string parent)
	{
		return Arrange(parent, parentType is "autocomplete" or "type-ahead" ? "autocomplete" : "single_select", child, "autocomplete");
	}

	[Given(@"the type-ahead {string} question's choices depend on the type-ahead {string} question")]
	public Task GivenTypeAheadModelDependsOnTypeAheadMake(string child,
														  string parent)
	{
		return Arrange(parent, "autocomplete", child, "autocomplete");
	}

	[Given(@"the {string} question's choices depend on an answered single-select {string} question")]
	public async Task GivenModelDependsOnAnAnsweredMake(string child,
													   string parent)
	{
		await Arrange(parent, "single_select", child, "autocomplete");
		await Answered((parent, ChoiceId(await View(parent), "Niviuk")));
		await Remember(parent, child);
	}

	[Given(@"the answered {string} question's choices depend on the {string} question")]
	public async Task GivenAnAnsweredModelDependsOnMake(string child,
														string parent)
	{
		await Arrange(parent, "single_select", child, "autocomplete");
		await Answered((parent, ChoiceId(await View(parent), "Niviuk")), (child, ChoiceId(await View(child), "Mentor 7")));
		await Remember(parent, child);
	}

	[Given(@"the {string} question's choices depend on the {string} question, and {string} is offered under {string}")]
	public async Task GivenModelDependsOnMakeWithALink(string child,
													   string parent,
													   string choice,
													   string parentChoice)
	{
		await Arrange(parent, "single_select", child, "autocomplete");
		await GivenIsLinkedTo(choice, parentChoice);
	}

	[Given(@"{string} is linked to the {string} choice")]
	[Given(@"{string} is linked to the {string} value")]
	public async Task GivenIsLinkedTo(string choice,
									  string parentChoice)
	{
		var model = await View(_childName!);
		var parentChoiceId = ChoiceId(await View("Make"), parentChoice);

		if (!ParentsOf(model, choice).SequenceEqual([parentChoiceId]))
		{
			var saved = await Put(_childName!, RequestFrom(model, options: Options(model, (choice, parentChoiceId))));
			saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		}

		await Remember("Make", _childName!);
	}

	// ---- REQ-QB-180: one level only ----

	[When(@"an Administrator makes a third question's choices depend on {string}")]
	public async Task WhenAThirdDependsOnTheChild(string child)
	{
		var linked = ChoiceId(await View(child), "Mentor 7");
		_response = await Post(Request("Size", "autocomplete", [Option("Small", linked)], _ids[child]));
	}

	[Then(@"the dependency is refused, saying {string} already depends on another question")]
	public async Task ThenRefusedBecauseTheParentDepends(string parent)
	{
		var detail = await Refused();
		detail.ShouldContain(Label(parent));
		detail.ShouldContain("one level deep");
	}

	[When(@"an Administrator makes the {string} question's choices depend on a third question")]
	public async Task WhenTheParentDependsOnAThird(string parent)
	{
		await Create("Wing", "single_select", [("Paraglider", null)]);
		var make = await View(parent);
		var wing = ChoiceId(await View("Wing"), "Paraglider");
		_response = await Put(parent, RequestFrom(make, _ids["Wing"], Options(make, [.. Labels(make).Select(label => (label, wing))])));
	}

	[Then(@"the dependency is refused, saying other questions' choices already depend on {string}")]
	public async Task ThenRefusedBecauseTheChildIsAParent(string parent)
	{
		var detail = await Refused();
		detail.ShouldContain(Label("Model"));
		detail.ShouldContain("one level deep");
	}

	// ---- REQ-QB-181: form order ----

	[Given(@"the {string} question comes after the {string} question on the form")]
	public async Task GivenTheParentComesAfter(string parent,
											   string child)
	{
		await Create(child, "autocomplete", [("Mentor 7", null)]);
		await Create(parent, "single_select", [("Niviuk", null), ("Ozone", null)]);
	}

	[When(@"an Administrator makes the {string} question's choices depend on {string}")]
	public async Task WhenTheChildIsMadeToDependOn(string child,
												   string parent)
	{
		var model = await View(child);
		var niviuk = ChoiceId(await View(parent), "Niviuk");
		_response = await Put(child, RequestFrom(model, _ids[parent], Options(model, [.. Labels(model).Select(label => (label, niviuk))])));
	}

	[Then(@"the dependency is refused, naming both questions")]
	[Then(@"the new order is refused, naming both questions")]
	[Then(@"the change is refused, naming both questions")]
	public async Task ThenRefusedNamingBoth()
	{
		var detail = await Refused();
		detail.ShouldContain(Label("Make"));
		detail.ShouldContain(Label("Model"));
	}

	[When(@"an Administrator moves {string} before {string}")]
	public async Task WhenTheChildMovesFirst(string child,
											 string parent)
	{
		// Every question must be listed, and parallel scenarios add questions, so a
		// listing that went stale before it was sent is simply taken again. A short
		// jittered delay between attempts gives the concurrent writers a chance to
		// settle, instead of hammering the endpoint with the same stale snapshot.
		const int maxAttempts = 50;

		for (var attempt = 0; attempt < maxAttempts; attempt++)
		{
			var order = (await _admin!.GetFromJsonAsync<JsonElement>(AdminQuestions)).EnumerateArray()
				.Select(question => question.GetProperty("id").GetString()!)
				.ToList();
			order.Remove(_ids[child]);
			order.Insert(order.IndexOf(_ids[parent]), _ids[child]);

			_response = await _admin!.PostAsJsonAsync(new Uri("/api/admin/questions/order", UriKind.Relative), new { questionIdsInOrder = order });
			var body = await _response.Content.ReadAsStringAsync();

			if (!body.Contains("incomplete-order", StringComparison.Ordinal)
				&& !body.Contains("unknown-question", StringComparison.Ordinal))
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(20 + Random.Shared.Next(0, 60)));
		}

		throw new InvalidOperationException(
			$"Order kept going stale after {maxAttempts} attempts because parallel scenarios kept adding questions.");
	}

	// ---- REQ-QB-212: every choice is offered under at least one parent choice ----

	[Given(@"a type-ahead question {string} offers {string} and {string}")]
	public async Task GivenAnUnlinkedTypeAhead(string child,
											   string first,
											   string second)
	{
		await Create("Make", "single_select", [("Niviuk", null), ("Ozone", null)]);
		await Create(child, "autocomplete", [(first, null), (second, null)]);
		_childName = child;
		await Remember("Make", child);
	}

	[When(@"an Administrator makes its choices depend on the {string} question, offering only {string} under {string}")]
	public async Task WhenOfferingOnlyOne(string parent,
										  string choice,
										  string parentChoice)
	{
		var model = await View(_childName!);
		_response = await Put(_childName!, RequestFrom(model, _ids[parent], Options(model, (choice, ChoiceId(await View(parent), parentChoice)))));
	}

	[Then(@"the save is refused, naming {string}")]
	public async Task ThenTheSaveIsRefusedNaming(string choice)
	{
		(await Refused()).ShouldContain($"'{choice}'");
	}

	[Then(@"nothing is saved")]
	public async Task ThenNothingIsSaved()
	{
		var model = await View(_childName!);
		model.GetProperty("choicesDependOnQuestionId").ValueKind.ShouldBe(JsonValueKind.Null);
		model.GetProperty("options").EnumerateArray().ShouldAllBe(option => option.GetProperty("parentChoiceIds").GetArrayLength() == 0);
	}

	[When(@"they offer {string} under {string} and {string}, and {string} under {string}, in the same save")]
	public async Task WhenOfferingBoth(string first,
									   string firstParent,
									   string firstOtherParent,
									   string second,
									   string secondParent)
	{
		var model = await View(_childName!);
		var make = await View("Make");
		_response = await Put(_childName!, RequestFrom(model, _ids["Make"], OptionsUnder(model,
			(first, [ChoiceId(make, firstParent), ChoiceId(make, firstOtherParent)]),
			(second, [ChoiceId(make, secondParent)]))));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"the dependency is saved, with {string} under both and {string} under {string}")]
	public async Task ThenBothAreSaved(string first,
									   string second,
									   string secondParent)
	{
		var model = await View(_childName!);
		var make = await View("Make");
		model.GetProperty("choicesDependOnQuestionId").GetString().ShouldBe(_ids["Make"]);
		ParentsOf(model, first).ShouldBe(Sorted(ChoiceId(make, "Niviuk"), ChoiceId(make, "Ozone")));
		ParentsOf(model, second).ShouldBe([ChoiceId(make, secondParent)]);
	}

	[Then(@"adding a choice to {string} under no parent choice is refused")]
	public async Task ThenAnUnlinkedChoiceIsRefused(string child)
	{
		var model = await View(child);
		_response = await Put(child, RequestFrom(model, options: [.. Options(model), Option("Buzz Z7", null)]));
		(await Refused()).ShouldContain("'Buzz Z7'");

		_response = await Put(child, RequestFrom(model, options: [.. Options(model), OptionUnder("Buzz Z7")]));
		(await Refused()).ShouldContain("'Buzz Z7'");
	}

	[Then(@"offering a choice under a choice of any question other than {string} is refused")]
	public async Task ThenAForeignLinkIsRefused(string parent)
	{
		await Create("Elsewhere", "single_select", [("Gin", null)]);
		var model = await View(_childName!);
		_response = await Put(_childName!, RequestFrom(model, options: OptionsUnder(model,
			("Mentor 7", [ChoiceId(await View("Make"), "Niviuk"), ChoiceId(await View("Elsewhere"), "Gin")]))));
		(await Refused()).ShouldContain(Label(parent));
	}

	// ---- REQ-QB-213: one choice under several parent choices, and unique wording ----

	[When(@"an Administrator adds {string}, in French {string}, offered under {string} and {string}")]
	public async Task WhenAddingOneBilingualUnderBoth(string wording,
													  string french,
													  string firstParent,
													  string secondParent)
	{
		var model = await View(_childName!);
		var make = await View("Make");
		var option = OptionUnder(wording, ChoiceId(make, firstParent), ChoiceId(make, secondParent));
		option["labelFr"] = french;
		_response = await Put(_childName!, RequestFrom(model, options: [.. Options(model), option]));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	// Adds an English-only choice under two parents; the arrangement behind the
	// Given steps that need one, not a step of its own.
	private async Task WhenAddingOneOtherUnderBoth(string wording,
												   string firstParent,
												   string secondParent)
	{
		var model = await View(_childName!);
		var make = await View("Make");
		_response = await Put(_childName!, RequestFrom(model, options: [.. Options(model), OptionUnder(wording, ChoiceId(make, firstParent), ChoiceId(make, secondParent))]));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} offers one {string} choice, offered under both")]
	public async Task ThenOneChoiceUnderBoth(string child,
											 string wording)
	{
		var model = await View(child);
		var make = await View("Make");
		model.GetProperty("options").EnumerateArray().Count(option => option.GetProperty("labelEn").GetString() == wording).ShouldBe(1);
		ParentsOf(model, wording).ShouldBe(Sorted(ChoiceId(make, "Niviuk"), ChoiceId(make, "Ozone")));
	}

	[Then(@"adding a second {string} under any parent choice is refused, naming it")]
	public Task ThenASecondIsRefused(string wording)
	{
		return RefusedAsRepeated(wording, $"{wording} (fr) bis", wording);
	}

	[Then(@"adding {string} in English under any parent choice is refused, naming it")]
	public Task ThenAWhitespaceVariantIsRefused(string wording)
	{
		ArgumentNullException.ThrowIfNull(wording);
		return RefusedAsRepeated(wording, "Something else", wording.Trim());
	}

	[Then(@"adding a choice whose French reads {string} under any parent choice is refused, naming it")]
	public Task ThenAFrenchTwinIsRefused(string french)
	{
		return RefusedAsRepeated("Anything else", french, french);
	}

	// ---- REQ-QB-184: outside revisions ----	// ---- REQ-QB-184: outside revisions ----

	[Given(@"an answered {string} question and an answered {string} question")]
	public async Task GivenTwoAnsweredQuestions(string parent,
												string child)
	{
		await Create(parent, "single_select", [("Niviuk", null), ("Ozone", null)]);
		await Create(child, "autocomplete", [("Mentor 7", null), ("Rush 6", null)]);
		_childName = child;
		await Answered((parent, ChoiceId(await View(parent), "Niviuk")), (child, ChoiceId(await View(child), "Mentor 7")));
		await Remember(parent, child);
	}

	[When(@"an Administrator makes {string}'s choices depend on {string} and links each choice")]
	public async Task WhenTheAnsweredChildIsLinked(string child,
												   string parent)
	{
		var model = await View(child);
		var make = await View(parent);
		_response = await Put(child, RequestFrom(model, _ids[parent], Options(model, ("Mentor 7", ChoiceId(make, "Niviuk")), ("Rush 6", ChoiceId(make, "Ozone")))));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[When(@"later links one choice to a different parent choice")]
	public async Task WhenOneLinkChanges()
	{
		var model = await View(_childName!);
		var make = await View("Make");
		_response = await Put(_childName!, RequestFrom(model, options: OptionsUnder(model, ("Rush 6", [ChoiceId(make, "Niviuk"), ChoiceId(make, "Ozone")]))));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
		ParentsOf(await View(_childName!), "Rush 6").ShouldContain(ChoiceId(make, "Niviuk"));
	}

	[Then(@"neither question gains a revision, and neither is replaced")]
	[Then(@"neither question gains a revision")]
	public async Task ThenNeitherIsRevised()
	{
		await ThenGainsNoRevision("Make");
		await ThenGainsNoRevision(_childName!);
	}

	[Then(@"every earlier answer still names the choice it named")]
	public async Task ThenEveryAnswerStillNamesItsChoice()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		foreach (var (question, choiceId) in _answered)
		{
			var id = TinyId.Parse(_ids[question]);
			var named = await database.ReportAnswers.AsNoTracking().Where(answer => answer.QuestionId == id).Select(answer => answer.ChoiceId).ToListAsync();
			named.ShouldContain(TinyId.Parse(choiceId));
		}
	}

	// ---- REQ-QB-185: clearing the parent ----

	[When(@"an Administrator clears the {string} question's parent")]
	public async Task WhenTheParentIsCleared(string child)
	{
		var model = await View(child);
		var request = RequestFrom(model, options: Options(model));
		request["choicesDependOnQuestionId"] = null;
		foreach (var option in request["options"]!.AsArray())
		{
			option!["parentChoiceIds"] = null;
		}

		_response = await Put(child, request);
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"each {string} choice keeps its link")]
	public async Task ThenEveryLinkIsKept(string child)
	{
		var model = await View(child);
		model.GetProperty("choicesDependOnQuestionId").ValueKind.ShouldBe(JsonValueKind.Null);
		ParentsOf(model, "Mentor 7").ShouldBe([ChoiceId(await View("Make"), "Niviuk")]);
		ParentsOf(model, "Rush 6").ShouldBe([ChoiceId(await View("Make"), "Ozone")]);
	}

	[Then(@"the report form offers every {string} choice, whatever {string} is answered with")]
	public async Task ThenTheFormOffersEveryChoice(string child,
												   string parent)
	{
		var shown = await PublicView(child);
		shown.GetProperty("choicesDependOnQuestionId").ValueKind.ShouldBe(JsonValueKind.Null);
		shown.GetProperty("options").GetArrayLength().ShouldBe(3);

		// A Niviuk make with an Ozone model is no longer refused.
		(await SubmitAnswers((parent, ChoiceId(await View(parent), "Niviuk")), (child, ChoiceId(await View(child), "Rush 6"))))
			.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[Then(@"a choice added to {string} needs no parent choice")]
	public async Task ThenANewChoiceNeedsNoLink(string child)
	{
		var model = await View(child);
		var saved = await Put(child, RequestFrom(model, options: [.. Options(model), Option("Buzz Z7", null)]));
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
	}

	// ---- REQ-QB-214: a parent choice is removed while each child keeps a parent ----

	[Given(@"{string} is offered under {string} and {string}, and {string} under {string} only")]
	public async Task GivenOneSharedAndOneAlone(string shared,
												string firstParent,
												string secondParent,
												string alone,
												string aloneParent)
	{
		// The child offers exactly these under the parent, and "Rush 6" under "Ozone".
		var model = await View(_childName!);
		var make = await View("Make");
		var request = RequestFrom(model, options:
		[
			OptionUnder(shared, ChoiceId(make, firstParent), ChoiceId(make, secondParent)),
			.. OptionsUnder(model, (alone, [ChoiceId(make, aloneParent)]))
				.Where(option => option["labelEn"]!.GetValue<string>() is var label && (label == alone || label == "Rush 6")),
		]);
		var saved = await Put(_childName!, request);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		_ids[$"{firstParent} (choice)"] = ChoiceId(make, firstParent);
		await Remember("Make", _childName!);
	}

	[When(@"an Administrator saving the question removes {string}")]
	public async Task WhenAnAdministratorRemovesTheParentChoice(string parentChoice)
	{
		var make = await View("Make");
		var request = RequestFrom(make, options: Options(make));
		var kept = new JsonArray([.. request["options"]!.AsArray().Where(option => option!["labelEn"]!.GetValue<string>() != parentChoice).Select(option => option!.DeepClone())]);
		request["options"] = kept;
		_response = await Put("Make", request);
	}

	[When(@"a Safety Officer on the type-ahead review page removes {string}")]
	public async Task WhenASafetyOfficerRemovesTheParentValue(string parentChoice)
	{
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_response = await _officer.DeleteAsync(new Uri($"/api/admin/type-ahead-values/{ChoiceId(await View("Make"), parentChoice)}", UriKind.Relative));
	}

	[Then(@"the removal is refused, naming {string} and not {string}")]
	public async Task ThenTheRemovalIsRefused(string stranded,
											  string kept)
	{
		var detail = await Refused();
		detail.ShouldContain($"'{stranded}'");
		detail.ShouldNotContain($"'{kept}'");
		detail.ShouldContain(Label(_childName!));
		ChoiceId(await View("Make"), "Niviuk").ShouldNotBeNull();
	}

	[When(@"{string} is also offered under {string} and an Administrator saving the question removes {string} again")]
	public async Task WhenAlsoOfferedAndAdministratorRemoves(string choice,
															 string otherParent,
															 string parentChoice)
	{
		await OfferAlsoUnder(choice, otherParent);
		await WhenAnAdministratorRemovesTheParentChoice(parentChoice);
	}

	[When(@"{string} is also offered under {string} and a Safety Officer on the type-ahead review page removes {string} again")]
	public async Task WhenAlsoOfferedAndSafetyOfficerRemoves(string choice,
															 string otherParent,
															 string parentChoice)
	{
		await OfferAlsoUnder(choice, otherParent);
		await WhenASafetyOfficerRemovesTheParentValue(parentChoice);
	}

	[Then(@"{string} is removed")]
	public async Task ThenTheParentChoiceIsRemoved(string parentChoice)
	{
		_response!.IsSuccessStatusCode.ShouldBeTrue(await _response.Content.ReadAsStringAsync());
		(await View("Make")).GetProperty("options").EnumerateArray()
			.ShouldNotContain(option => option.GetProperty("labelEn").GetString() == parentChoice);
	}

	[Then(@"{string} and {string} keep their {string} links, which filter nothing")]
	public async Task ThenTheLinksStayInert(string first,
											string second,
											string parentChoice)
	{
		var removed = _ids[$"{parentChoice} (choice)"];
		var model = await View(_childName!);
		ParentsOf(model, first).ShouldContain(removed);
		ParentsOf(model, second).ShouldContain(removed);

		// The form no longer offers the removed parent choice, so the link never matches an answer.
		(await PublicView("Make")).GetProperty("options").EnumerateArray()
			.ShouldNotContain(option => option.GetProperty("id").GetString() == removed);
	}

	[Then(@"saving {string} again, as the editor sends it, succeeds and keeps the {string} links")]
	public async Task ThenTheChildSavesAgain(string child,
											 string parentChoice)
	{
		var removed = _ids[$"{parentChoice} (choice)"];
		var model = await View(child);

		// The editor's draft echoes every link the view names, the removed parent's included.
		var saved = await Put(child, RequestFrom(model, options: Options(model)));
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		ParentsOf(await View(child), "Other").ShouldContain(removed);
		ParentsOf(await View(child), "Mentor 7").ShouldContain(removed);
	}

	[Then(@"a reviewer offering {string} under {string} only succeeds and keeps its {string} link")]
	public async Task ThenAReviewerRelinksAfterTheRemoval(string choice,
														  string parentChoice,
														  string removedChoice)
	{
		var removed = _ids[$"{removedChoice} (choice)"];
		var live = ChoiceId(await View("Make"), parentChoice);
		var response = await SetParents(choice, live);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
		ParentsOf(await View(_childName!), choice).ShouldBe(Sorted(live, removed));
	}

	[Then(@"a reviewer offering {string} under the removed {string} alone is refused, and {string} stays under {string}")]
	public async Task ThenOnlyARemovedParentIsRefused(string choice,
													  string removedChoice,
													  string _,
													  string liveChoice)
	{
		var removed = _ids[$"{removedChoice} (choice)"];
		var live = ChoiceId(await View("Make"), liveChoice);
		var before = ParentsOf(await View(_childName!), choice);

		_response = await SetParents(choice, removed);
		(await Refused()).ShouldContain("at least one");

		var after = ParentsOf(await View(_childName!), choice);
		after.ShouldBe(before);
		after.ShouldContain(live);
	}

	// ---- REQ-QB-215: a re-pointed link collapses into the one the child has ----

	[Given(@"{string} is offered under {string} and {string}")]
	public async Task GivenOfferedUnderTwo(string choice,
										   string firstParent,
										   string secondParent)
	{
		var model = await View(_childName!);
		var make = await View("Make");
		var request = RequestFrom(model, options: [.. Options(model), OptionUnder(choice, ChoiceId(make, firstParent), ChoiceId(make, secondParent))]);
		var saved = await Put(_childName!, request);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		_ids[$"{firstParent} (choice)"] = ChoiceId(make, firstParent);
		_lastChoice = choice;
		await Remember("Make", _childName!);
	}

	[Then(@"{string} is offered under {string} once")]
	public async Task ThenOfferedUnderOnce(string choice,
										   string parentChoice)
	{
		ParentsOf(await View(_childName!), choice).ShouldBe([ChoiceId(await View("Make"), parentChoice)]);
	}

	[Then(@"its link to {string} is marked removed, not erased")]
	public async Task ThenTheOldLinkIsStamped(string parentChoice)
	{
		var link = await LinkRow(_lastChoice!, _ids[$"{parentChoice} (choice)"]);
		link.ShouldNotBeNull();
		link.Deleted.ShouldNotBeNull();
	}

	// ---- REQ-QB-187 to REQ-QB-190: a link follows its parent ----

	[When(@"an Administrator replaces the parent choice {string} with {string}")]
	public async Task WhenTheParentChoiceIsReplaced(string parentChoice,
													string replacement)
	{
		var make = await View("Make");
		var request = RequestFrom(make, options: Options(make));
		var option = request["options"]!.AsArray().Single(candidate => candidate!["labelEn"]!.GetValue<string>() == parentChoice)!;
		option["labelEn"] = replacement;
		option["labelFr"] = $"{replacement} (fr)";
		option["replace"] = true;

		_response = await Put("Make", request);
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[When(@"a Safety Officer merges the parent value {string} into {string}")]
	public async Task WhenTheParentValueIsMerged(string source,
												 string target)
	{
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var make = await View("Make");
		_response = await _officer.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{ChoiceId(make, source)}/merge", UriKind.Relative),
			new { intoId = ChoiceId(make, target) });
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} is linked to {string}")]
	public async Task ThenIsLinkedTo(string choice,
									 string parentChoice)
	{
		ParentsOf(await View(_childName!), choice).ShouldBe([ChoiceId(await View("Make"), parentChoice)]);
	}

	[When(@"an Administrator changes the {string} question's wording")]
	public async Task WhenTheQuestionIsReworded(string name)
	{
		var view = await View(name);
		var request = RequestFrom(view, options: Options(view));
		request["labelEn"] = $"{Label(name)} (reworded)";
		_response = await Put(name, request);
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());

		var replacement = (await _response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
		replacement.ShouldNotBe(_ids[name], "an answered question forks (ADR-0071)");
		_ids[$"{name} (retired)"] = _ids[name];
		_ids[name] = replacement;
	}

	[Then(@"{string}'s choices depend on the question that replaced {string}")]
	public async Task ThenTheDependencyFollowsTheFork(string child,
													  string parent)
	{
		(await View(child)).GetProperty("choicesDependOnQuestionId").GetString().ShouldBe(_ids[parent]);
	}

	[Then(@"{string} is linked to that question's copy of {string}")]
	public async Task ThenTheLinkFollowsTheFork(string choice,
												string parentChoice)
	{
		ParentsOf(await View(_childName!), choice).ShouldBe([ChoiceId(await View("Make"), parentChoice)]);
	}

	[Then(@"{string} gains no revision")]
	public async Task ThenGainsNoRevision(string name)
	{
		var now = await View(name);
		now.GetProperty("id").GetString().ShouldBe(_before[name].GetProperty("id").GetString());
		now.GetProperty("revisionId").GetString().ShouldBe(_before[name].GetProperty("revisionId").GetString());
	}

	[Then(@"the question that replaced {string} depends on {string}")]
	public async Task ThenTheForkedChildKeepsItsParent(string child,
													   string parent)
	{
		(await View(child)).GetProperty("choicesDependOnQuestionId").GetString().ShouldBe(_ids[parent]);
	}

	[Then(@"its copy of {string} is linked to {string}")]
	public async Task ThenTheCopyKeepsItsLink(string choice,
											  string parentChoice)
	{
		var copy = await View(_childName!);
		ChoiceId(copy, choice).ShouldNotBe(ChoiceId(_before[_childName!], choice), "a fork's choices are new rows (ADR-0128)");
		ParentsOf(copy, choice).ShouldBe([ChoiceId(await View("Make"), parentChoice)]);
	}

	// ---- REQ-QB-191: the report form's questions ----

	[When(@"the report form loads today's questions")]
	public static void WhenTheFormLoads()
	{
		// Each Then reads the public endpoint itself.
	}

	[Then(@"the {string} question names {string} as the question its choices depend on")]
	public async Task ThenTheFormNamesTheParent(string child,
												string parent)
	{
		(await PublicView(child)).GetProperty("choicesDependOnQuestionId").GetString().ShouldBe(_ids[parent]);
	}

	[Then(@"each {string} choice names every {string} choice it is offered under")]
	public async Task ThenTheFormNamesEachLink(string child,
											   string parent)
	{
		// "Other" is offered under both makes, so one choice names two.
		var make = await View(parent);
		var admin = await View(child);
		foreach (var option in (await PublicView(child)).GetProperty("options").EnumerateArray())
		{
			var linked = option.GetProperty("parentChoiceIds").EnumerateArray().Select(id => id.GetString()!).ToArray();
			linked.ShouldNotBeEmpty();
			linked.ShouldBe(ParentsOf(admin, option.GetProperty("labelEn").GetString()!));
		}

		ParentsOf(admin, "Other").ShouldBe(Sorted(ChoiceId(make, "Niviuk"), ChoiceId(make, "Ozone")));
	}

	[Then(@"a question whose choices depend on nothing names no parent")]
	public async Task ThenAnIndependentQuestionNamesNone()
	{
		(await PublicView("Make")).GetProperty("choicesDependOnQuestionId").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	// ---- REQ-QB-192, REQ-QB-193: a reporter's typed value ----

	[When(@"a reporter answers {string} with its {string} choice and types {string} for {string}, which {string} does not offer")]
	public async Task WhenAReporterTypesUnderAChoice(string parent,
													 string parentChoice,
													 string typed,
													 string child,
													 string _)
	{
		_response = await SubmitRaw(
			Picked(parent, ChoiceId(await View(parent), parentChoice)),
			Typed(child, typed));
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[When(@"a reporter answers {string} with {string}, a value {string} does not offer and types {string} for {string}, which {string} does not offer")]
	public async Task WhenAReporterTypesUnderATypedParent(string parent,
														  string parentTyped,
														  string _,
														  string typed,
														  string child,
														  string __)
	{
		_response = await SubmitRaw(Typed(parent, parentTyped), Typed(child, typed));
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} gains a reporter-added value {string}, linked to {string}")]
	public async Task ThenTheNewValueIsLinkedToAChoice(string child,
													   string value,
													   string parentChoice)
	{
		var model = await View(child);
		Option(model, value).GetProperty("addedByReporter").GetBoolean().ShouldBeTrue();
		ParentsOf(model, value).ShouldBe([ChoiceId(await View("Make"), parentChoice)]);
	}

	[Then(@"{string} gains a reporter-added value {string}, linked to the reporter-added {string} value {string}")]
	public async Task ThenTheNewValueIsLinkedToANewValue(string child,
														 string value,
														 string parent,
														 string parentValue)
	{
		var make = await View(parent);
		Option(make, parentValue).GetProperty("addedByReporter").GetBoolean().ShouldBeTrue();
		await ThenTheNewValueIsLinkedToAChoice(child, value, parentValue);
	}

	[Then(@"the {string} answer names that value and nothing about {string}")]
	public async Task ThenTheAnswerNamesOnlyItsValue(string child,
													 string _)
	{
		var model = await View(child);
		var answer = await OnlyAnswerTo(child);
		answer.ChoiceId.ShouldBe(TinyId.Parse(ChoiceId(model, "Zeno 2")));
		answer.Value.ShouldBeNull();
		answer.TranslatedValue.ShouldBeNull();
	}

	[When(@"a reporter answers {string} with {string} and types {string} for {string}")]
	public async Task WhenAReporterTypesAnExistingWording(string parent,
														  string parentChoice,
														  string typed,
														  string child)
	{
		await Remember(child);
		_answersBefore = await AnswerIds(child);
		_response = await SubmitRaw(Picked(parent, ChoiceId(await View(parent), parentChoice)), Typed(child, typed));
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} gains no new value")]
	public async Task ThenNoValueIsAdded(string child)
	{
		var now = (await View(child)).GetProperty("options").EnumerateArray().Select(option => option.GetProperty("id").GetString()).ToList();
		var before = _before[child].GetProperty("options").EnumerateArray().Select(option => option.GetProperty("id").GetString()).ToList();
		now.ShouldBeSubsetOf(before);
	}

	// ---- REQ-QB-216 to REQ-QB-219: a reporter's typed value is matched question-wide ----

	[Given(@"the {string} question offers {string} under {string} and {string}")]
	public async Task GivenOneChoiceUnderBoth(string child,
											  string wording,
											  string firstParent,
											  string secondParent)
	{
		await Arrange("Make", "single_select", child, "autocomplete");
		await WhenAddingOneOtherUnderBoth(wording, firstParent, secondParent);
		_response = null;
		await Remember("Make", child);
	}

	[Given(@"the {string} question offers {string} under {string} only")]
	public async Task GivenOneChoiceUnderOne(string child,
											 string wording,
											 string parentChoice)
	{
		await Arrange("Make", "single_select", child, "autocomplete");
		ParentsOf(await View(child), wording).ShouldBe([ChoiceId(await View("Make"), parentChoice)]);
	}

	[Given(@"the {string} value {string} was merged into {string}, which is offered under {string} only")]
	public async Task GivenAMergedValue(string child,
										string merged,
										string target,
										string parentChoice)
	{
		await GivenOneChoiceUnderOne(child, target, parentChoice);
		await AddedByReporter(child, merged, parentChoice);
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var model = await View(child);
		var response = await _officer.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{await ChoiceIdIncludingRemoved(child, merged)}/merge", UriKind.Relative),
			new { intoId = ChoiceId(model, target) });
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
		await ApproveAll(child);
	}

	[Given(@"the {string} value {string} was removed")]
	public async Task GivenARemovedValue(string child,
										 string value)
	{
		await Arrange("Make", "single_select", child, "autocomplete");
		await AddedByReporter(child, value, "Niviuk");
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var response = await _officer.DeleteAsync(new Uri($"/api/admin/type-ahead-values/{ChoiceId(await View(child), value)}", UriKind.Relative));
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
	}

	[Then(@"the {string} answer names that {string}")]
	[Then(@"the {string} answer names {string}")]
	public async Task ThenTheAnswerNames(string child,
										 string wording)
	{
		var fresh = (await AnswerIds(child)).Except(_answersBefore).ShouldHaveSingleItem();
		fresh.ChoiceId.ShouldBe(TinyId.Parse(await ChoiceIdIncludingRemoved(child, wording)));
	}

	[Then(@"{string} gains no new value, and {string} is not flagged for review")]
	public async Task ThenNoValueAndNoFlag(string child,
										   string wording)
	{
		await ThenNoValueIsAdded(child);
		(await Row(child, wording)).NeedsReview.ShouldBeFalse();
	}

	[Then(@"{string} is offered under {string} and {string}, and is flagged for review")]
	public async Task ThenOfferedUnderBothAndFlagged(string wording,
													 string firstParent,
													 string secondParent)
	{
		var make = await View("Make");
		ParentsOf(await View(_childName!), wording).ShouldBe(Sorted(ChoiceId(make, firstParent), ChoiceId(make, secondParent)));
		(await Row(_childName!, wording)).NeedsReview.ShouldBeTrue();
	}

	[Then(@"{string} is flagged for review and still removed")]
	public async Task ThenFlaggedAndStillRemoved(string wording)
	{
		var row = await Row(_childName!, wording);
		row.NeedsReview.ShouldBeTrue();
		row.Deleted.ShouldNotBeNull();
	}

	// ---- REQ-QB-220: a reviewer sets a value's parents, never none ----

	[Given(@"a reporter added the {string} value {string}, offered under {string}")]
	public async Task GivenAReporterAddedALinkedValue(string child,
													  string value,
													  string parentChoice)
	{
		await Arrange("Make", "single_select", child, "autocomplete");
		await AddedByReporter(child, value, parentChoice);
		_answered.Add((child, ChoiceId(await View(child), value)));
		await Remember("Make", child);
	}

	[When(@"a Safety Officer offers {string} under {string} and {string}")]
	public async Task WhenASafetyOfficerOffersUnderBoth(string value,
														string firstParent,
														string secondParent)
	{
		var make = await View("Make");
		_response = await SetParents(value, ChoiceId(make, firstParent), ChoiceId(make, secondParent));
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} is offered under both, and every answer naming it still names it")]
	public async Task ThenTheValueIsUnderBoth(string value)
	{
		var make = await View("Make");
		ParentsOf(await View(_childName!), value).ShouldBe(Sorted(ChoiceId(make, "Niviuk"), ChoiceId(make, "Ozone")));
		await ThenEveryAnswerStillNamesItsChoice();
	}

	[When(@"they offer it under {string} only")]
	public async Task WhenOfferedUnderOneOnly(string parentChoice)
	{
		_response = await SetParents(_lastChoice!, ChoiceId(await View("Make"), parentChoice));
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"its {string} link is marked removed, not erased")]
	public async Task ThenTheUntickedLinkIsStamped(string parentChoice)
	{
		var link = await LinkRow(_lastChoice!, ChoiceId(await View("Make"), parentChoice));
		link.ShouldNotBeNull();
		link.Deleted.ShouldNotBeNull();
	}

	[Then(@"offering it under no parent choice is refused")]
	public async Task ThenNoParentIsRefused()
	{
		_response = await SetParents(_lastChoice!);
		(await Refused()).ShouldContain("at least one");
	}

	[Then(@"offering it under a choice of any question other than {string} is refused")]
	public async Task ThenAForeignParentIsRefused(string parent)
	{
		await Create("Elsewhere", "single_select", [("Gin", null)]);
		_response = await SetParents(_lastChoice!, ChoiceId(await View("Elsewhere"), "Gin"));
		(await Refused()).ShouldContain(Label(parent));
	}

	[Then(@"changing the parents of a {string} value that was merged into another is refused")]
	public async Task ThenAMergedValuesParentsAreRefused(string child)
	{
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var model = await View(child);
		var ikuma = ChoiceId(model, "Ikuma");
		var merged = await _officer.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{ikuma}/merge", UriKind.Relative),
			new { intoId = ChoiceId(model, "Mentor 7") });
		merged.StatusCode.ShouldBe(HttpStatusCode.NoContent, await merged.Content.ReadAsStringAsync());

		_response = await _officer.PutAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{ikuma}/parent", UriKind.Relative),
			new { parentChoiceIds = new[] { ChoiceId(await View("Make"), "Ozone") } });
		(await Refused()).ShouldContain("merged");
	}

	// ---- REQ-QB-221: a merge unions the parents ----

	[Given(@"the {string} values {string} under {string} and {string} under {string}")]
	public async Task GivenTwoValuesUnderDifferentParents(string child,
														  string first,
														  string firstParent,
														  string second,
														  string secondParent)
	{
		await Arrange("Make", "single_select", child, "autocomplete");
		await AddedByReporter(child, first, firstParent);
		await AddedByReporter(child, second, secondParent);
		_answered.Add((child, ChoiceId(await View(child), second)));
	}

	[When(@"a Safety Officer merges the value {string} into {string}")]
	public async Task WhenASafetyOfficerMergesTheValue(string source,
													   string target)
	{
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var model = await View(_childName!);
		_response = await _officer.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{ChoiceId(model, source)}/merge", UriKind.Relative),
			new { intoId = ChoiceId(model, target) });
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} is offered under {string} and {string}")]
	public async Task ThenOfferedUnderBoth(string wording,
										   string firstParent,
										   string secondParent)
	{
		var make = await View("Make");
		ParentsOf(await View(_childName!), wording).ShouldBe(Sorted(ChoiceId(make, firstParent), ChoiceId(make, secondParent)));
	}

	[Then(@"every answer naming {string} reads {string}, and none is rewritten")]
	public async Task ThenMergedAnswersReadTheSurvivor(string merged,
													   string survivor)
	{
		await ThenEveryAnswerStillNamesItsChoice();
		var row = await Row(_childName!, merged);
		row.MergedIntoChoiceId.ShouldBe(TinyId.Parse(ChoiceId(await View(_childName!), survivor)));
	}

	// ---- REQ-QB-206: grouping and form order ----	// ---- REQ-QB-206: grouping and form order ----

	[Given(@"a group question comes before the {string} question on the form")]
	public async Task GivenAGroupBeforeTheParent(string parent)
	{
		await Create("Section", "group", []);
		await Create(parent, "single_select", [("Niviuk", null), ("Ozone", null)]);
	}

	[When(@"an Administrator makes a question grouped under that group depend on {string}")]
	public async Task WhenAGroupedQuestionDependsOnALaterParent(string parent)
	{
		var request = Request("Model", "autocomplete", [Option("Mentor 7", ChoiceId(await View(parent), "Niviuk"))], _ids[parent]);
		request["groupedUnderQuestionId"] = _ids["Section"];
		_response = await Post(request);
	}

	[When(@"an Administrator groups {string} under a group question placed after {string}")]
	public async Task WhenTheParentIsGroupedAfterTheChild(string parent,
														  string _)
	{
		await Create("Later", "group", []);
		var make = await View(parent);
		var request = RequestFrom(make, options: Options(make));
		request["groupedUnderQuestionId"] = _ids["Later"];
		_response = await Put(parent, request);
	}

	// ---- REQ-SUB-114: a required child that cannot be answered yet ----

	[Given(@"a required single-select {string} question's choices depend on the {string} question, and nothing is offered under {string}")]
	public async Task GivenARequiredPickerChild(string child,
												string parent,
												string empty)
	{
		await Create(parent, "single_select", [("Niviuk", null), ("Ozone", null), (empty, null)]);
		var make = await View(parent);
		var request = Request(child, "single_select",
			[Option("Mentor 7", ChoiceId(make, "Niviuk")), Option("Rush 6", ChoiceId(make, "Ozone"))], _ids[parent]);
		request["isRequired"] = true;
		var created = await Post(request);
		created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
		_ids[child] = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
		_childName = child;
	}

	[When(@"a reporter submits a report leaving {string} unanswered, with {string} sent with no choice")]
	public async Task WhenTheRequiredChildIsSentEmptyWithoutParent(string _,
																   string child)
	{
		_response = await SubmitRaw((child, new { value = (string?)null, choices = Array.Empty<string>() }));
	}

	[When(@"a reporter submits a report answering {string} with {string}, with {string} sent with no choice")]
	public async Task WhenTheRequiredChildIsSentEmptyUnderAnEmptyParentChoice(string parent,
																			  string parentChoice,
																			  string child)
	{
		_response = await SubmitRaw(
			Picked(parent, ChoiceId(await View(parent), parentChoice)),
			(child, new { value = (string?)null, choices = Array.Empty<string>() }));
	}

	[When(@"a reporter submits a report answering {string} with {string}, after every {string} choice under {string} was removed, with {string} sent with no choice")]
	public async Task WhenTheRequiredChildIsSentEmptyUnderARemovedChoice(string parent,
																		 string parentChoice,
																		 string child,
																		 string _,
																		 string __)
	{
		// Removed choices are kept, stamped; they no longer count as offered (ADR-0095).
		var model = await View(child);
		var removed = ChoiceId(await View(parent), parentChoice);
		var kept = Options(model).Where(option => !option["parentChoiceIds"]!.AsArray().Any(id => id!.GetValue<string>() == removed)).ToArray();
		var saved = await Put(child, RequestFrom(model, options: kept));
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

		await WhenTheRequiredChildIsSentEmptyUnderAnEmptyParentChoice(parent, parentChoice, child);
	}

	[Then(@"the report is accepted with no answer to {string}")]
	public async Task ThenAcceptedWithoutTheChild(string child)
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_ids[child]);
		(await database.ReportAnswers.IgnoreQueryFilters().CountAsync(answer => answer.QuestionId == id)).ShouldBe(0);
	}

	// ---- REQ-QB-203: a parent off the form ----

	[When(@"an Administrator deactivates {string}")]
	public async Task WhenTheParentIsDeactivated(string parent)
	{
		var make = await View(parent);
		var request = RequestFrom(make, options: Options(make));
		request["isActive"] = false;
		_response = await Put(parent, request);
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"the report form names no parent for {string} and offers every {string} choice")]
	public async Task ThenTheFormFiltersNothing(string child,
												string _)
	{
		var shown = await PublicView(child);
		shown.GetProperty("choicesDependOnQuestionId").ValueKind.ShouldBe(JsonValueKind.Null);
		shown.GetProperty("options").GetArrayLength().ShouldBe(3);
	}

	[Then(@"a report answering {string} with any of its choices, and not answering {string}, is accepted")]
	public async Task ThenAnUnfilteredAnswerIsAccepted(string child,
													   string _)
	{
		(await SubmitAnswers((child, ChoiceId(await View(child), "Rush 6")))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[When(@"an Administrator deletes {string}")]
	public async Task WhenTheParentIsDeleted(string parent)
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		_response = await _admin.DeleteAsync(new Uri($"/api/admin/questions/{_ids[parent]}", UriKind.Relative));
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await _response.Content.ReadAsStringAsync());
	}

	// ---- REQ-SUB-113: the API holds a submission to the links ----

	[When(@"a reporter submits {string} answered with {string} and {string} answered with {string}")]
	public async Task WhenAMismatchIsSubmitted(string child,
											   string choice,
											   string parent,
											   string parentChoice)
	{
		await Remember(parent, child);
		_response = await SubmitAnswers((child, ChoiceId(await View(child), choice)), (parent, ChoiceId(await View(parent), parentChoice)));
	}

	[When(@"a reporter submits {string} answered with {string} and {string} left unanswered")]
	public async Task WhenAnOrphanIsSubmitted(string child,
											  string choice,
											  string parent)
	{
		await Remember(parent, child);
		_response = await SubmitAnswers((child, ChoiceId(await View(child), choice)));
	}

	[Given(@"the {string} question's choices depend on the {string} question, and {string} is offered under {string} and {string}")]
	public async Task GivenModelDependsOnMakeWithASharedChoice(string child,
															   string parent,
															   string choice,
															   string firstParent,
															   string secondParent)
	{
		await Arrange(parent, "single_select", child, "autocomplete");

		// A make nothing is offered under, for a mismatched answer.
		var make = await View(parent);
		var saved = await Put(parent, RequestFrom(make, options: [.. Options(make), Option("Gin", null)]));
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

		await WhenAddingOneOtherUnderBoth(choice, firstParent, secondParent);
		await Remember(parent, child);
	}

	[Then(@"the report is accepted")]
	public async Task ThenTheReportIsAccepted()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"the report is refused as invalid, naming {string} and {string} by key, and no report, answer, or choice is written")]
	public async Task ThenRefusedAndNothingWritten(string child,
												   string parent)
	{
		await ThenTheSubmissionIsRefused(child, parent);
		await ThenNothingIsWritten();
	}

	[Then(@"the report is refused as invalid, naming {string} and {string} by key")]
	public async Task ThenTheSubmissionIsRefused(string child,
												 string parent)
	{
		// The scenario's keys are derived from wording carrying this run's marker.
		var detail = await Refused();
		var childKey = (await View(_childName!)).GetProperty("key").GetString()!;
		var parentKey = (await View("Make")).GetProperty("key").GetString()!;
		childKey.ShouldStartWith(child);
		parentKey.ShouldStartWith(parent);
		detail.ShouldContain($"'{childKey}'");
		detail.ShouldContain($"'{parentKey}'");
	}

	[Then(@"no report, answer, or choice is written")]
	public async Task ThenNothingIsWritten()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var ids = new[] { TinyId.Parse(_ids["Make"]), TinyId.Parse(_ids[_childName!]) };

		(await database.ReportAnswers.IgnoreQueryFilters().CountAsync(answer => ids.Contains(answer.QuestionId))).ShouldBe(0);
		(await View(_childName!)).GetProperty("options").GetArrayLength().ShouldBe(_before[_childName!].GetProperty("options").GetArrayLength());
	}

	// ---- Helpers ----

	private string Label(string name)
	{
		return $"{name} {_run}";
	}

	private async Task Arrange(string parent,
							   string parentType,
							   string child,
							   string childType)
	{
		await Create(parent, parentType, parentType == "autocomplete"
			? [("Niviuk", null), ("Ozone", null), ("Nivuik", null)]
			: [("Niviuk", null), ("Ozone", null)]);

		var make = await View(parent);
		await Create(child, childType,
			[
				("Mentor 7", ChoiceId(make, "Niviuk")),
				("Ikuma", ChoiceId(make, "Niviuk")),
				("Rush 6", ChoiceId(make, "Ozone")),
			],
			_ids[parent]);

		_childName = child;
		await Remember(parent, child);
	}

	private async Task Remember(params string[] names)
	{
		foreach (var name in names)
		{
			_before[name] = await View(name);
		}
	}

	private async Task Create(string name,
							  string type,
							  (string Label, string? ParentChoiceId)[] options,
							  string? parentId = null)
	{
		var response = await Post(Request(name, type, [.. options.Select(option => Option(option.Label, option.ParentChoiceId))], parentId));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		_ids[name] = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	private JsonObject Request(string name,
							   string type,
							   JsonNode[] options,
							   string? parentId)
	{
		return new JsonObject
		{
			["type"] = type,
			["labelEn"] = Label(name),
			["labelFr"] = $"{Label(name)} (fr)",
			["isRequired"] = false,
			["isPrivate"] = false,
			["isActive"] = true,
			["choicesDependOnQuestionId"] = parentId,
			["options"] = new JsonArray(options),
		};
	}

	private static JsonObject Option(string label,
								   string? parentChoiceId)
	{
		return new JsonObject
		{
			["code"] = null,
			["labelEn"] = label,
			["labelFr"] = $"{label} (fr)",
			["parentChoiceIds"] = parentChoiceId is null ? null : new JsonArray(parentChoiceId),
		};
	}

	/// <summary>A new option offered under exactly <paramref name="parentChoiceIds" />, none included.</summary>
	private static JsonObject OptionUnder(string label,
										  params string[] parentChoiceIds)
	{
		var option = Option(label, null);
		option["parentChoiceIds"] = new JsonArray([.. parentChoiceIds.Select(id => (JsonNode)id)]);
		return option;
	}

	/// <summary>A save request carrying everything the view shows, with the dependency and options given.</summary>
	private static JsonObject RequestFrom(JsonElement view,
										  string? parentId = null,
										  JsonNode[]? options = null)
	{
		return new JsonObject
		{
			["type"] = view.GetProperty("type").GetString(),
			["labelEn"] = view.GetProperty("labelEn").GetString(),
			["labelFr"] = view.GetProperty("labelFr").GetString(),
			["isRequired"] = view.GetProperty("isRequired").GetBoolean(),
			["isPrivate"] = view.GetProperty("isPrivate").GetBoolean(),
			["isActive"] = view.GetProperty("isActive").GetBoolean(),
			["choicesDependOnQuestionId"] = parentId ?? view.GetProperty("choicesDependOnQuestionId").GetString(),
			["options"] = new JsonArray(options ?? []),
		};
	}

	/// <summary>Every option the view shows, sent back as it is, with each one named offered under that one parent choice only.</summary>
	private static JsonNode[] Options(JsonElement view,
									  params (string Label, string? ParentChoiceId)[] relinked)
	{
		return OptionsUnder(view, [.. relinked.Select(pair => (pair.Label, pair.ParentChoiceId is null ? Array.Empty<string>() : new[] { pair.ParentChoiceId }))]);
	}

	/// <summary>Every option the view shows, sent back as it is, with each one named offered under exactly its parent choices.</summary>
	private static JsonNode[] OptionsUnder(JsonElement view,
										   params (string Label, string[] ParentChoiceIds)[] relinked)
	{
		return
		[
			.. view.GetProperty("options").EnumerateArray().Select(option =>
			{
				var label = option.GetProperty("labelEn").GetString()!;
				var link = relinked.FirstOrDefault(pair => pair.Label == label);
				var parents = link.Label is null
					? option.GetProperty("parentChoiceIds").EnumerateArray().Select(id => id.GetString()!).ToArray()
					: link.ParentChoiceIds;

				return (JsonNode)new JsonObject
				{
					["code"] = option.GetProperty("code").GetString(),
					["labelEn"] = label,
					["labelFr"] = option.GetProperty("labelFr").GetString() ?? string.Empty,
					["pin"] = option.GetProperty("pin").GetString(),
					["parentChoiceIds"] = new JsonArray([.. parents.Select(id => (JsonNode)id)]),
				};
			}),
		];
	}

	private static string[] Sorted(params string[] ids)
	{
		return [.. ids.Order(StringComparer.Ordinal)];
	}

	/// <summary>Adds a new choice to the child, under whichever parent choice, and expects it refused as a repeat, naming <paramref name="named" />.</summary>
	private async Task RefusedAsRepeated(string labelEn,
										 string labelFr,
										 string named)
	{
		var model = await View(_childName!);
		var option = OptionUnder(labelEn, ChoiceId(await View("Make"), "Ozone"));
		option["code"] = $"repeat_{Guid.NewGuid():N}"[..20];
		option["labelFr"] = labelFr;
		_response = await Put(_childName!, RequestFrom(model, options: [.. Options(model), option]));
		var detail = await Refused();
		detail.ShouldContain("offered twice");
		detail.ShouldContain(named, Case.Insensitive);
	}

	private async Task OfferAlsoUnder(string choice,
									  string parentChoice)
	{
		var model = await View(_childName!);
		var parents = ParentsOf(model, choice).Append(ChoiceId(await View("Make"), parentChoice)).ToArray();
		var saved = await Put(_childName!, RequestFrom(model, options: OptionsUnder(model, (choice, parents))));
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
	}

	/// <summary>A reporter answers the parent with <paramref name="parentChoice" /> and types <paramref name="value" /> for the child.</summary>
	private async Task AddedByReporter(string child,
									   string value,
									   string parentChoice)
	{
		(await SubmitRaw(Picked("Make", ChoiceId(await View("Make"), parentChoice)), Typed(child, value))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
		_lastChoice = value;
	}

	/// <summary>Clears every review flag on the child, so a step can see a flag set again.</summary>
	private async Task ApproveAll(string child)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_ids[child]);
		await database.QuestionChoices.Where(choice => choice.QuestionId == id).ExecuteUpdateAsync(set => set.SetProperty(choice => choice.NeedsReview, false));
	}

	private async Task<Core.Features.QuestionBank.QuestionChoice> Row(string child,
																	   string wording)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_ids[child]);
		return await database.QuestionChoices.AsNoTracking().SingleAsync(choice => choice.QuestionId == id && choice.LabelEn == wording);
	}

	private async Task<(TinyId Id, TinyId? ChoiceId)[]> AnswerIds(string child)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_ids[child]);
		return [.. (await database.ReportAnswers.AsNoTracking().Where(answer => answer.QuestionId == id).Select(answer => new { answer.Id, answer.ChoiceId }).ToListAsync())
			.Select(answer => (answer.Id, answer.ChoiceId))];
	}

	private async Task<string> ChoiceIdIncludingRemoved(string child,
														string wording)
	{
		return (await Row(child, wording)).Id.Value;
	}

	private async Task<Core.Features.QuestionBank.ChoiceParentLink?> LinkRow(string wording,
																		  string parentChoiceId)
	{
		var choice = await Row(_childName!, wording);
		return choice.ParentLinks.SingleOrDefault(link => link.ParentChoiceId == TinyId.Parse(parentChoiceId));
	}

	private static IEnumerable<string> Labels(JsonElement view)
	{
		return view.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("labelEn").GetString()!);
	}

	private async Task<HttpResponseMessage> Post(JsonObject request)
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		return await _admin.PostAsJsonAsync(AdminQuestions, request);
	}

	private async Task<HttpResponseMessage> Put(string name,
												JsonObject request)
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		return await _admin.PutAsJsonAsync(new Uri($"/api/admin/questions/{_ids[name]}", UriKind.Relative), request);
	}

	private async Task<HttpResponseMessage> SetParents(string value,
													   params string[] parentChoiceIds)
	{
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		return await _officer.PutAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{ChoiceId(await View(_childName!), value)}/parent", UriKind.Relative),
			new { parentChoiceIds });
	}

	private async Task<string> Refused()
	{
		var body = await _response!.Content.ReadAsStringAsync();
		_response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
		return JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString()!;
	}

	private async Task<JsonElement> View(string name)
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var questions = await _admin.GetFromJsonAsync<JsonElement>(AdminQuestions);
		return questions.EnumerateArray().Single(question => question.GetProperty("id").GetString() == _ids[name]);
	}

	private async Task<JsonElement> PublicView(string name)
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var form = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
		return form.EnumerateArray()
			.SelectMany(question => new[] { question }.Concat(question.GetProperty("children").EnumerateArray()))
			.Single(question => question.GetProperty("id").GetString() == _ids[name]);
	}

	private static JsonElement Option(JsonElement view,
									  string label)
	{
		return view.GetProperty("options").EnumerateArray().Single(option => option.GetProperty("labelEn").GetString() == label);
	}

	private static string ChoiceId(JsonElement view,
								   string label)
	{
		return Option(view, label).GetProperty("id").GetString()!;
	}

	private static string[] ParentsOf(JsonElement view,
									  string label)
	{
		return [.. Option(view, label).GetProperty("parentChoiceIds").EnumerateArray().Select(id => id.GetString()!)];
	}

	private async Task Answered(params (string Question, string ChoiceId)[] answers)
	{
		(await SubmitAnswers(answers)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
		_answered.AddRange(answers);
	}

	private async Task<HttpResponseMessage> SubmitAnswers(params (string Question, string ChoiceId)[] answers)
	{
		return await SubmitRaw([.. answers.Select(answer => Picked(answer.Question, answer.ChoiceId))]);
	}

	private static (string Question, object Entry) Picked(string question,
												   string choiceId)
	{
		return (question, new { value = (string?)null, choices = new[] { choiceId } });
	}

	private static (string Question, object Entry) Typed(string question,
												  string text)
	{
		return (question, new { value = text, choices = (string[]?)null });
	}

	private async Task<HttpResponseMessage> SubmitRaw(params (string Question, object Entry)[] answers)
	{
		var entries = new List<object>
		{
			new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)true, choices = (string[]?)null },
		};

		foreach (var (question, entry) in answers)
		{
			var revisionId = (await View(question)).GetProperty("revisionId").GetString();
			var node = JsonSerializer.SerializeToNode(entry)!.AsObject();
			node["questionRevisionId"] = revisionId;
			entries.Add(node);
		}

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		return await reporter.PostAsJsonAsync(Submit, new { language = "en-CA", answers = entries });
	}

	private async Task<Core.Features.Reporting.ReportAnswer> OnlyAnswerTo(string question)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_ids[question]);
		return await database.ReportAnswers.AsNoTracking().SingleAsync(answer => answer.QuestionId == id);
	}
}
