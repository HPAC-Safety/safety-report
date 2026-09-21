using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
/// The admin question-authoring endpoints, against a real PostgreSQL container.
/// </summary>
/// <remarks>
/// Every question here is synthetic. The question bank is form definition, not
/// report content, so nothing in these tests touches a report, an answer, or
/// anything a reporter wrote.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class AdminQuestionEndpointTests(ApiPostgresFixture fixture)
{
    private const string SessionHeader = "X-Hpac-Member-Session";
    private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);
    private static readonly Uri OptionSets = new("/api/admin/option-sets", UriKind.Relative);

    private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

    [Fact]
    public async Task Given_no_member_session_When_questions_are_listed_Then_the_api_refuses()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        using var response = await client.GetAsync(Questions);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Given_a_member_session_When_a_question_is_created_Then_it_is_listed_with_its_first_revision()
    {
        // Given
        using var client = SignedIn();
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
    public async Task Given_an_existing_question_When_it_is_edited_Then_a_new_revision_is_written_rather_than_a_patch()
    {
        // Given
        using var client = SignedIn();
        var key = UniqueKey("surface_wind");
        var created = await CreateAsync(client, Draft(key, "short_text"));
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
    public async Task Given_a_non_boolean_question_When_another_depends_on_it_Then_the_api_rejects_the_dependency()
    {
        // Given
        using var client = SignedIn();
        var parent = await CreateAsync(client, Draft(UniqueKey("glider_make"), "short_text"));

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
    public async Task Given_a_boolean_question_When_another_depends_on_it_Then_the_dependency_is_stored()
    {
        // Given
        using var client = SignedIn();
        var parent = await CreateAsync(client, Draft(UniqueKey("were_you_injured"), "yes_no"));
        var parentId = parent.GetProperty("id").GetString();

        // When
        var child = Draft(UniqueKey("injury_detail"), "long_text") with { DependsOnQuestionId = parentId };
        var created = await CreateAsync(client, child);

        // Then
        created.GetProperty("dependsOnQuestionId").GetString().ShouldBe(parentId);
    }

    [Fact]
    public async Task Given_a_shared_choice_list_When_a_question_uses_it_Then_the_revision_snapshots_its_options()
    {
        // Given
        using var client = SignedIn();
        var set = await CreateOptionSetAsync(client, UniqueKey("aerodromes"));

        // When
        var question = Draft(UniqueKey("launch_site"), "autocomplete") with
        {
            OptionSetId = set.GetProperty("id").GetString(),
        };

        var created = await CreateAsync(client, question);

        // Then
        var options = created.GetProperty("options").EnumerateArray().ToList();
        options.Count.ShouldBe(2);
        options.ShouldAllBe(option => option.GetProperty("sourceItemId").GetString() != null);
    }

    [Fact]
    public async Task Given_a_question_built_from_a_choice_list_When_the_list_changes_Then_the_saved_revision_is_untouched()
    {
        // Given
        using var client = SignedIn();
        var set = await CreateOptionSetAsync(client, UniqueKey("provinces"));
        var setId = set.GetProperty("id").GetString();

        var question = Draft(UniqueKey("occurrence_province"), "single_select") with { OptionSetId = setId };
        var created = await CreateAsync(client, question);
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
        var listed = await ListAsync(client);
        var stored = listed.Single(candidate => candidate.GetProperty("revisionId").GetString() == revisionId);

        var options = stored.GetProperty("options").EnumerateArray().ToList();
        options.Count.ShouldBe(2);
        options.Select(option => option.GetProperty("labelEn").GetString()).ShouldContain("Alberta");
    }

    [Fact]
    public async Task Given_several_questions_When_they_are_rearranged_Then_each_moved_one_gains_a_revision()
    {
        // Given
        using var client = SignedIn();
        var first = await CreateAsync(client, Draft(UniqueKey("first_question"), "short_text"));
        var second = await CreateAsync(client, Draft(UniqueKey("second_question"), "short_text"));

        var firstId = first.GetProperty("id").GetString()!;
        var secondId = second.GetProperty("id").GetString()!;

        var before = await ListAsync(client);
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
    public async Task Given_a_question_When_it_is_deleted_Then_it_disappears_from_the_list_without_being_erased()
    {
        // Given
        using var client = SignedIn();
        var created = await CreateAsync(client, Draft(UniqueKey("retired_question"), "short_text"));
        var id = created.GetProperty("id").GetString()!;

        // When
        using var response = await client.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listed = await ListAsync(client);
        listed.ShouldNotContain(question => question.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task Given_an_unknown_type_When_a_question_is_created_Then_the_api_rejects_it()
    {
        // Given
        using var client = SignedIn();

        // When
        using var response = await client.PostAsJsonAsync(Questions, Draft(UniqueKey("odd"), "telepathy"));

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private HttpClient SignedIn()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(SessionHeader, "test-session");
        return client;
    }

    private static string UniqueKey(string prefix)
    {
        var key = $"{prefix}_{Guid.NewGuid():N}";
        return key[..Math.Min(key.Length, 40)];
    }

    private static SaveQuestion Draft(string key, string type) =>
        new(key, type, "A synthetic question", "Une question synthétique", null, null, null, null,
            IsRequired: false, IsPrivate: true, IsActive: true, null, null, null, []);

    private static async Task<JsonElement> CreateAsync(HttpClient client, SaveQuestion request)
    {
        using var response = await client.PostAsJsonAsync(Questions, request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> CreateOptionSetAsync(HttpClient client, string key)
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

    private static async Task<List<JsonElement>> ListAsync(HttpClient client)
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
        string? SectionKey,
        string? DependsOnQuestionId,
        string? OptionSetId,
        IReadOnlyList<Option> Options);

    private sealed record Reorder(IReadOnlyList<string> QuestionIdsInOrder);

    private sealed record SaveOptionSet(string? Key, string NameEn, string NameFr, IReadOnlyList<Option> Items);

    private sealed record Option(string Code, string LabelEn, string LabelFr);
}
