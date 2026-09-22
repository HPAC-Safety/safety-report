using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Translation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The admin translation endpoint, with a deterministic translator in place of
///     a provider. No credential and no network are involved.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class AdminTranslationEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Translate = new("/api/admin/translate", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoMemberSession_WhenTranslationIsRequested_ThenApiRefuses()
	{
		// Given
		await using var factory = WithTranslator(new FakeTranslator());
		using var client = factory.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync(Translate, Request(["Were you injured?"]));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenConfiguredTranslator_WhenAvailabilityIsAsked_ThenReportsAvailable()
	{
		// Given
		await using var factory = WithTranslator(new FakeTranslator());
		using var client = await SignedInAsync(factory);

		// When
		var body = await client.GetFromJsonAsync<JsonElement>(Translate);

		// Then
		body.GetProperty("available").GetBoolean().ShouldBeTrue();
		body.GetProperty("standIn").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task GivenDevelopmentStandIn_WhenAvailabilityIsAsked_ThenSaysStandIn()
	{
		// Given — a developer's server with no credential
		await using var factory = WithTranslator(new EchoTranslator());
		using var client = await SignedInAsync(factory);

		// When
		var body = await client.GetFromJsonAsync<JsonElement>(Translate);

		// Then — the screen says so, so copied English is never mistaken for
		// a translation
		body.GetProperty("available").GetBoolean().ShouldBeTrue();
		body.GetProperty("standIn").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public async Task GivenDevelopmentStandIn_WhenTextIsTranslated_ThenComesBackUnchanged()
	{
		// Given
		await using var factory = WithTranslator(new EchoTranslator());
		using var client = await SignedInAsync(factory);

		// When — the browser posts and the endpoint answers exactly as in
		// production; only the adapter differs
		using var response = await client.PostAsJsonAsync(Translate, Request(["Were you injured?"]));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("texts")[0].GetString().ShouldBe("Were you injured?");
	}

	[Fact]
	public async Task GivenNoConfiguredTranslator_WhenAvailabilityIsAsked_ThenReportsUnavailable()
	{
		// Given — the ordinary state of a checkout with no credential
		await using var factory = WithTranslator(new FakeTranslator { Configured = false });
		using var client = await SignedInAsync(factory);

		// When
		var body = await client.GetFromJsonAsync<JsonElement>(Translate);

		// Then
		body.GetProperty("available").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task GivenEnglishText_WhenTranslated_ThenTranslationsComeBackInOrder()
	{
		// Given
		await using var factory = WithTranslator(new FakeTranslator());
		using var client = await SignedInAsync(factory);

		// When
		using var response = await client.PostAsJsonAsync(Translate, Request(["One", "Two"]));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		var texts = body.GetProperty("texts").EnumerateArray().Select(text => text.GetString()).ToList();

		texts.ShouldBe(["[fr-CA] One", "[fr-CA] Two"]);
	}

	[Fact]
	public async Task GivenBlankFieldsAmongText_WhenTranslated_ThenTheyComeBackBlankInPlace()
	{
		// Given — an administrator may leave the help text empty
		var translator = new FakeTranslator();
		await using var factory = WithTranslator(translator);
		using var client = await SignedInAsync(factory);

		// When
		using var response = await client.PostAsJsonAsync(Translate, Request(["Label", "", "   ", "Option"]));

		// Then
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		var texts = body.GetProperty("texts").EnumerateArray().Select(text => text.GetString()).ToList();

		texts.ShouldBe(["[fr-CA] Label", "", "", "[fr-CA] Option"]);

		// And nothing blank was sent to the provider
		translator.Received.ShouldBe(["Label", "Option"]);
	}

	[Fact]
	public async Task GivenNothingButBlanks_WhenTranslationIsRequested_ThenNoProviderCallIsMade()
	{
		// Given
		var translator = new FakeTranslator();
		await using var factory = WithTranslator(translator);
		using var client = await SignedInAsync(factory);

		// When
		using var response = await client.PostAsJsonAsync(Translate, Request(["", "  "]));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		translator.Calls.ShouldBe(0);
	}

	[Fact]
	public async Task GivenFrenchText_WhenTranslatedBack_ThenDirectionIsHonoured()
	{
		// Given
		var translator = new FakeTranslator();
		await using var factory = WithTranslator(translator);
		using var client = await SignedInAsync(factory);

		// When
		using var response = await client.PostAsJsonAsync(
			Translate, new { texts = new[] { "Avez-vous été blessé ?" }, from = "fr-CA", to = "en-CA" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		translator.LastSource.ShouldBe(Locale.FrCa);
		translator.LastTarget.ShouldBe(Locale.EnCa);
	}

	[Theory]
	[InlineData("en-CA", "en-CA")]
	[InlineData("de-DE", "fr-CA")]
	[InlineData("en-CA", "")]
	public async Task GivenUnusableLanguagePair_WhenTranslationIsRequested_ThenApiRejects(
		string from, string to)
	{
		// Given
		await using var factory = WithTranslator(new FakeTranslator());
		using var client = await SignedInAsync(factory);

		// When
		using var response = await client.PostAsJsonAsync(
			Translate, new { texts = new[] { "One" }, from, to });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenTranslatorIsUnavailable_WhenTranslationIsRequested_ThenApiSaysSoSafely()
	{
		// Given
		await using var factory = WithTranslator(new FakeTranslator
		{
			Failure = new TranslationUnavailableException("The translation service answered 403.")
		});

		using var client = await SignedInAsync(factory);

		// When
		using var response = await client.PostAsJsonAsync(Translate, Request(["Were you injured?"]));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

		var problem = await response.Content.ReadAsStringAsync();
		problem.ShouldContain("403");
		// Neither the drafted wording nor anything resembling a credential
		// reaches the caller.
		problem.ShouldNotContain("Were you injured?");
	}

	[Fact]
	public async Task GivenRequestWithNoTextsFieldAtAll_WhenArrives_ThenAnsweredWithNothing()
	{
		// Given
		var translator = new FakeTranslator();
		await using var factory = WithTranslator(translator);
		using var client = await SignedInAsync(factory);

		// When
		using var response = await client.PostAsJsonAsync(
			Translate, new { from = "en-CA", to = "fr-CA" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		translator.Calls.ShouldBe(0);
	}

	private static object Request(string[] texts)
	{
		return new { texts, from = "en-CA", to = "fr-CA" };
	}

	private WebApplicationFactory<Program> WithTranslator(ITranslator translator)
	{
		return _factory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				services.RemoveAll<ITranslator>();
				services.AddSingleton(translator);
			}));
	}

	private static Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> factory)
	{
		return SignedInClient.AsAsync(factory, MemberRole.Administrator);
	}

	/// <summary>
	///     Stands in for a provider. Deterministic on purpose: these tests assert
	///     the endpoint's behaviour, never the quality of a translation.
	/// </summary>
	private sealed class FakeTranslator : ITranslator
	{
		public bool Configured { get; init; } = true;

		public TranslationUnavailableException? Failure { get; init; }

		public List<string> Received { get; } = [];

		public int Calls { get; private set; }

		public Locale LastSource { get; private set; }

		public Locale LastTarget { get; private set; }

		public bool IsConfigured => Configured;

		public Task<IReadOnlyList<string>> TranslateAsync(
			IReadOnlyList<string> texts, Locale source, Locale target, CancellationToken cancellationToken)
		{
			Calls++;
			Received.AddRange(texts);
			LastSource = source;
			LastTarget = target;

			return Failure is not null
				? throw Failure
				: Task.FromResult<IReadOnlyList<string>>([.. texts.Select(text => $"[{target.Code}] {text}")]);
		}
	}
}
