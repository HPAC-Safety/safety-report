using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The Typeform import admin endpoints, against a real PostgreSQL
///     container. See ADR-0077, amended by ADR-0078.
/// </summary>
/// <remarks>
///     Every file here is either the organization's real, already-public form
///     export or hand-built synthetic Typeform JSON — nothing is a report, an
///     answer, or anything a reporter wrote.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class TypeformImportEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Import = new("/api/admin/typeform/import", UriKind.Relative);
	private static readonly Uri PendingLogic = new("/api/admin/typeform/pending-logic", UriKind.Relative);
	private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Typeform");

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBearerToken_WhenImportIsAttempted_ThenApiRefuses()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.PostAsync(Import, Multipart("synthetic-en.json", "synthetic-fr.json"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenAMemberSession_WhenImportIsAttempted_ThenApiRefuses()
	{
		// Given
		using var client = await SignedIn(MemberRole.User);

		// When
		using var response = await client.PostAsync(Import, Multipart("synthetic-en.json", "synthetic-fr.json"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenAMatchedPair_WhenImported_ThenPreviewListsDrafts()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsync(Import, Multipart("synthetic-en.json", "synthetic-fr.json"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
		var drafts = preview.GetProperty("drafts").EnumerateArray().ToList();
		drafts.ShouldContain(draft => draft.GetProperty("key").GetString() == "name_ref");
	}

	[Fact]
	public async Task GivenOnlyOneFile_WhenImportIsAttempted_ThenApiRejects()
	{
		// Given
		using var client = await SignedIn();
		using var content = new MultipartFormDataContent
		{
			{ FileContent("synthetic-en.json"), "english", "synthetic-en.json" },
		};

		// When
		using var response = await client.PostAsync(Import, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenAnUnsupportedField_WhenImported_ThenPreviewListsItAsRejected()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsync(Import, Multipart("synthetic-en.json", "synthetic-fr.json"));

		// Then
		var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
		var rejected = preview.GetProperty("rejected").EnumerateArray().ToList();
		rejected.ShouldContain(field => field.GetProperty("ref").GetString() == "unsupported-ref");
	}

	[Fact]
	public async Task GivenAFieldWithARealCondition_WhenImported_ThenAPendingLogicNoteIsPersisted()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsync(Import, Multipart("synthetic-en.json", "synthetic-fr.json"));

		// Then
		var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
		var noteIds = preview.GetProperty("pendingLogicNoteIds").EnumerateArray().Select(id => id.GetString()).ToList();
		noteIds.ShouldHaveSingleItem();

		var listed = await client.GetFromJsonAsync<JsonElement>(PendingLogic);
		listed.EnumerateArray().ShouldContain(note => note.GetProperty("id").GetString() == noteIds[0]);
	}

	[Fact]
	public async Task GivenAPendingLogicNote_WhenDeleted_ThenNoLongerListed()
	{
		// Given
		using var client = await SignedIn();
		using var imported = await client.PostAsync(Import, Multipart("synthetic-en.json", "synthetic-fr.json"));
		var preview = await imported.Content.ReadFromJsonAsync<JsonElement>();
		var noteId = preview.GetProperty("pendingLogicNoteIds").EnumerateArray().First().GetString();

		// When
		using var response = await client.DeleteAsync(new Uri($"{PendingLogic}/{noteId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var listed = await client.GetFromJsonAsync<JsonElement>(PendingLogic);
		listed.EnumerateArray().ShouldNotContain(note => note.GetProperty("id").GetString() == noteId);
	}

	[Fact]
	public async Task GivenAMalformedPendingLogicId_WhenDeleted_ThenApiReturnsNotFound()
	{
		// Given — not even a well-formed TinyId (12 characters, not 11)
		using var client = await SignedIn();

		// When
		using var response = await client.DeleteAsync(new Uri($"{PendingLogic}/unknown00000", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAWellFormedButUnknownPendingLogicId_WhenDeleted_ThenApiReturnsNotFound()
	{
		// Given — a syntactically valid TinyId that simply names no row
		using var client = await SignedIn();

		// When
		using var response = await client.DeleteAsync(new Uri($"{PendingLogic}/AAAAAAAAAAA", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenTheOrganizationsRealExportPair_WhenImported_ThenPreviewSucceeds()
	{
		// Given
		using var client = await SignedIn();

		// When
		using var response = await client.PostAsync(Import, Multipart("form-en.json", "form-fr.json"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
		preview.GetProperty("drafts").GetArrayLength().ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task GivenAWellFormedJsonFileMissingFields_WhenImported_ThenApiRejects()
	{
		// Given — valid JSON, but not shaped like a Typeform export
		using var client = await SignedIn();
		using var content = new MultipartFormDataContent
		{
			{ new StringContent("{\"not\":\"a typeform export\"}"), "english", "bad.json" },
			{ new StringContent("{\"not\":\"a typeform export\"}"), "french", "bad.json" },
		};

		// When
		using var response = await client.PostAsync(Import, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenAFileThatIsNotValidJson_WhenImported_ThenApiRejects()
	{
		// Given — malformed JSON, not merely the wrong shape
		using var client = await SignedIn();
		using var content = new MultipartFormDataContent
		{
			{ new StringContent("{this is not json"), "english", "bad.json" },
			{ new StringContent("{this is not json"), "french", "bad.json" },
		};

		// When
		using var response = await client.PostAsync(Import, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private Task<HttpClient> SignedIn(MemberRole role = MemberRole.Administrator)
	{
		return SignedInClient.As(_factory, role);
	}

	private static MultipartFormDataContent Multipart(string englishFileName,
													  string frenchFileName)
	{
		return new MultipartFormDataContent
		{
			{ FileContent(englishFileName), "english", englishFileName }, { FileContent(frenchFileName), "french", frenchFileName },
		};
	}

	private static StreamContent FileContent(string fileName)
	{
		var stream = File.OpenRead(Path.Combine(FixturesDirectory, fileName));
		var content = new StreamContent(stream);
		content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
		return content;
	}
}
