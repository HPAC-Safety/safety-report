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
	private static readonly Uri OptionSets = new("/api/admin/option-sets", UriKind.Relative);

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
			DependsOnQuestionId = parent.GetProperty("id").GetString()
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
					new Option("paraglider", "Paraglider", "Parapente")
				]
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
			DependsOnOptionCode = "hang_glider"
		};
		var created = await Create(client, child);

		// Then
		created.GetProperty("dependsOnQuestionId").GetString().ShouldBe(parentId);
		created.GetProperty("dependsOnOptionCode").GetString().ShouldBe("hang_glider");
	}

	[Fact]
	public async Task GivenSingleSelectQuestion_WhenAnotherDependsOnAnUnofferedOption_ThenApiRejectsDependency()
	{
		// Given
		using var client = await SignedIn();
		var parent = await CreatePilotType(client);

		// When
		var child = Draft(UniqueKey("rating"), "short_text") with
		{
			DependsOnQuestionId = parent.GetProperty("id").GetString(),
			DependsOnOptionCode = "trike"
		};
		using var response = await client.PostAsJsonAsync(Questions, child);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("trike");
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
			DependsOnQuestionId = parent.GetProperty("id").GetString()
		};
		using var response = await client.PostAsJsonAsync(Questions, child);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	// -------------------------------------------- statement/group (ADR-0076) --

	private static SaveQuestion NoAnswerDraft(string key, string type)
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
			DependsOnQuestionId = parent.GetProperty("id").GetString()
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
			GroupedUnderQuestionId = notAGroup.GetProperty("id").GetString()
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
			GroupedUnderQuestionId = outer.GetProperty("id").GetString()
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
	public async Task GivenMultiSelectWithSharedList_WhenAuthoredWithReporterAdditions_ThenFlagIsStored()
	{
		// Given
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("ratings"));

		// When
		var created = await Create(
			client,
			Draft(UniqueKey("ratings"), "multi_select") with
			{
				OptionSetId = set.GetProperty("id").GetString(),
				AllowsReporterAdditions = true
			});

		// Then
		created.GetProperty("allowsReporterAdditions").GetBoolean().ShouldBeTrue();
		created.GetProperty("choicesComeFromLiveList").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public async Task GivenMultiSelect_WhenAuthoredWithoutReporterAdditions_ThenDefaultsToClosed()
	{
		// Given
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("ratings"));

		// When
		var created = await Create(
			client, Draft(UniqueKey("ratings"), "multi_select") with { OptionSetId = set.GetProperty("id").GetString() });

		// Then
		created.GetProperty("allowsReporterAdditions").GetBoolean().ShouldBeFalse();
		created.GetProperty("choicesComeFromLiveList").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task GivenSingleSelect_WhenAuthoredWithReporterAdditions_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsJsonAsync(
			Questions,
			Draft(UniqueKey("pilot_type"), "single_select") with
			{
				AllowsReporterAdditions = true,
				Options = [new Option("hang_glider", "Hang glider", "Deltaplane")]
			});

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("cannot allow reporter additions");
	}

	[Fact]
	public async Task GivenAutocomplete_WhenAuthoredWithoutReporterAdditions_ThenAlwaysStoredOn()
	{
		// Given
		using var client = await SignedIn();

		// When — the flag is redundant for autocomplete; the type alone decides
		var created = await Create(client, Draft(UniqueKey("launch_site"), "autocomplete"));

		// Then
		created.GetProperty("allowsReporterAdditions").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public async Task GivenSharedChoiceList_WhenQuestionUses_ThenRevisionSnapshotsOptions()
	{
		// Given
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("aerodromes"));

		// When
		var question = Draft(UniqueKey("launch_site"), "autocomplete") with
		{
			OptionSetId = set.GetProperty("id").GetString()
		};

		var created = await Create(client, question);

		// Then
		var options = created.GetProperty("options").EnumerateArray().ToList();
		options.Count.ShouldBe(2);
		options.ShouldAllBe(option => option.GetProperty("sourceItemId").GetString() != null);
	}

	[Fact]
	public async Task GivenQuestionBuiltFromChoiceList_WhenListChanges_ThenSavedRevisionIsUntouched()
	{
		// Given
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("provinces"));
		var setId = set.GetProperty("id").GetString();

		var question = Draft(UniqueKey("occurrence_province"), "single_select") with { OptionSetId = setId };
		var created = await Create(client, question);
		var revisionId = created.GetProperty("revisionId").GetString();

		// When the shared list is edited afterwards
		var edited = new SaveOptionSet(
			null,
			"Provinces",
			"Provinces",
			[new Option("alberta", "Alberta (edited)", "Alberta (modifié)")]);

		using var replaced = await client.PutAsJsonAsync(
			new Uri($"/api/admin/option-sets/{setId}", UriKind.Relative), edited);

		replaced.StatusCode.ShouldBe(HttpStatusCode.OK);

		// Then the revision that already snapshotted it is unchanged
		var listed = await List(client);
		var stored = listed.Single(candidate => candidate.GetProperty("revisionId").GetString() == revisionId);

		var options = stored.GetProperty("options").EnumerateArray().ToList();
		options.Count.ShouldBe(2);
		options.Select(option => option.GetProperty("labelEn").GetString()).ShouldContain("Alberta");
	}

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
	public async Task GivenNoMemberSession_WhenChoiceListsAreListed_ThenApiRefuses()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(OptionSets);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

	[Fact]
	public async Task GivenChoiceListDoesNotExist_WhenQuestionNames_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		var request = Draft(UniqueKey("launch_site"), "autocomplete") with { OptionSetId = "AAAAAAAAAAA" };
		using var response = await client.PostAsJsonAsync(Questions, request);

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
	public async Task GivenChoiceList_WhenDeleted_ThenDisappearsFromList()
	{
		// Given
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("retired_list"));
		var id = set.GetProperty("id").GetString();

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/option-sets/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var listed = await client.GetFromJsonAsync<JsonElement>(OptionSets);
		listed.EnumerateArray().ShouldNotContain(candidate => candidate.GetProperty("id").GetString() == id);
	}

	[Fact]
	public async Task GivenChoiceListKeyAlreadyInUse_WhenAnotherIsCreated_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("duplicate_list");
		await CreateOptionSet(client, key);

		// When
		var request = new SaveOptionSet(key, "Duplicate", "Duplicate", [new Option("one", "One", "Un")]);
		using var response = await client.PostAsJsonAsync(OptionSets, request);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenNoKey_WhenChoiceListIsCreated_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		var request = new SaveOptionSet(" ", "Nameless", "Sans nom", []);
		using var response = await client.PostAsJsonAsync(OptionSets, request);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenRepeatedCode_WhenChoiceListIsCreated_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();

		// When
		var request = new SaveOptionSet(
			UniqueKey("repeated"),
			"Repeated",
			"Répété",
			[new Option("one", "One", "Un"), new Option("one", "One again", "Encore un")]);

		using var response = await client.PostAsJsonAsync(OptionSets, request);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenIdNamesNoChoiceList_WhenReplaced_ThenApiReturnsNotFound(string id)
	{
		// Given
		using var client = await SignedIn();

		// When
		var request = new SaveOptionSet(null, "Absent", "Absent", []);
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/option-sets/{id}", UriKind.Relative), request);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenIdNamesNoChoiceList_WhenDeleted_ThenApiReturnsNotFound(string id)
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/option-sets/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenChoiceList_WhenItemIsAddedAndAnotherRemoved_ThenListMatchesWhatWasSent()
	{
		// Given
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("edited_list"));
		var id = set.GetProperty("id").GetString();

		// When — 'alberta' relabelled, 'yukon' dropped, 'nunavut' added, order reversed
		var request = new SaveOptionSet(
			null,
			"Edited",
			"Modifié",
			[new Option("nunavut", "Nunavut", "Nunavut"), new Option("alberta", "Alberta (edited)", "Alberta (modifié)")]);

		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/option-sets/{id}", UriKind.Relative), request);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var replaced = await response.Content.ReadFromJsonAsync<JsonElement>();
		var items = replaced.GetProperty("items").EnumerateArray().ToList();

		items.Select(item => item.GetProperty("code").GetString()).ShouldBe(["nunavut", "alberta"]);
		items[1].GetProperty("labelEn").GetString().ShouldBe("Alberta (edited)");
	}

	[Fact]
	public async Task GivenTypeAheadBackedByList_WhenListGrows_ThenQuestionOffersNewChoice()
	{
		// Given — a type-ahead renders the live list, so a site a reporter
		// added shows up without anyone republishing the question (ADR-0063)
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("sites"));
		var setId = set.GetProperty("id").GetString();

		var created = await Create(
			client, Draft(UniqueKey("where_did_this_happen"), "autocomplete") with { OptionSetId = setId });

		created.GetProperty("choicesComeFromLiveList").GetBoolean().ShouldBeTrue();

		// When a choice is added to the shared list afterwards
		var grown = new SaveOptionSet(
			null,
			"Sites",
			"Sites",
			[
				new Option("alberta", "Alberta", "Alberta"),
				new Option("yukon", "Yukon", "Yukon"),
				new Option("mount_7", "Mount 7", "Mont 7")
			]);

		using var replaced = await client.PutAsJsonAsync(
			new Uri($"/api/admin/option-sets/{setId}", UriKind.Relative), grown);

		replaced.StatusCode.ShouldBe(HttpStatusCode.OK);

		// Then the question offers it, while its revision still records what
		// it was saved with
		var listed = await List(client);
		var question = listed.Single(candidate => candidate.GetProperty("id").GetString() == created.GetProperty("id").GetString());

		question.GetProperty("options").EnumerateArray()
			.Select(option => option.GetProperty("code").GetString())
			.ShouldContain("mount_7");

		question.GetProperty("revisionNumber").GetInt32().ShouldBe(1);
	}

	[Fact]
	public async Task GivenPickOneBackedByList_WhenListGrows_ThenQuestionKeepsSnapshot()
	{
		// Given — a closed, curated set still renders exactly what it recorded
		using var client = await SignedIn();
		var set = await CreateOptionSet(client, UniqueKey("provinces"));
		var setId = set.GetProperty("id").GetString();

		var created = await Create(
			client, Draft(UniqueKey("occurrence_province"), "single_select") with { OptionSetId = setId });

		created.GetProperty("choicesComeFromLiveList").GetBoolean().ShouldBeFalse();

		// When
		var grown = new SaveOptionSet(
			null,
			"Provinces",
			"Provinces",
			[
				new Option("alberta", "Alberta", "Alberta"),
				new Option("yukon", "Yukon", "Yukon"),
				new Option("nunavut", "Nunavut", "Nunavut")
			]);

		using var replaced = await client.PutAsJsonAsync(
			new Uri($"/api/admin/option-sets/{setId}", UriKind.Relative), grown);

		replaced.StatusCode.ShouldBe(HttpStatusCode.OK);

		// Then
		var listed = await List(client);
		var question = listed.Single(candidate => candidate.GetProperty("id").GetString() == created.GetProperty("id").GetString());

		question.GetProperty("options").EnumerateArray()
			.Select(option => option.GetProperty("code").GetString())
			.ShouldNotContain("nunavut");
	}

	[Fact]
	public async Task GivenChoiceList_WhenListed_ThenEachChoiceSaysWhetherReporterAdded()
	{
		// Given
		using var client = await SignedIn();
		await CreateOptionSet(client, UniqueKey("authored"));

		// When
		var listed = await client.GetFromJsonAsync<JsonElement>(OptionSets);

		// Then — nothing an administrator authored is marked
		listed.EnumerateArray()
			.SelectMany(set => set.GetProperty("items").EnumerateArray())
			.ShouldAllBe(item => !item.GetProperty("addedByReporter").GetBoolean());
	}

	[Fact]
	public async Task GivenOptionsWithoutCodes_WhenQuestionIsCreated_ThenCodesAreDerivedFromEnglishWording()
	{
		// Given
		using var client = await SignedIn();

		// When
		var question = await Create(client, Draft(UniqueKey("launch_site"), "single_select") with
		{
			Options = [new Option(null, "King Eddy", "King Eddy"), new Option(null, "Mara", "Mara")]
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
			Options = [new Option(null, "King Eddy", "King Eddy")]
		});

		// When
		using var revised = await client.PutAsJsonAsync(
			new Uri($"{Questions}/{question.GetProperty("id").GetString()}", UriKind.Relative),
			Draft(null, "single_select") with
			{
				Options = [new Option("king_eddy", "King Edward", "King Edward"), new Option(null, "Mara", "Mara")]
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
			Options = [new Option(null, "Site A-1", "Site A-1"), new Option(null, "Site A 1", "Site A 1")]
		});

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString() ?? "";
		detail.ShouldContain("'Site A-1'");
		detail.ShouldContain("'Site A 1'");
	}

	[Fact]
	public async Task GivenItemsWithoutCodes_WhenChoiceListIsCreatedAndExtended_ThenCodesAreDerivedFromEnglishWording()
	{
		// Given
		using var client = await SignedIn();
		using var created = await client.PostAsJsonAsync(OptionSets, new SaveOptionSet(
			UniqueKey("sites"), "Sites", "Sites", [new Option(null, "King Eddy", "King Eddy")]));
		created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
		var set = await created.Content.ReadFromJsonAsync<JsonElement>();

		// When
		using var replaced = await client.PutAsJsonAsync(
			new Uri($"{OptionSets}/{set.GetProperty("id").GetString()}", UriKind.Relative),
			new SaveOptionSet(null, "Sites", "Sites",
				[new Option("king_eddy", "King Edward", "King Edward"), new Option(null, "Mara", "Mara")]));

		// Then
		replaced.StatusCode.ShouldBe(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());
		var items = (await replaced.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray()
			.Select(item => item.GetProperty("code").GetString())
			.ToList();
		items.ShouldBe(["king_eddy", "mara"]);
	}

	private static List<string?> Codes(JsonElement question)
	{
		return [.. question.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("code").GetString())];
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

	private static SaveQuestion Draft(string? key, string type)
	{
		return new SaveQuestion(key, type, "A synthetic question", "Une question synthétique", null, null, null, null,
			false, true, true, null, null, null, null, false, []);
	}

	private static async Task<JsonElement> Create(HttpClient client, SaveQuestion request)
	{
		using var response = await client.PostAsJsonAsync(Questions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static async Task<JsonElement> CreateOptionSet(HttpClient client, string key)
	{
		var request = new SaveOptionSet(
			key,
			"Aerodromes",
			"Aérodromes",
			[new Option("alberta", "Alberta", "Alberta"), new Option("yukon", "Yukon", "Yukon")]);

		using var response = await client.PostAsJsonAsync(OptionSets, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

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
		string? DependsOnOptionCode,
		string? OptionSetId,
		string? GroupedUnderQuestionId,
		bool AllowsReporterAdditions,
		IReadOnlyList<Option> Options);

	private sealed record Reorder(IReadOnlyList<string> QuestionIdsInOrder);

	private sealed record SaveOptionSet(string? Key, string NameEn, string NameFr, IReadOnlyList<Option> Items);

	private sealed record Option(string? Code, string LabelEn, string LabelFr);
}
