using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The edges of the anonymous public report endpoints (#28), against a real
///     PostgreSQL container. What the feed contains and in what order is proven
///     by the REQ-MOD-036..038 acceptance scenarios; these cover what a visitor
///     can type into the address bar.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class PublicReportEndpointTests(ApiPostgresFixture fixture)
{
	private const string Feed = "/api/v1/public/reports";

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	public static TheoryData<string> UnreadableCursors =>
	[
		"",
		"not a cursor",
		"!!!!",
		Base64Url("12345"),
		Base64Url("12345.short"),
		Base64Url("abc.AAAAAAAAAAA"),
		Base64Url("99999999999999999999.AAAAAAAAAAA"),
		Base64Url("1.2.3"),
		new string('A', 65),
	];

	[Theory]
	[MemberData(nameof(UnreadableCursors))]
	public async Task GivenUnreadableCursor_WhenFeedIsQueried_ThenFirstPageIsReturned(string cursor)
	{
		// Given
		using var client = _factory.CreateClient();
		var first = await client.GetFromJsonAsync<JsonElement>(new Uri(Feed, UriKind.Relative));

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?after={Uri.EscapeDataString(cursor)}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>();
		page.GetProperty("items").GetRawText().ShouldBe(first.GetProperty("items").GetRawText());
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t\n")]
	public async Task GivenBlankSearchBox_WhenFeedIsQueried_ThenSameAsNoQAtAll(string q)
	{
		// Given
		using var client = _factory.CreateClient();
		var plain = await client.GetFromJsonAsync<JsonElement>(new Uri(Feed, UriKind.Relative));

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q={Uri.EscapeDataString(q)}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>();
		page.GetRawText().ShouldBe(plain.GetRawText());
	}

	[Theory]
	[InlineData("a search term nobody's published summary or comment could ever contain")]
	[InlineData("un terme de recherche introuvable")]
	public async Task GivenNonBlankSearchMatchingNothing_WhenFeedIsQueried_ThenEmptyPageReturned(string q)
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q={Uri.EscapeDataString(q)}&locale=en-CA", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>();
		page.GetProperty("items").GetArrayLength().ShouldBe(0);
		page.GetProperty("next").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	public async Task GivenSearchQueryLongerThanCap_WhenFeedIsQueried_ThenAcceptedWithoutError()
	{
		// Given
		using var client = _factory.CreateClient();
		var tooLong = new string('a', 5000);

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q={Uri.EscapeDataString(tooLong)}&locale=en-CA", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Theory]
	[InlineData("not a cursor")]
	[InlineData("!!!!")]
	public async Task GivenUnreadableCursor_WhenSearchIsQueried_ThenStartsFromTop(string cursor)
	{
		// Given
		using var client = _factory.CreateClient();
		var first = await client.GetAsync(new Uri($"{Feed}?q=field&locale=en-CA", UriKind.Relative));

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q=field&locale=en-CA&after={Uri.EscapeDataString(cursor)}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>();
		var firstPage = await first.Content.ReadFromJsonAsync<JsonElement>();
		page.GetProperty("items").GetRawText().ShouldBe(firstPage.GetProperty("items").GetRawText());
	}

	[Theory]
	[InlineData("short")]
	[InlineData("has spaces!")]
	[InlineData("AAAAAAAAAAAA")]
	public async Task GivenMalformedReportId_WhenDetailIsRequested_ThenNotFound(string reportId)
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}/{Uri.EscapeDataString(reportId)}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private static string Base64Url(string plain)
	{
		return System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(plain));
	}
}
