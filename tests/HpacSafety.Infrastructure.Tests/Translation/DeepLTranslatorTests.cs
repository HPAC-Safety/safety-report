using System.Net;
using System.Text;
using System.Text.Json;

using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;

using Microsoft.Extensions.Options;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Translation;

/// <summary>
/// The DeepL adapter, against a stubbed transport. Nothing here reaches the
/// network, and no real credential is used.
/// </summary>
public class DeepLTranslatorTests
{
    private const string Key = "test-key:fx";

    [Fact]
    public async Task Given_no_credential_When_a_translation_is_requested_Then_it_reports_that_it_is_unconfigured()
    {
        // Given
        var (translator, _) = Translator(apiKey: null);

        // Then
        translator.IsConfigured.ShouldBeFalse();

        await Should.ThrowAsync<TranslationUnavailableException>(() =>
            translator.TranslateAsync(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
    }

    [Fact]
    public async Task Given_one_language_When_it_is_translated_into_itself_Then_it_is_refused()
    {
        // Given
        var (translator, _) = Translator();

        // When / Then
        await Should.ThrowAsync<TranslationUnavailableException>(() =>
            translator.TranslateAsync(["Were you injured?"], Locale.EnCa, Locale.EnCa, CancellationToken.None));
    }

    [Fact]
    public async Task Given_nothing_to_translate_When_it_is_requested_Then_no_call_is_made()
    {
        // Given
        var (translator, transport) = Translator();

        // When
        var translated = await translator.TranslateAsync([], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then — a provider charged per request is not asked to translate nothing
        translated.ShouldBeEmpty();
        transport.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_english_When_it_is_translated_Then_the_request_asks_deepl_for_canadian_french()
    {
        // Given
        var (translator, transport) = Translator(Responds("Avez-vous été blessé ?"));

        // When
        var translated = await translator.TranslateAsync(
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
    public async Task Given_french_When_it_is_translated_back_Then_it_is_asked_for_as_plain_french()
    {
        // Given — FR-CA is a target-only variant in DeepL and cannot be a source
        var (translator, transport) = Translator(Responds("Were you injured?"));

        // When
        await translator.TranslateAsync(
            ["Avez-vous été blessé ?"], Locale.FrCa, Locale.EnCa, CancellationToken.None);

        // Then
        var sent = transport.LastBody();
        sent.GetProperty("source_lang").GetString().ShouldBe("FR");
        sent.GetProperty("target_lang").GetString().ShouldBe("EN-CA");
    }

    [Fact]
    public async Task Given_text_with_a_placeholder_When_it_is_translated_Then_the_placeholder_is_protected_and_restored()
    {
        // Given — the response echoes back what a provider would return with
        // the ignored tag intact
        var (translator, transport) = Translator(Responds("Montrant <ph>{count}</ph> rapports"));

        // When
        var translated = await translator.TranslateAsync(
            ["Showing {count} reports"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then — '{count}' is never translated to '{compte}'
        translated.ShouldBe(["Montrant {count} rapports"]);

        var sent = transport.LastBody();
        sent.GetProperty("text")[0].GetString().ShouldBe("Showing <ph>{count}</ph> reports");
        sent.GetProperty("tag_handling").GetString().ShouldBe("xml");
        sent.GetProperty("ignore_tags")[0].GetString().ShouldBe("ph");
    }

    [Fact]
    public async Task Given_text_with_markup_characters_When_it_is_translated_Then_they_survive_the_round_trip()
    {
        // Given — tag_handling: xml means a literal '<' would be read as markup
        var (translator, transport) = Translator(Responds("Altitude &lt; 500 pieds &amp; en descente"));

        // When
        var translated = await translator.TranslateAsync(
            ["Altitude < 500 feet & descending"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then
        translated.ShouldBe(["Altitude < 500 pieds & en descente"]);
        transport.LastBody().GetProperty("text")[0].GetString()
            .ShouldBe("Altitude &lt; 500 feet &amp; descending");
    }

    [Fact]
    public async Task Given_several_strings_When_they_are_translated_Then_they_come_back_in_order()
    {
        // Given
        var (translator, _) = Translator(Responds("Un", "Deux", "Trois"));

        // When
        var translated = await translator.TranslateAsync(
            ["One", "Two", "Three"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then
        translated.ShouldBe(["Un", "Deux", "Trois"]);
    }

    [Fact]
    public async Task Given_a_provider_that_returns_the_wrong_number_of_results_When_it_answers_Then_it_is_refused()
    {
        // Given — position is the only thing mapping a translation to its field
        var (translator, _) = Translator(Responds("Un"));

        // When / Then
        await Should.ThrowAsync<TranslationUnavailableException>(() =>
            translator.TranslateAsync(["One", "Two"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_provider_that_refuses_When_it_answers_Then_the_failure_carries_no_provider_body()
    {
        // Given — a DeepL error body can echo the submitted text back
        var (translator, _) = Translator(new StubTransport(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"message\":\"Wrong endpoint. Use api-free. Text: Were you injured?\"}"),
            }));

        // When
        var cause = await Should.ThrowAsync<TranslationUnavailableException>(() =>
            translator.TranslateAsync(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));

        // Then
        cause.Message.ShouldContain("403");
        cause.Message.ShouldNotContain("Were you injured?");
        cause.Message.ShouldNotContain(Key);
    }

    [Fact]
    public async Task Given_a_provider_that_cannot_be_reached_When_it_is_called_Then_the_failure_is_reported_safely()
    {
        // Given
        var (translator, _) = Translator(new StubTransport(new HttpRequestException("no route to host")));

        // When
        var cause = await Should.ThrowAsync<TranslationUnavailableException>(() =>
            translator.TranslateAsync(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));

        // Then
        cause.Message.ShouldBe("The translation service could not be reached.");
        cause.Message.ShouldNotContain(Key);
    }

    [Fact]
    public async Task Given_a_provider_that_answers_with_no_translations_at_all_When_it_is_parsed_Then_it_is_refused()
    {
        // Given — a 200 with a body that carries no translations array
        var (translator, _) = Translator(new StubTransport(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            }));

        // When / Then
        await Should.ThrowAsync<TranslationUnavailableException>(() =>
            translator.TranslateAsync(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_provider_that_answers_with_nonsense_When_it_is_parsed_Then_it_is_refused()
    {
        // Given
        var (translator, _) = Translator(new StubTransport(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json at all") }));

        // When / Then
        await Should.ThrowAsync<TranslationUnavailableException>(() =>
            translator.TranslateAsync(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_free_tier_key_When_a_translation_is_requested_Then_the_free_host_is_used()
    {
        // Given — DeepL marks free keys with ':fx' and serves the tiers from
        // different hosts; getting this wrong is a 403 that reads like a bad key
        var (translator, transport) = Translator(Responds("Un"), apiKey: "abc:fx");

        // When
        await translator.TranslateAsync(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then
        transport.Requests[0].RequestUri!.Host.ShouldBe("api-free.deepl.com");
    }

    [Fact]
    public async Task Given_a_paid_key_When_a_translation_is_requested_Then_the_paid_host_is_used()
    {
        // Given
        var (translator, transport) = Translator(Responds("Un"), apiKey: "abc");

        // When
        await translator.TranslateAsync(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then
        transport.Requests[0].RequestUri!.Host.ShouldBe("api.deepl.com");
    }

    [Fact]
    public async Task Given_a_configured_endpoint_When_a_translation_is_requested_Then_it_overrides_the_derived_host()
    {
        // Given
        var (translator, transport) = Translator(
            Responds("Un"), endpoint: "https://translate.example.invalid/v2/translate");

        // When
        await translator.TranslateAsync(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then
        transport.Requests[0].RequestUri!.Host.ShouldBe("translate.example.invalid");
    }

    [Fact]
    public async Task Given_a_credential_When_a_translation_is_requested_Then_it_is_sent_as_a_deepl_auth_header()
    {
        // Given
        var (translator, transport) = Translator(Responds("Un"));

        // When
        await translator.TranslateAsync(["One"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

        // Then
        var authorization = transport.Requests[0].Headers.Authorization;
        authorization!.Scheme.ShouldBe("DeepL-Auth-Key");
        authorization.Parameter.ShouldBe(Key);
    }

    private static StubTransport Responds(params string[] translations) =>
        new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { translations = translations.Select(text => new { text }) }),
                Encoding.UTF8,
                "application/json"),
        });

    private static (DeepLTranslator Translator, StubTransport Transport) Translator(
        StubTransport? transport = null, string? apiKey = Key, string? endpoint = null)
    {
        transport ??= Responds("Un");

        var options = Options.Create(new DeepLOptions
        {
            ApiKey = apiKey,
            Endpoint = endpoint,
        });

        return (new DeepLTranslator(new StubClientFactory(transport), options), transport);
    }

    /// <summary>Captures what was sent and replays a canned response.</summary>
    private sealed class StubTransport : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _failure;
        private readonly List<string> _bodies = [];

        public StubTransport(HttpResponseMessage response) => _response = response;

        public StubTransport(Exception failure) => _failure = failure;

        public List<HttpRequestMessage> Requests { get; } = [];

        public JsonElement LastBody() => JsonDocument.Parse(_bodies[^1]).RootElement;

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
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
