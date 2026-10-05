using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
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
		var first = await client.GetFromJsonAsync<JsonElement>(new Uri(Feed, UriKind.Relative), cancellationToken: TestContext.Current.CancellationToken);

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?after={Uri.EscapeDataString(cursor)}", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
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
		var plain = await client.GetFromJsonAsync<JsonElement>(new Uri(Feed, UriKind.Relative), cancellationToken: TestContext.Current.CancellationToken);

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q={Uri.EscapeDataString(q)}", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		page.GetRawText().ShouldBe(plain.GetRawText());
	}

	[Theory]
	// Deliberately not an ordinary English or French sentence: this suite's
	// Postgres container is shared with every other Api.Tests class
	// (SharedApiPostgres), and every one of them publishes reports whose
	// synthetic summaries are themselves plain English/French sentences. A
	// "guaranteed no match" probe built from real words shares real trigrams
	// with that ever-growing corpus and can cross pg_trgm's similarity
	// threshold by chance once enough tests have run; a made-up token cannot.
	[InlineData("zzqxjwsearchprobenomatchshouldeverexist")]
	[InlineData("zzqxjwtermeinexistantquinecorrespondjamais")]
	public async Task GivenNonBlankSearchMatchingNothing_WhenFeedIsQueried_ThenEmptyPageReturned(string q)
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q={Uri.EscapeDataString(q)}&locale=en-CA", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
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
		using var response = await client.GetAsync(new Uri($"{Feed}?q={Uri.EscapeDataString(tooLong)}&locale=en-CA", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Theory]
	[InlineData("wing-over")]
	[InlineData("l'aile")]
	[InlineData("\"wing-over\"")]
	[InlineData("l'équipage sécurité-\"test\"")]
	public async Task GivenPunctuatedSearchQuery_WhenFeedIsQueried_ThenAcceptedWithoutError(string query)
	{
		// Given: search_public_reports splits the query into words and hands
		// each one to plainto_tsquery, never re-parses a tsquery's rendered
		// text, so a hyphen, an apostrophe, or a literal quote character
		// must never make it to the database as a syntax error (ADR-0157).
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q={Uri.EscapeDataString(query)}&locale=en-CA", UriKind.Relative), TestContext.Current.CancellationToken);

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
		var first = await client.GetAsync(new Uri($"{Feed}?q=field&locale=en-CA", UriKind.Relative), TestContext.Current.CancellationToken);

		// When
		using var response = await client.GetAsync(new Uri($"{Feed}?q=field&locale=en-CA&after={Uri.EscapeDataString(cursor)}", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var page = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		var firstPage = await first.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		page.GetProperty("items").GetRawText().ShouldBe(firstPage.GetProperty("items").GetRawText());
	}

	[Fact]
	public async Task GivenTheSameSearchFromEveryCallerRole_WhenTheFeedIsQueried_ThenTheResultsAreIdentical()
	{
		// Given
		var uri = new Uri($"{Feed}?q=integration-test-role-parity&locale=en-CA", UriKind.Relative);
		using var anonymous = _factory.CreateClient();
		using var user = await SignedInClient.As(_factory, MemberRole.User);
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		using var administrator = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		var anonymousBody = await (await anonymous.GetAsync(uri, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		var userBody = await (await user.GetAsync(uri, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		var officerBody = await (await officer.GetAsync(uri, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
		var administratorBody = await (await administrator.GetAsync(uri, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

		// Then: the same query gets the same answer whoever asks, or asks
		// anonymously — the public search endpoint never widens by role.
		userBody.ShouldBe(anonymousBody);
		officerBody.ShouldBe(anonymousBody);
		administratorBody.ShouldBe(anonymousBody);
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
		using var response = await client.GetAsync(new Uri($"{Feed}/{Uri.EscapeDataString(reportId)}", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private static string Base64Url(string plain)
	{
		return System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(plain));
	}
}
