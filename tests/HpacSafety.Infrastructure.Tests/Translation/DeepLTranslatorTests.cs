using System.Net;
using System.Text;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Translation;

/// <summary>
///     The DeepL adapter, against a stubbed transport. Nothing here reaches the
///     network, and no real credential is used.
/// </summary>
public class DeepLTranslatorTests
{
	private const string Key = "test-key:fx";

	[Fact]
	public async Task GivenNoCredential_WhenTranslationIsRequested_ThenReportsUnconfigured()
	{
		// Given
		var (translator, _) = Translator(apiKey: null);

		// Then
		translator.IsConfigured.ShouldBeFalse();

		await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
	}

	[Fact]
	public async Task GivenOneLanguage_WhenTranslatedIntoItself_ThenRefused()
	{
		// Given
		var (translator, _) = Translator();

		// When / Then
		await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["Were you injured?"], Locale.EnCa, Locale.EnCa, CancellationToken.None));
	}

	[Fact]
	public async Task GivenNothingToTranslate_WhenRequested_ThenNoCallIsMade()
	{
		// Given
		var (translator, transport) = Translator();

		// When
		var translated = await translator.Translate([], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then — a provider charged per request is not asked to translate nothing
		translated.ShouldBeEmpty();
		transport.Requests.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenEnglish_WhenTranslated_ThenRequestAsksDeeplForCanadianFrench()
	{
		// Given
		var (translator, transport) = Translator(Responds("Avez-vous été blessé ?"));

		// When
		var translated = await translator.Translate(
			["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		translated.ShouldBe(["Avez-vous été blessé ?"]);

		var sent = transport.LastBody();
		sent.GetProperty("source_lang").GetString().ShouldBe("EN");
		sent.GetProperty("target_lang").GetString().ShouldBe("FR-CA");
		sent.GetProperty("formality").GetString().ShouldBe("prefer_more");
		sent.GetProperty("preserve_formatting").GetBoolean().ShouldBeTrue();
	}

	[Fact]
	public async Task GivenFrench_WhenTranslatedBack_ThenAskedForAsPlainFrench()
	{
		// Given — FR-CA is a target-only variant in DeepL and cannot be a source
		var (translator, transport) = Translator(Responds("Were you injured?"));

		// When
		await translator.Translate(
			["Avez-vous été blessé ?"], Locale.FrCa, Locale.EnCa, CancellationToken.None);

		// Then
		var sent = transport.LastBody();
		sent.GetProperty("source_lang").GetString().ShouldBe("FR");
		sent.GetProperty("target_lang").GetString().ShouldBe("EN-CA");
	}

	[Fact]
	public async Task GivenTextWithPlaceholder_WhenTranslated_ThenPlaceholderIsProtectedAndRestored()
	{
		// Given — the response echoes back what a provider would return with
		// the ignored tag intact
		var (translator, transport) = Translator(Responds("Montrant <ph>{count}</ph> rapports"));

		// When
		var translated = await translator.Translate(
			["Showing {count} reports"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then — '{count}' is never translated to '{compte}'
		translated.ShouldBe(["Montrant {count} rapports"]);

		var sent = transport.LastBody();
		sent.GetProperty("text")[0].GetString().ShouldBe("Showing <ph>{count}</ph> reports");
		sent.GetProperty("tag_handling").GetString().ShouldBe("xml");
		sent.GetProperty("ignore_tags")[0].GetString().ShouldBe("ph");
	}

	[Fact]
	public async Task GivenTextWithMarkupCharacters_WhenTranslated_ThenTheySurviveRoundTrip()
	{
		// Given — tag_handling: xml means a literal '<' would be read as markup
		var (translator, transport) = Translator(Responds("Altitude &lt; 500 pieds &amp; en descente"));

		// When
		var translated = await translator.Translate(
			["Altitude < 500 feet & descending"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		translated.ShouldBe(["Altitude < 500 pieds & en descente"]);
		transport.LastBody().GetProperty("text")[0].GetString()
			.ShouldBe("Altitude &lt; 500 feet &amp; descending");
	}

	[Fact]
	public async Task GivenSeveralStrings_WhenTheyAreTranslated_ThenTheyComeBackInOrder()
	{
		// Given
		var (translator, _) = Translator(Responds("Un", "Deux", "Trois"));

		// When
		var translated = await translator.Translate(
			["One", "Two", "Three"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		translated.ShouldBe(["Un", "Deux", "Trois"]);
	}

	[Fact]
	public async Task GivenProviderReturnsWrongNumberOfResults_WhenAnswers_ThenRefused()
	{
		// Given — position is the only thing mapping a translation to its field
		var (translator, _) = Translator(Responds("Un"));

		// When / Then
		await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["One", "Two"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
	}

	[Fact]
	public async Task GivenProviderRefuses_WhenAnswers_ThenFailureCarriesNoProviderBody()
	{
		// Given — a DeepL error body can echo the submitted text back
		var (translator, _) = Translator(new StubTransport(
			new HttpResponseMessage(HttpStatusCode.Forbidden)
			{
				Content = new StringContent("{\"message\":\"Wrong endpoint. Use api-free. Text: Were you injured?\"}")
			}));

		// When
		var cause = await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));

		// Then
		cause.Message.ShouldContain("403");
		cause.Message.ShouldNotContain("Were you injured?");
		cause.Message.ShouldNotContain(Key);
	}

	[Fact]
	public async Task GivenProviderCannotBeReached_WhenCalled_ThenFailureIsReportedSafely()
	{
		// Given
		var (translator, _) = Translator(new StubTransport(new HttpRequestException("no route to host")));

		// When
		var cause = await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));

		// Then
		cause.Message.ShouldBe("The translation service could not be reached.");
		cause.Message.ShouldNotContain(Key);
	}

	[Fact]
	public async Task GivenProviderAnswersWithNoTranslationsAtAll_WhenParsed_ThenRefused()
	{
		// Given — a 200 with a body that carries no translations array
		var (translator, _) = Translator(new StubTransport(
			new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{}", Encoding.UTF8, "application/json")
			}));

		// When / Then
		await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
	}

	[Fact]
	public async Task GivenProviderAnswersWithNonsense_WhenParsed_ThenRefused()
	{
		// Given
		var (translator, _) = Translator(new StubTransport(
			new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json at all") }));

		// When / Then
		await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
	}

	[Fact]
	public async Task GivenFreeTierKey_WhenTranslationIsRequested_ThenFreeHostIsUsed()
	{
		// Given — DeepL marks free keys with ':fx' and serves the tiers from
		// different hosts; getting this wrong is a 403 that reads like a bad key
		var (translator, transport) = Translator(Responds("Un"), "abc:fx");

		// When
		await translator.Translate(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		transport.Requests[0].RequestUri!.Host.ShouldBe("api-free.deepl.com");
	}

	[Fact]
	public async Task GivenPaidKey_WhenTranslationIsRequested_ThenPaidHostIsUsed()
	{
		// Given
		var (translator, transport) = Translator(Responds("Un"), "abc");

		// When
		await translator.Translate(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		transport.Requests[0].RequestUri!.Host.ShouldBe("api.deepl.com");
	}

	[Fact]
	public async Task GivenConfiguredEndpoint_WhenTranslationIsRequested_ThenOverridesDerivedHost()
	{
		// Given
		var (translator, transport) = Translator(
			Responds("Un"), endpoint: "https://translate.example.invalid/v2/translate");

		// When
		await translator.Translate(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		transport.Requests[0].RequestUri!.Host.ShouldBe("translate.example.invalid");
	}

	[Fact]
	public async Task GivenCredential_WhenTranslationIsRequested_ThenSentAsDeeplAuthHeader()
	{
		// Given
		var (translator, transport) = Translator(Responds("Un"));

		// When
		await translator.Translate(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		var authorization = transport.Requests[0].Headers.Authorization;
		authorization!.Scheme.ShouldBe("DeepL-Auth-Key");
		authorization.Parameter.ShouldBe(Key);
	}

	private static StubTransport Responds(params string[] translations)
	{
		return new StubTransport(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(
				JsonSerializer.Serialize(new { translations = translations.Select(text => new { text }) }),
				Encoding.UTF8,
				"application/json")
		});
	}

	private static (DeepLTranslator Translator, StubTransport Transport) Translator(
		StubTransport? transport = null, string? apiKey = Key, string? endpoint = null)
	{
		transport ??= Responds("Un");

		var options = Options.Create(new DeepLOptions
		{
			ApiKey = apiKey,
			Endpoint = endpoint
		});

		return (new DeepLTranslator(new StubClientFactory(transport), options), transport);
	}

	/// <summary>Captures what was sent and replays a canned response.</summary>
	private sealed class StubTransport : HttpMessageHandler
	{
		private readonly List<string> _bodies = [];
		private readonly Exception? _failure;
		private readonly HttpResponseMessage? _response;

		public StubTransport(HttpResponseMessage response)
		{
			_response = response;
		}

		public StubTransport(Exception failure)
		{
			_failure = failure;
		}

		public List<HttpRequestMessage> Requests { get; } = [];

		public JsonElement LastBody()
		{
			return JsonDocument.Parse(_bodies[^1]).RootElement;
		}

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(request);

			if (request.Content is not null)
			{
				_bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
			}

			return _failure is not null ? throw _failure : _response!;
		}
	}

	private sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient(handler, false);
		}
	}
}
