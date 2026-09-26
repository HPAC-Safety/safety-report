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
///     A question's choices may depend on another question's answer (<c>REQ-QB-179</c>
///     to <c>REQ-QB-194</c>, <c>REQ-QB-203</c>, <c>REQ-SUB-113</c>, ADR-0146).
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

	// ---- REQ-QB-179: which types may take part ----

	[Given(@"a {word} question, offering ""Niviuk"" and ""Ozone"" when its type has choices")]
	public async Task GivenAParentOfType(string type)
	{
		_parentType = type;
		var takesChoices = type is "single_select" or "multi_select" or "autocomplete";
		await Create("Make", type, takesChoices ? [("Niviuk", null), ("Ozone", null)] : []);
	}

	[When(@"an Administrator makes a {word} question's choices depend on it, linking each choice to one of its choices")]
	public async Task WhenAChildOfTypeDependsOnIt(string type)
	{
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

		if (ParentOf(model, choice) != parentChoiceId)
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

	[Then(@"the dependency is refused, because {string} already depends on another question")]
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

	[Then(@"the dependency is refused, because other questions' choices already depend on {string}")]
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
		// listing that went stale before it was sent is simply taken again.
		for (var attempt = 0; attempt < 5; attempt++)
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
		}
	}

	// ---- REQ-QB-182: every choice is linked, to the parent's choices ----

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

	[When(@"an Administrator makes its choices depend on the {string} question, linking only {string} to {string}")]
	public async Task WhenLinkingOnlyOne(string parent,
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
		model.GetProperty("options").EnumerateArray().ShouldAllBe(option => option.GetProperty("parentChoiceId").ValueKind == JsonValueKind.Null);
	}

	[When(@"they link {string} to {string} and {string} to {string} in the same save")]
	public async Task WhenLinkingBoth(string first,
									  string firstParent,
									  string second,
									  string secondParent)
	{
		var model = await View(_childName!);
		var make = await View("Make");
		_response = await Put(_childName!, RequestFrom(model, _ids["Make"], Options(model, (first, ChoiceId(make, firstParent)), (second, ChoiceId(make, secondParent)))));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"the dependency is saved with both links")]
	public async Task ThenBothLinksAreSaved()
	{
		var model = await View(_childName!);
		var make = await View("Make");
		model.GetProperty("choicesDependOnQuestionId").GetString().ShouldBe(_ids["Make"]);
		ParentOf(model, "Mentor 7").ShouldBe(ChoiceId(make, "Niviuk"));
		ParentOf(model, "Rush 6").ShouldBe(ChoiceId(make, "Ozone"));
	}

	[Then(@"adding a choice to {string} without a parent choice is refused")]
	public async Task ThenAnUnlinkedChoiceIsRefused(string child)
	{
		var model = await View(child);
		_response = await Put(child, RequestFrom(model, options: [.. Options(model), Option("Buzz Z7", null)]));
		await Refused();
	}

	[Then(@"linking a choice to a choice of any question other than {string} is refused")]
	public async Task ThenAForeignLinkIsRefused(string parent)
	{
		await Create("Elsewhere", "single_select", [("Gin", null)]);
		var model = await View(_childName!);
		_response = await Put(_childName!, RequestFrom(model, options: Options(model, ("Mentor 7", ChoiceId(await View("Elsewhere"), "Gin")))));
		(await Refused()).ShouldContain(Label(parent));
	}

	// ---- REQ-QB-183: the same wording once per parent choice ----

	[When(@"an Administrator adds {string} linked to {string} and {string} linked to {string}")]
	public async Task WhenAddingTwoOthers(string first,
										  string firstParent,
										  string second,
										  string secondParent)
	{
		var model = await View(_childName!);
		var make = await View("Make");
		_response = await Put(_childName!, RequestFrom(model, options: [.. Options(model), Option(first, ChoiceId(make, firstParent)), Option(second, ChoiceId(make, secondParent))]));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} offers two {string} choices, each with its own identifier and link")]
	public async Task ThenTwoChoicesShareTheWording(string child,
													string wording)
	{
		var others = (await View(child)).GetProperty("options").EnumerateArray()
			.Where(option => option.GetProperty("labelEn").GetString() == wording)
			.ToList();

		others.Count.ShouldBe(2);
		others.Select(option => option.GetProperty("id").GetString()).Distinct().Count().ShouldBe(2);
		others.Select(option => option.GetProperty("parentChoiceId").GetString()).Distinct().Count().ShouldBe(2);
	}

	[Then(@"adding a second {string} linked to {string} is refused")]
	public async Task ThenADuplicateUnderOneParentIsRefused(string wording,
															string parentChoice)
	{
		var model = await View(_childName!);
		_response = await Put(_childName!, RequestFrom(model, options: [.. Options(model), Option(wording, ChoiceId(await View("Make"), parentChoice))]));
		(await Refused()).ShouldContain(wording);
	}

	// ---- REQ-QB-184: outside revisions ----

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
		_response = await Put(_childName!, RequestFrom(model, options: Options(model, ("Rush 6", ChoiceId(await View("Make"), "Niviuk")))));
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
		ParentOf(await View(_childName!), "Rush 6").ShouldBe(ChoiceId(await View("Make"), "Niviuk"));
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
			option!["parentChoiceId"] = null;
		}

		_response = await Put(child, request);
		_response.StatusCode.ShouldBe(HttpStatusCode.OK, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"each {string} choice keeps its link")]
	public async Task ThenEveryLinkIsKept(string child)
	{
		var model = await View(child);
		model.GetProperty("choicesDependOnQuestionId").ValueKind.ShouldBe(JsonValueKind.Null);
		ParentOf(model, "Mentor 7").ShouldBe(ChoiceId(await View("Make"), "Niviuk"));
		ParentOf(model, "Rush 6").ShouldBe(ChoiceId(await View("Make"), "Ozone"));
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

	// ---- REQ-QB-186: a linked parent choice is not removed ----

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

	[Then(@"the removal is refused, naming the {string} question")]
	public async Task ThenTheRemovalIsRefused(string child)
	{
		(await Refused()).ShouldContain(Label(child));
	}

	[Then(@"{string} is still offered")]
	public async Task ThenStillOffered(string parentChoice)
	{
		ChoiceId(await View("Make"), parentChoice).ShouldNotBeNull();
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
		ParentOf(await View(_childName!), choice).ShouldBe(ChoiceId(await View("Make"), parentChoice));
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
		ParentOf(await View(_childName!), choice).ShouldBe(ChoiceId(await View("Make"), parentChoice));
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
		ParentOf(copy, choice).ShouldBe(ChoiceId(await View("Make"), parentChoice));
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

	[Then(@"each {string} choice names the {string} choice it is linked to")]
	public async Task ThenTheFormNamesEachLink(string child,
											   string parent)
	{
		var admin = await View(child);
		foreach (var option in (await PublicView(child)).GetProperty("options").EnumerateArray())
		{
			var linked = option.GetProperty("parentChoiceId").GetString();
			linked.ShouldNotBeNull();
			linked.ShouldBe(ParentOf(admin, option.GetProperty("labelEn").GetString()!));
		}
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
		ParentOf(model, value).ShouldBe(ChoiceId(await View("Make"), parentChoice));
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

	[Given(@"the {string} question offers {string} linked to {string} and {string} linked to {string}")]
	public async Task GivenTwoOthers(string child,
									 string first,
									 string firstParent,
									 string second,
									 string secondParent)
	{
		await Arrange("Make", "single_select", child, "autocomplete");
		await WhenAddingTwoOthers(first, firstParent, second, secondParent);
		_response = null;
		await Remember("Make", child);
	}

	[When(@"a reporter answers {string} with {string} and types {string} for {string}")]
	public async Task WhenAReporterTypesAnExistingWording(string parent,
														  string parentChoice,
														  string typed,
														  string child)
	{
		_response = await SubmitRaw(Picked(parent, ChoiceId(await View(parent), parentChoice)), Typed(child, typed));
		_response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"the {string} answer names the {string} linked to {string}")]
	public async Task ThenTheAnswerNamesTheScopedChoice(string child,
														string wording,
														string parentChoice)
	{
		var model = await View(child);
		var ozone = ChoiceId(await View("Make"), parentChoice);
		var expected = model.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("labelEn").GetString() == wording && option.GetProperty("parentChoiceId").GetString() == ozone)
			.GetProperty("id").GetString();

		(await OnlyAnswerTo(child)).ChoiceId.ShouldBe(TinyId.Parse(expected!));
	}

	[Then(@"{string} gains no new value")]
	public async Task ThenNoValueIsAdded(string child)
	{
		(await View(child)).GetProperty("options").GetArrayLength().ShouldBe(_before[child].GetProperty("options").GetArrayLength());
	}

	// ---- REQ-QB-194: a reviewer changes a link ----

	[Given(@"a reporter added the {string} value {string}, linked to {string}")]
	public async Task GivenAReporterAddedALinkedValue(string child,
													  string value,
													  string parentChoice)
	{
		await Arrange("Make", "single_select", child, "autocomplete");
		(await SubmitRaw(Picked("Make", ChoiceId(await View("Make"), parentChoice)), Typed(child, value))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
		_answered.Add((child, ChoiceId(await View(child), value)));
		await Remember("Make", child);
	}

	[When(@"a Safety Officer links {string} to {string}")]
	public async Task WhenASafetyOfficerRelinks(string value,
												string parentChoice)
	{
		_response = await Relink(value, ChoiceId(await View("Make"), parentChoice));
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"{string} is linked to {string}, and every answer naming it still names it")]
	public async Task ThenTheValueIsRelinked(string value,
											 string parentChoice)
	{
		ParentOf(await View(_childName!), value).ShouldBe(ChoiceId(await View("Make"), parentChoice));
		await ThenEveryAnswerStillNamesItsChoice();
	}

	[Then(@"clearing its link is refused")]
	public async Task ThenClearingIsRefused()
	{
		_response = await Relink("Zeno 2", null);
		await Refused();
	}

	[Then(@"linking it to a choice of any question other than {string} is refused")]
	public async Task ThenAForeignRelinkIsRefused(string parent)
	{
		await Create("Elsewhere", "single_select", [("Gin", null)]);
		_response = await Relink("Zeno 2", ChoiceId(await View("Elsewhere"), "Gin"));
		(await Refused()).ShouldContain(Label(parent));
	}

	[Then(@"merging {string} into a {string} value linked to another {string} choice is refused")]
	public async Task ThenACrossParentMergeIsRefused(string value,
													 string child,
													 string _)
	{
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var model = await View(child);
		_response = await _officer.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{ChoiceId(model, value)}/merge", UriKind.Relative),
			new { intoId = ChoiceId(model, "Rush 6") });
		(await Refused()).ShouldContain("different choices of the parent question");
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

	[Then(@"the API refuses the submission, naming {string} and {string} by key")]
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
			["parentChoiceId"] = parentChoiceId,
		};
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

	/// <summary>Every option the view shows, sent back as it is, with the parent choices given re-linked.</summary>
	private static JsonNode[] Options(JsonElement view,
									  params (string Label, string? ParentChoiceId)[] relinked)
	{
		return
		[
			.. view.GetProperty("options").EnumerateArray().Select(option =>
			{
				var label = option.GetProperty("labelEn").GetString()!;
				var link = relinked.FirstOrDefault(pair => pair.Label == label);

				return (JsonNode)new JsonObject
				{
					["code"] = option.GetProperty("code").GetString(),
					["labelEn"] = label,
					["labelFr"] = option.GetProperty("labelFr").GetString() ?? string.Empty,
					["pin"] = option.GetProperty("pin").GetString(),
					["parentChoiceId"] = link.Label is null ? option.GetProperty("parentChoiceId").GetString() : link.ParentChoiceId,
				};
			}),
		];
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

	private async Task<HttpResponseMessage> Relink(string value,
												   string? parentChoiceId)
	{
		_officer ??= await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		return await _officer.PutAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{ChoiceId(await View(_childName!), value)}/parent", UriKind.Relative),
			new { parentChoiceId });
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

	private static string? ParentOf(JsonElement view,
									string label)
	{
		return Option(view, label).GetProperty("parentChoiceId").GetString();
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
