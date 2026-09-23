using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The Typeform export admin endpoint, against a real PostgreSQL container.
///     See ADR-0077, amended by ADR-0078.
/// </summary>
/// <remarks>Every question here is synthetic — the question bank is form definition, not report content.</remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class TypeformExportEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Export = new("/api/admin/typeform/export", UriKind.Relative);
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBearerToken_WhenExportIsAttempted_ThenApiRefuses()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(Export);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenAMemberSession_WhenExportIsAttempted_ThenApiRefuses()
	{
		// Given
		using var client = await SignedIn(MemberRole.User);

		// When
		using var response = await client.GetAsync(Export);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenALiveQuestion_WhenExported_ThenTheResultIsAZipOfAnEnglishAndAFrenchFile()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("wind_direction");
		await Create(client, key);

		// When
		using var response = await client.GetAsync(Export);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		response.Content.Headers.ContentType!.MediaType.ShouldBe("application/zip");

		using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
		archive.Entries.Select(entry => entry.Name).ShouldBe(["form-en.json", "form-fr.json"]);

		var english = await ReadEntry(archive, "form-en.json");
		english.ShouldContain($"\"ref\":\"{key}\"");
		english.ShouldContain("\"title\":\"A synthetic question\"");

		var french = await ReadEntry(archive, "form-fr.json");
		french.ShouldContain("\"title\":\"Une question synthétique\"");
	}

	[Fact]
	public async Task GivenAPrivateQuestion_WhenExported_ThenTheHpacExtensionCarriesItButAPlainFieldStillParses()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("injury_description");
		await Create(client, key);

		// When
		using var response = await client.GetAsync(Export);
		using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
		var english = await ReadEntry(archive, "form-en.json");

		// Then
		english.ShouldContain("\"hpac\":{");
		english.ShouldContain("\"is_private\":true");
		// A plain Typeform importer, unaware of the extension, still sees an
		// ordinary field: ref, title, and type parse regardless.
		english.ShouldContain($"\"ref\":\"{key}\"");
		english.ShouldContain("\"type\":\"short_text\"");
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

	private static async Task Create(HttpClient client,
									 string key)
	{
		var request = new
		{
			Key = key,
			Type = "short_text",
			LabelEn = "A synthetic question",
			LabelFr = "Une question synthétique",
			HelpTextEn = (string?)null,
			HelpTextFr = (string?)null,
			PlaceholderEn = (string?)null,
			PlaceholderFr = (string?)null,
			IsRequired = false,
			IsPrivate = true,
			IsActive = true,
			DependsOnQuestionId = (string?)null,
			DependsOnOptionCode = (string?)null,
			OptionSetId = (string?)null,
			GroupedUnderQuestionId = (string?)null,
			AllowsReporterAdditions = false,
			Options = Array.Empty<object>(),
		};

		using var response = await client.PostAsJsonAsync(Questions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
	}

	private static async Task<string> ReadEntry(ZipArchive archive,
												string entryName)
	{
		using var stream = archive.GetEntry(entryName)!.Open();
		using var reader = new StreamReader(stream);
		return await reader.ReadToEndAsync();
	}
}
