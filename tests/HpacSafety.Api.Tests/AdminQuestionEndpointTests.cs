using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The admin question-authoring endpoints, against a real PostgreSQL container.
/// </summary>
/// <remarks>
///     Every question here is synthetic. The question bank is form definition, not
///     report content, so nothing in these tests touches a report, an answer, or
///     anything a reporter wrote.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class AdminQuestionEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBearerToken_WhenQuestionsAreListed_ThenApiRefuses()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(Questions);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenMemberSession_WhenQuestionIsCreated_ThenListedWithFirstRevision()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("wind_direction");

		// When
		using var created = await client.PostAsJsonAsync(Questions, Draft(key, "short_text"));

		// Then
		created.StatusCode.ShouldBe(HttpStatusCode.Created);

		var question = await created.Content.ReadFromJsonAsync<JsonElement>();
		question.GetProperty("key").GetString().ShouldBe(key);
		question.GetProperty("revisionNumber").GetInt32().ShouldBe(1);
		question.GetProperty("id").GetString()!.Length.ShouldBe(11);
	}

	[Fact]
	public async Task GivenExistingQuestion_WhenEdited_ThenNewRevisionIsWrittenRatherThanPatch()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("surface_wind");
		var created = await Create(client, Draft(key, "short_text"));
		var id = created.GetProperty("id").GetString()!;

		// When
		var edit = Draft(key, "short_text") with { LabelEn = "Reworded question", IsRequired = true };
		using var response = await client.PutAsJsonAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative), edit);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var revised = await response.Content.ReadFromJsonAsync<JsonElement>();
		revised.GetProperty("revisionNumber").GetInt32().ShouldBe(2);
		revised.GetProperty("labelEn").GetString().ShouldBe("Reworded question");
		revised.GetProperty("isRequired").GetBoolean().ShouldBeTrue();
		revised.GetProperty("revisionId").GetString().ShouldNotBe(created.GetProperty("revisionId").GetString());
	}

	[Fact]
	public async Task GivenNonBooleanQuestion_WhenAnotherDependsOn_ThenApiRejectsDependency()
	{
		// Given
		using var client = await SignedIn();
		var parent = await Create(client, Draft(UniqueKey("glider_make"), "short_text"));

		// When
		var child = Draft(UniqueKey("glider_detail"), "long_text") with
		{
			DependsOnQuestionId = parent.GetProperty("id").GetString(),
		};

		using var response = await client.PostAsJsonAsync(Questions, child);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("yes/no");
	}

	[Fact]
	public async Task GivenBooleanQuestion_WhenAnotherDependsOn_ThenDependencyIsStored()
	{
		// Given
		using var client = await SignedIn();
		var parent = await Create(client, Draft(UniqueKey("were_you_injured"), "yes_no"));
		var parentId = parent.GetProperty("id").GetString();

		// When
		var child = Draft(UniqueKey("injury_detail"), "long_text") with { DependsOnQuestionId = parentId };
		var created = await Create(client, child);

		// Then
		created.GetProperty("dependsOnQuestionId").GetString().ShouldBe(parentId);
	}

	private static async Task<JsonElement> CreatePilotType(HttpClient client)
	{
		return await Create(
			client,
			Draft(UniqueKey("pilot_type"), "single_select") with
			{
				Options =
				[
					new Option("hang_glider", "Hang glider", "Deltaplane"),
					new Option("paraglider", "Paraglider", "Parapente"),
				],
			});
	}

	[Fact]
	public async Task GivenSingleSelectQuestion_WhenAnotherDependsOnItsOption_ThenDependencyIsStored()
	{
		// Given
		using var client = await SignedIn();
		var parent = await CreatePilotType(client);
		var parentId = parent.GetProperty("id").GetString();

		// When
		var child = Draft(UniqueKey("rating"), "short_text") with
		{
			DependsOnQuestionId = parentId,
			DependsOnChoiceId = ChoiceIdOf(parent, "hang_glider"),
		};
		var created = await Create(client, child);

		// Then — the dependency names the parent's choice by ID (ADR-0128)
		created.GetProperty("dependsOnQuestionId").GetString().ShouldBe(parentId);
		created.GetProperty("dependsOnChoiceId").GetString().ShouldBe(ChoiceIdOf(parent, "hang_glider"));
	}

	[Fact]
	public async Task GivenSingleSelectQuestion_WhenAnotherDependsOnAnUnofferedOption_ThenApiRejectsDependency()
	{
		// Given
		using var client = await SignedIn();
		var parent = await CreatePilotType(client);
		var another = await CreatePilotType(client);

		// When — another question's choice
		var child = Draft(UniqueKey("rating"), "short_text") with
		{
			DependsOnQuestionId = parent.GetProperty("id").GetString(),
			DependsOnChoiceId = ChoiceIdOf(another, "hang_glider"),
		};
		using var response = await client.PostAsJsonAsync(Questions, child);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("does not currently offer");
	}

	[Fact]
	public async Task GivenSingleSelectQuestion_WhenAnotherDependsWithNoOption_ThenApiRejectsDependency()
	{
		// Given
		using var client = await SignedIn();
		var parent = await CreatePilotType(client);

		// When
		var child = Draft(UniqueKey("rating"), "short_text") with
		{
			DependsOnQuestionId = parent.GetProperty("id").GetString(),
		};
		using var response = await client.PostAsJsonAsync(Questions, child);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenSharedChoiceListsAreGone_WhenTheirEndpointIsCalled_ThenApiReturnsNotFound()
	{
		// Given — each question owns its choices; there are no shared lists (ADR-0095)
		using var client = await SignedIn();

		// When
		using var response = await client.GetAsync(new Uri("/api/admin/option-sets", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenQuestionDependsOnAChoice_WhenParentIsSavedWithoutIt_ThenApiRejectsNamingTheDependent()
	{
		// Given
		using var client = await SignedIn();
		var parentDraft = Draft(UniqueKey("aircraft"), "single_select") with
		{
			Options = [new Option(null, "Hang glider", "Deltaplane"), new Option(null, "Paraglider", "Parapente")],
		};
		var parent = await Create(client, parentDraft);
		var parentId = parent.GetProperty("id").GetString()!;
		await Create(client, Draft(UniqueKey("wing_rating"), "short_text") with
		{
			LabelEn = "Wing rating",
			DependsOnQuestionId = parentId,
			DependsOnChoiceId = ChoiceIdOf(parent, "paraglider"),
		});

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{parentId}", UriKind.Relative),
			parentDraft with { Options = [new Option("hang_glider", "Hang glider", "Deltaplane")] });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("Wing rating");
	}

	[Fact]
	public async Task GivenAConditionOnAPickerOption_WhenTheOptionIsReplaced_ThenTheConditionShowsTheReplacementAndResavingChangesNothing()
	{
		// Given
		using var client = await SignedIn();
		var parentDraft = Draft(UniqueKey("aircraft"), "single_select") with
		{
			Options = [new Option(null, "Hang glider", "Deltaplane"), new Option(null, "Paraglider", "Parapente")],
		};
		var parent = await Create(client, parentDraft);
		var parentId = parent.GetProperty("id").GetString()!;
		var childDraft = Draft(UniqueKey("wing_rating"), "short_text") with
		{
			DependsOnQuestionId = parentId,
			DependsOnChoiceId = ChoiceIdOf(parent, "paraglider"),
		};
		var child = await Create(client, childDraft);
		var childId = child.GetProperty("id").GetString()!;

		// When — the option is replaced, not fixed (ADR-0128)
		using var replaced = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{parentId}", UriKind.Relative),
			parentDraft with
			{
				Options = [new Option("hang_glider", "Hang glider", "Deltaplane"), new Option("paraglider", "Paraglider (solo)", "Parapente (solo)", Replace: true)],
			});
		replaced.StatusCode.ShouldBe(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());
		var afterReplace = await replaced.Content.ReadFromJsonAsync<JsonElement>();

		// Then — the screen shows the condition on the replacement
		var shown = (await List(client)).Single(question => question.GetProperty("id").GetString() == childId);
		shown.GetProperty("dependsOnChoiceId").GetString().ShouldBe(ChoiceIdOf(afterReplace, "paraglider_solo"));
		using var reader = _factory.CreateClient();
		(await reader.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/questions/", UriKind.Relative))).EnumerateArray()
			.Single(question => question.GetProperty("id").GetString() == childId)
			.GetProperty("dependsOnChoiceId").GetString().ShouldBe(ChoiceIdOf(afterReplace, "paraglider_solo"));

		// When — the condition is saved back as the screen shows it
		using var resaved = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{childId}", UriKind.Relative),
			childDraft with { DependsOnChoiceId = ChoiceIdOf(afterReplace, "paraglider_solo") });

		// Then — nothing about the question changed, so nothing is revised
		resaved.StatusCode.ShouldBe(HttpStatusCode.OK, await resaved.Content.ReadAsStringAsync());
		(await resaved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionNumber").GetInt32().ShouldBe(1);
	}

	[Fact]
	public async Task GivenAConditionNamingAMalformedChoiceId_WhenSaved_ThenApiRefuses()
	{
		// Given
		using var client = await SignedIn();
		var parent = await CreatePilotType(client);

		// When
		using var response = await client.PostAsJsonAsync(Questions, Draft(UniqueKey("rating"), "short_text") with
		{
			DependsOnQuestionId = parent.GetProperty("id").GetString(),
			DependsOnChoiceId = "not-a-choice-id",
		});

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenACondition_WhenSavedNamingAnotherChoice_ThenTheConditionChanges()
	{
		// Given
		using var client = await SignedIn();
		var parent = await CreatePilotType(client);
		var draft = Draft(UniqueKey("rating"), "short_text") with
		{
			DependsOnQuestionId = parent.GetProperty("id").GetString(),
			DependsOnChoiceId = ChoiceIdOf(parent, "hang_glider"),
		};
		var child = await Create(client, draft);

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{child.GetProperty("id").GetString()}", UriKind.Relative),
			draft with { DependsOnChoiceId = ChoiceIdOf(parent, "paraglider") });

		// Then — a different condition is a new revision
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
		saved.GetProperty("dependsOnChoiceId").GetString().ShouldBe(ChoiceIdOf(parent, "paraglider"));
		saved.GetProperty("revisionNumber").GetInt32().ShouldBe(2);
	}

	[Fact]
	public async Task GivenATypeAheadValue_WhenAnAdministratorAsksToReplaceIt_ThenApiRefuses()
	{
		// Given — a type-ahead value is corrected in place, never replaced (ADR-0129)
		using var client = await SignedIn();
		var draft = Draft(UniqueKey("launch"), "autocomplete") with { Options = [new Option(null, "Mount 7", "Mont 7")] };
		var created = await Create(client, draft);

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{created.GetProperty("id").GetString()}", UriKind.Relative),
			draft with { Options = [new Option("mount_7", "Mount Seven", "Mont Sept", Replace: true)] });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("corrected in place");
	}

	[Fact]
	public async Task GivenAPickerOption_WhenReplacedWithWordingTheQuestionAlreadyHad_ThenApiRefuses()
	{
		// Given
		using var client = await SignedIn();
		var draft = Draft(UniqueKey("wing"), "single_select") with
		{
			Options = [new Option(null, "Hang glider", "Deltaplane"), new Option(null, "Paraglider", "Parapente")],
		};
		var created = await Create(client, draft);

		// When — a replacement must be a new option, not one the question has
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{created.GetProperty("id").GetString()}", UriKind.Relative),
			draft with { Options = [new Option("hang_glider", "Paraglider", "Parapente", Replace: true), new Option("paraglider", "Paraglider", "Parapente")] });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenQuestionWithChoices_WhenOnlyItsChoicesAreEdited_ThenNoRevisionIsCreated()
	{
		// Given
		using var client = await SignedIn();
		var draft = Draft(UniqueKey("launch"), "single_select") with
		{
			Options = [new Option(null, "Coopers", "Coopers"), new Option(null, "Woodside", "Woodside")],
		};
		var created = await Create(client, draft);
		var id = created.GetProperty("id").GetString()!;

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative),
			draft with { Options = [new Option("woodside", "Woodside Hill", "Colline Woodside"), new Option(null, "Mara", "Mara")] });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
		saved.GetProperty("id").GetString().ShouldBe(id);
		saved.GetProperty("revisionNumber").GetInt32().ShouldBe(1);
		saved.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("code").GetString())
			.ShouldBe(["woodside", "mara"], ignoreOrder: true);
	}

	[Fact]
	public async Task GivenQuestionWithChoices_WhenEditSendsNoChoiceList_ThenItsChoicesAreLeftAsTheyAre()
	{
		// Given
		using var client = await SignedIn();
		var draft = Draft(UniqueKey("launch_unchanged"), "single_select") with
		{
			Options = [new Option(null, "Coopers", "Coopers")],
		};
		var created = await Create(client, draft);
		var id = created.GetProperty("id").GetString()!;

		// When — a client that sends no list at all says nothing about the choices
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative),
			new { type = "single_select", labelEn = "Reworded", labelFr = "Reformulé", isRequired = false, isPrivate = true, isActive = true });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
		saved.GetProperty("options").EnumerateArray().Single().GetProperty("code").GetString().ShouldBe("coopers");
	}

	[Fact]
	public async Task GivenSingleSelectWithNoChoice_WhenCreated_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(Questions, Draft(UniqueKey("aircraft"), "single_select"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("needs at least one choice");
	}

	[Fact]
	public async Task GivenTypeAheadWithNoChoice_WhenCreated_ThenCreated()
	{
		// Given — reporters fill a type-ahead (ADR-0063)
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(Questions, Draft(UniqueKey("site"), "autocomplete"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task GivenConsentQuestion_WhenEditedToNonPrivate_ThenApiRejects()
	{
		// Given — the seeded system question
		using var client = await SignedIn();
		var listed = await List(client);
		var consent = listed.FirstOrDefault(question => question.GetProperty("isSystem").GetBoolean());

		if (consent.ValueKind == JsonValueKind.Undefined)
		{
			return; // No seeded consent question in this database; nothing to assert.
		}

		// When
		var id = consent.GetProperty("id").GetString();
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative),
			new
			{
				type = "yes_no",
				labelEn = consent.GetProperty("labelEn").GetString(),
				labelFr = consent.GetProperty("labelFr").GetString(),
				isRequired = true,
				isPrivate = false,
				isActive = true,
			});

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("always private");
	}

	// -------------------------------------------- statement/group (ADR-0076) --

	private static SaveQuestion NoAnswerDraft(string key,
											  string type)
	{
		return Draft(key, type) with { IsRequired = false, IsPrivate = false };
	}

	[Fact]
	public async Task GivenStatementType_WhenCreatedWithoutRequiredOrPrivate_ThenCreated()
	{
		// Given
		using var client = await SignedIn();

		// When
		var created = await Create(client, NoAnswerDraft(UniqueKey("intro"), "statement"));

		// Then
		created.GetProperty("type").GetString().ShouldBe("statement");
		created.GetProperty("isRequired").GetBoolean().ShouldBeFalse();
		created.GetProperty("isPrivate").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task GivenStatementType_WhenCreatedRequired_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(
			Questions, NoAnswerDraft(UniqueKey("intro"), "statement") with { IsRequired = true });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("cannot be marked required or private");
	}

	[Fact]
	public async Task GivenGroupType_WhenCreatedPrivate_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(
			Questions, NoAnswerDraft(UniqueKey("aircraft"), "group") with { IsPrivate = true });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("cannot be marked required or private");
	}

	[Fact]
	public async Task GivenNoAnswerType_WhenMadeConditionalOnAnother_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var parent = await Create(client, Draft(UniqueKey("were_you_injured"), "yes_no"));

		// When
		var child = NoAnswerDraft(UniqueKey("heading"), "statement") with
		{
			DependsOnQuestionId = parent.GetProperty("id").GetString(),
		};
		using var response = await client.PostAsJsonAsync(Questions, child);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("collects no answer and cannot be made conditional");
	}

	[Fact]
	public async Task GivenGroupQuestion_WhenAnotherIsGroupedUnderIt_ThenGroupingIsStored()
	{
		// Given
		using var client = await SignedIn();
		var group = await Create(client, NoAnswerDraft(UniqueKey("aircraft"), "group"));
		var groupId = group.GetProperty("id").GetString();

		// When
		var child = Draft(UniqueKey("manufacturer"), "short_text") with { GroupedUnderQuestionId = groupId };
		var created = await Create(client, child);

		// Then
		created.GetProperty("groupedUnderQuestionId").GetString().ShouldBe(groupId);
	}

	[Fact]
	public async Task GivenNonGroupQuestion_WhenAnotherIsGroupedUnderIt_ThenApiRejectsGrouping()
	{
		// Given
		using var client = await SignedIn();
		var notAGroup = await Create(client, Draft(UniqueKey("manufacturer"), "short_text"));

		// When
		var child = Draft(UniqueKey("model"), "short_text") with
		{
			GroupedUnderQuestionId = notAGroup.GetProperty("id").GetString(),
		};
		using var response = await client.PostAsJsonAsync(Questions, child);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("group question");
	}

	[Fact]
	public async Task GivenTwoGroupQuestions_WhenOneIsGroupedUnderTheOther_ThenApiRejectsNesting()
	{
		// Given
		using var client = await SignedIn();
		var outer = await Create(client, NoAnswerDraft(UniqueKey("form"), "group"));
		var inner = await Create(client, NoAnswerDraft(UniqueKey("aircraft"), "group"));

		// When
		var edit = NoAnswerDraft(UniqueKey("aircraft"), "group") with
		{
			GroupedUnderQuestionId = outer.GetProperty("id").GetString(),
		};
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{inner.GetProperty("id").GetString()}", UriKind.Relative), edit);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("cannot itself be grouped");
	}

	[Fact]
	public async Task GivenQuestion_WhenGroupedUnderItself_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var group = await Create(client, NoAnswerDraft(UniqueKey("aircraft"), "group"));
		var groupId = group.GetProperty("id").GetString();

		// When
		var edit = NoAnswerDraft(UniqueKey("aircraft"), "group") with { GroupedUnderQuestionId = groupId };
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{groupId}", UriKind.Relative), edit);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	// ------------------------------------- multi-select reporter additions (ADR-0077) --

	[Fact]
	public async Task GivenSeveralQuestions_WhenTheyAreRearranged_ThenEachMovedOneGainsRevision()
	{
		// Given
		using var client = await SignedIn();
		var first = await Create(client, Draft(UniqueKey("first_question"), "short_text"));
		var second = await Create(client, Draft(UniqueKey("second_question"), "short_text"));

		var firstId = first.GetProperty("id").GetString()!;
		var secondId = second.GetProperty("id").GetString()!;

		var before = await List(client);
		var others = before
			.Select(question => question.GetProperty("id").GetString()!)
			.Where(id => id != firstId && id != secondId)
			.ToList();

		// When the two are put at the front, swapped
		var arranged = new List<string> { secondId, firstId };
		arranged.AddRange(others);

		using var response = await client.PostAsJsonAsync(
			new Uri("/api/admin/questions/order", UriKind.Relative), new Reorder(arranged));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var reordered = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
		reordered[0].GetProperty("id").GetString().ShouldBe(secondId);
		reordered[1].GetProperty("id").GetString().ShouldBe(firstId);
		reordered[0].GetProperty("revisionNumber").GetInt32().ShouldBe(2);

		reordered
			.Select(question => question.GetProperty("displayOrder").GetInt32())
			.Distinct()
			.Count()
			.ShouldBe(reordered.Count);
	}

	[Fact]
	public async Task GivenQuestion_WhenDeleted_ThenDisappearsFromListWithoutBeingErased()
	{
		// Given
		using var client = await SignedIn();
		var created = await Create(client, Draft(UniqueKey("retired_question"), "short_text"));
		var id = created.GetProperty("id").GetString()!;

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var listed = await List(client);
		listed.ShouldNotContain(question => question.GetProperty("id").GetString() == id);
	}

	[Fact]
	public async Task GivenAnUnreferencedSupersededRevision_WhenDeleted_ThenStampedRatherThanRemoved()
	{
		// Given — an edit nobody answered leaves revision 1 as unreferenced history
		using var client = await SignedIn();
		var key = UniqueKey("wind_direction");
		var created = await Create(client, Draft(key, "short_text"));
		var questionId = created.GetProperty("id").GetString()!;
		var oldRevisionId = created.GetProperty("revisionId").GetString()!;

		await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{questionId}", UriKind.Relative),
			Draft(key, "short_text") with { LabelEn = "Reworded" });

		// When
		using var response = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{questionId}/revisions/{oldRevisionId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		// And the question and its current (new) revision are untouched
		var listed = await List(client);
		var stillListed = listed.Single(question => question.GetProperty("id").GetString() == questionId);
		stillListed.GetProperty("revisionNumber").GetInt32().ShouldBe(2);
	}

	[Fact]
	public async Task GivenTheCurrentRevision_WhenDeleted_ThenApiRefuses()
	{
		// Given — a question with only one revision, which is also the current one
		using var client = await SignedIn();
		var created = await Create(client, Draft(UniqueKey("single_revision"), "short_text"));
		var questionId = created.GetProperty("id").GetString()!;
		var revisionId = created.GetProperty("revisionId").GetString()!;

		// When
		using var response = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{questionId}/revisions/{revisionId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenARevisionDeletion_WhenSucceeds_ThenAContentFreeAuditRowIsWritten()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("audited_revision");
		var created = await Create(client, Draft(key, "short_text"));
		var questionId = created.GetProperty("id").GetString()!;
		var oldRevisionId = created.GetProperty("revisionId").GetString()!;

		await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{questionId}", UriKind.Relative),
			Draft(key, "short_text") with { LabelEn = "Reworded" });

		// When
		using var response = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{questionId}/revisions/{oldRevisionId}", UriKind.Relative));
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		// Then
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog.SingleAsync(e =>
			e.Action == AuditAction.DeletedQuestionRevision && e.TargetId == TinyId.Parse(oldRevisionId));

		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		entry.Detail.ShouldBeNull();
	}

	[Fact]
	public async Task GivenANonAdministrator_WhenARevisionIsDeleted_ThenApiRefuses()
	{
		// Given
		using var admin = await SignedIn();
		var created = await Create(admin, Draft(UniqueKey("member_only"), "short_text"));
		var questionId = created.GetProperty("id").GetString()!;
		var revisionId = created.GetProperty("revisionId").GetString()!;

		using var member = await SignedIn(MemberRole.User);

		// When
		using var response = await member.DeleteAsync(
			new Uri($"/api/admin/questions/{questionId}/revisions/{revisionId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenIdNamesNoRevision_WhenDeleted_ThenApiReturnsNotFound(string revisionId)
	{
		// Given
		using var client = await SignedIn();
		var created = await Create(client, Draft(UniqueKey("no_such_revision"), "short_text"));
		var questionId = created.GetProperty("id").GetString()!;

		// When
		using var response = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{questionId}/revisions/{revisionId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenARevisionOfAnotherQuestion_WhenDeleted_ThenApiReturnsNotFound()
	{
		// Given
		using var client = await SignedIn();
		var questionA = await Create(client, Draft(UniqueKey("question_a"), "short_text"));
		var questionB = await Create(client, Draft(UniqueKey("question_b"), "short_text"));
		var revisionOfB = questionB.GetProperty("revisionId").GetString()!;

		// When — B's revision named under A's question id
		using var response = await client.DeleteAsync(new Uri(
			$"/api/admin/questions/{questionA.GetProperty("id").GetString()}/revisions/{revisionOfB}",
			UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenUnknownType_WhenQuestionIsCreated_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(Questions, Draft(UniqueKey("odd"), "telepathy"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenNoKey_WhenQuestionIsCreated_ThenKeyIsDerivedFromEnglishWording()
	{
		// Given
		using var client = await SignedIn();
		var marker = Guid.NewGuid().ToString("N")[..8];

		// When
		var created = await Create(client, Draft(" ", "short_text") with { LabelEn = $"Wind at launch {marker}?" });

		// Then
		created.GetProperty("key").GetString().ShouldBe($"wind_at_launch_{marker}");
	}

	[Fact]
	public async Task GivenWordingAlreadyUsed_WhenQuestionIsCreatedWithoutKey_ThenKeyIsSuffixed()
	{
		// Given
		using var client = await SignedIn();
		var wording = $"Launch site {Guid.NewGuid():N}";
		var first = await Create(client, Draft(null, "short_text") with { LabelEn = wording });

		// When
		var second = await Create(client, Draft(null, "short_text") with { LabelEn = wording });

		// Then
		second.GetProperty("key").GetString().ShouldBe($"{first.GetProperty("key").GetString()}_2");
	}

	[Fact]
	public async Task GivenRetiredQuestionHoldsWording_WhenQuestionIsCreatedWithoutKey_ThenRetiredKeyIsNotReused()
	{
		// Given
		using var client = await SignedIn();
		var wording = $"Landing field {Guid.NewGuid():N}";
		var retired = await Create(client, Draft(null, "short_text") with { LabelEn = wording });
		using var deleted = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{retired.GetProperty("id").GetString()}", UriKind.Relative));
		deleted.EnsureSuccessStatusCode();

		// When
		var created = await Create(client, Draft(null, "short_text") with { LabelEn = wording });

		// Then
		created.GetProperty("key").GetString().ShouldBe($"{retired.GetProperty("key").GetString()}_2");
	}

	[Fact]
	public async Task GivenRetiredQuestionHoldsKey_WhenQuestionIsCreatedWithThatKey_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("retired");
		var retired = await Create(client, Draft(key, "short_text"));
		using var deleted = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{retired.GetProperty("id").GetString()}", UriKind.Relative));
		deleted.EnsureSuccessStatusCode();

		// When
		using var response = await client.PostAsJsonAsync(Questions, Draft(key, "short_text"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenKeyAlreadyInUse_WhenQuestionIsCreated_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("duplicate");
		await Create(client, Draft(key, "short_text"));

		// When
		using var response = await client.PostAsJsonAsync(Questions, Draft(key, "short_text"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenQuestionNamesItself_WhenEdited_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("self_referential");
		var created = await Create(client, Draft(key, "yes_no"));
		var id = created.GetProperty("id").GetString()!;

		// When
		var edit = Draft(key, "yes_no") with { DependsOnQuestionId = id };
		using var response = await client.PutAsJsonAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative), edit);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenIdNamesNoQuestion_WhenEdited_ThenApiReturnsNotFound(string id)
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative), Draft(UniqueKey("absent"), "short_text"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenIdNamesNoQuestion_WhenDeleted_ThenApiReturnsNotFound(string id)
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenUnknownType_WhenQuestionIsEdited_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("retyped");
		var created = await Create(client, Draft(key, "short_text"));
		var id = created.GetProperty("id").GetString()!;

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative), Draft(key, "telepathy"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenPublicationConsent_WhenDeleted_ThenApiRefuses()
	{
		// Given — the seeded system question, which nothing may remove
		using var client = await SignedIn();
		var listed = await List(client);
		var consent = listed.FirstOrDefault(question => question.GetProperty("isSystem").GetBoolean());

		if (consent.ValueKind == JsonValueKind.Undefined)
		{
			return; // No seeded consent question in this database; nothing to assert.
		}

		// When
		var id = consent.GetProperty("id").GetString();
		using var response = await client.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenArrangementNamingUnknownQuestion_WhenApplied_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/admin/questions/order", UriKind.Relative), new Reorder(["AAAAAAAAAAA"]));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenArrangementOmitsQuestion_WhenApplied_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var created = await Create(client, Draft(UniqueKey("only_one"), "short_text"));

		// When — a partial arrangement would leave every omitted question adrift
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/admin/questions/order", UriKind.Relative),
			new Reorder([created.GetProperty("id").GetString()!]));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}


	[Fact]
	public async Task GivenOptionsWithoutCodes_WhenQuestionIsCreated_ThenCodesAreDerivedFromEnglishWording()
	{
		// Given
		using var client = await SignedIn();

		// When
		var question = await Create(client, Draft(UniqueKey("launch_site"), "single_select") with
		{
			Options = [new Option(null, "King Eddy", "King Eddy"), new Option(null, "Mara", "Mara")],
		});

		// Then
		Codes(question).ShouldBe(["king_eddy", "mara"]);
	}

	[Fact]
	public async Task GivenExistingOption_WhenRewordedWithItsCode_ThenCodeIsUnchanged()
	{
		// Given
		using var client = await SignedIn();
		var question = await Create(client, Draft(UniqueKey("launch_site"), "single_select") with
		{
			Options = [new Option(null, "King Eddy", "King Eddy")],
		});

		// When
		using var revised = await client.PutAsJsonAsync(
			new Uri($"{Questions}/{question.GetProperty("id").GetString()}", UriKind.Relative),
			Draft(null, "single_select") with
			{
				Options = [new Option("king_eddy", "King Edward", "King Edward"), new Option(null, "Mara", "Mara")],
			});

		// Then
		revised.StatusCode.ShouldBe(HttpStatusCode.OK, await revised.Content.ReadAsStringAsync());
		Codes(await revised.Content.ReadFromJsonAsync<JsonElement>()).ShouldBe(["king_eddy", "mara"]);
	}

	[Fact]
	public async Task GivenWordingsReducingToSameCode_WhenQuestionIsCreated_ThenApiRefusesNamingBoth()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(Questions, Draft(UniqueKey("launch_site"), "single_select") with
		{
			Options = [new Option(null, "Site A-1", "Site A-1"), new Option(null, "Site A 1", "Site A 1")],
		});

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString() ?? "";
		detail.ShouldContain("'Site A-1'");
		detail.ShouldContain("'Site A 1'");
	}

	private static List<string?> Codes(JsonElement question)
	{
		// By code: the list has no order of its own (ADR-0136).
		return [.. question.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("code").GetString()).Order(StringComparer.Ordinal)];
	}

	private Task<HttpClient> SignedIn(MemberRole role = MemberRole.Administrator)
	{
		return SignedInClient.As(_factory, role);
	}

	private static string UniqueKey(string prefix)
	{
		var key = $"{prefix}_{Guid.NewGuid():N}";
		return key[..Math.Min(key.Length, 40)];
	}

	private static SaveQuestion Draft(string? key,
									  string type)
	{
		return new SaveQuestion(key, type, "A synthetic question", "Une question synthétique", null, null, null, null,
			false, true, true, null, null, null, []);
	}

	private static async Task<JsonElement> Create(HttpClient client,
												  SaveQuestion request)
	{
		using var response = await client.PostAsJsonAsync(Questions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static async Task<List<JsonElement>> List(HttpClient client)
	{
		var body = await client.GetFromJsonAsync<JsonElement>(Questions);
		return [.. body.EnumerateArray()];
	}

	private sealed record SaveQuestion(
		string? Key,
		string Type,
		string LabelEn,
		string LabelFr,
		string? HelpTextEn,
		string? HelpTextFr,
		string? PlaceholderEn,
		string? PlaceholderFr,
		bool IsRequired,
		bool IsPrivate,
		bool IsActive,
		string? DependsOnQuestionId,
		string? DependsOnChoiceId,
		string? GroupedUnderQuestionId,
		IReadOnlyList<Option> Options);

	private sealed record Reorder(IReadOnlyList<string> QuestionIdsInOrder);

	private sealed record Option(string? Code, string? LabelEn, string? LabelFr, bool Replace = false);

	/// <summary>The ID of the choice with this code, as the admin view carries it.</summary>
	private static string ChoiceIdOf(JsonElement question,
									 string code)
	{
		return question.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("code").GetString() == code)
			.GetProperty("id").GetString()!;
	}
}
