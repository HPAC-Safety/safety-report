using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The public current-question endpoint, against a real PostgreSQL container.
/// </summary>
/// <remarks>
///     Every question here is synthetic. Questions are seeded through the
///     Administrator-only authoring endpoints and then read back through the
///     public endpoint, which takes no bearer token at all.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class PublicQuestionEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBearerToken_WhenQuestionsAreListed_ThenApiAnswersAnonymously()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(PublicQuestions);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GivenActiveQuestion_WhenListedPublicly_ThenBothLanguagesArePresent()
	{
		// Given
		using var admin = await SignedIn();
		var key = UniqueKey("wind_direction");
		await Create(admin, Draft(key, "short_text"));

		// When
		var listed = await ListPublic();

		// Then
		var question = listed.Single(candidate => candidate.GetProperty("key").GetString() == key);
		question.GetProperty("labelEn").GetString().ShouldBe("A synthetic question");
		question.GetProperty("labelFr").GetString().ShouldBe("Une question synthétique");
		question.GetProperty("type").GetString().ShouldBe("short_text");
	}

	[Fact]
	public async Task GivenInactiveQuestion_WhenListedPublicly_ThenOmitted()
	{
		// Given
		using var admin = await SignedIn();
		var key = UniqueKey("never_asked");
		await Create(admin, Draft(key, "short_text") with { IsActive = false });

		// When
		var listed = await ListPublic();

		// Then
		listed.ShouldNotContain(candidate => candidate.GetProperty("key").GetString() == key);
	}

	[Fact]
	public async Task GivenDeletedQuestion_WhenListedPublicly_ThenOmittedRatherThanFallingBackToAnOlderRevision()
	{
		// Given
		using var admin = await SignedIn();
		var key = UniqueKey("retired");
		var created = await Create(admin, Draft(key, "short_text"));
		var id = created.GetProperty("id").GetString();

		// When
		using var delete = await admin.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));
		delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		// Then
		var listed = await ListPublic();
		listed.ShouldNotContain(candidate => candidate.GetProperty("key").GetString() == key);
	}

	[Fact]
	public async Task GivenEditedQuestion_WhenListedPublicly_ThenOnlyTheCurrentRevisionIsShown()
	{
		// Given
		using var admin = await SignedIn();
		var key = UniqueKey("surface_wind");
		var created = await Create(admin, Draft(key, "short_text"));
		var id = created.GetProperty("id").GetString();

		// When
		var edit = Draft(key, "short_text") with { LabelEn = "Reworded question" };
		using var revise = await admin.PutAsJsonAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative), edit);
		revise.StatusCode.ShouldBe(HttpStatusCode.OK);

		// Then
		var listed = await ListPublic();
		var question = listed.Single(candidate => candidate.GetProperty("key").GetString() == key);
		question.GetProperty("labelEn").GetString().ShouldBe("Reworded question");
	}

	[Fact]
	public async Task GivenGroupWithChildren_WhenListedPublicly_ThenChildrenAreNestedAndNotRepeatedAtTopLevel()
	{
		// Given
		using var admin = await SignedIn();
		var groupKey = UniqueKey("aircraft");
		var group = await Create(admin, Draft(groupKey, "group") with { IsRequired = false, IsPrivate = false });
		var groupId = group.GetProperty("id").GetString();

		var childKey = UniqueKey("aircraft_type");
		await Create(admin, Draft(childKey, "short_text") with { GroupedUnderQuestionId = groupId });

		// When
		var listed = await ListPublic();

		// Then
		listed.ShouldNotContain(candidate => candidate.GetProperty("key").GetString() == childKey);

		var groupView = listed.Single(candidate => candidate.GetProperty("key").GetString() == groupKey);
		var children = groupView.GetProperty("children").EnumerateArray().ToList();
		children.ShouldHaveSingleItem();
		children[0].GetProperty("key").GetString().ShouldBe(childKey);
	}

	[Fact]
	public async Task GivenConditionalQuestion_WhenListedPublicly_ThenDependencyIsIncluded()
	{
		// Given
		using var admin = await SignedIn();
		var parent = await Create(admin, Draft(UniqueKey("were_you_injured"), "yes_no"));
		var parentId = parent.GetProperty("id").GetString();

		var childKey = UniqueKey("injury_detail");
		await Create(admin, Draft(childKey, "long_text") with { DependsOnQuestionId = parentId });

		// When
		var listed = await ListPublic();

		// Then
		var child = listed.Single(candidate => candidate.GetProperty("key").GetString() == childKey);
		child.GetProperty("dependsOnQuestionId").GetString().ShouldBe(parentId);
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

	private static SaveQuestion Draft(string key, string type)
	{
		return new SaveQuestion(key, type, "A synthetic question", "Une question synthétique", null, null, null, null,
			false, true, true, null, null, null, null, false, []);
	}

	private static async Task<JsonElement> Create(HttpClient client, SaveQuestion request)
	{
		using var response = await client.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<List<JsonElement>> ListPublic()
	{
		using var client = _factory.CreateClient();
		var body = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
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

	private sealed record Option(string Code, string LabelEn, string LabelFr);
}
