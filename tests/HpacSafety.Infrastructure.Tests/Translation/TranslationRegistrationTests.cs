using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Translation;

/// <summary>
/// How the translation provider is wired up, including the case that matters
/// most in practice: a checkout with no credential at all.
/// </summary>
public class TranslationRegistrationTests
{
    [Fact]
    public void Given_no_configuration_at_all_When_translation_is_registered_Then_it_resolves_and_reports_unconfigured()
    {
        // Given — an ordinary local checkout
        using var provider = Provider([]);

        // When
        var translator = provider.GetRequiredService<ITranslator>();

        // Then — registration never fails for a missing credential; the
        // endpoint reports unavailability instead. See ADR-0062.
        translator.ShouldBeOfType<DeepLTranslator>();
        translator.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_key_in_the_translation_section_When_it_is_registered_Then_it_is_configured()
    {
        // Given
        using var provider = Provider(new Dictionary<string, string?>
        {
            ["Translation:ApiKey"] = "abc:fx",
        });

        // When
        var translator = provider.GetRequiredService<ITranslator>();

        // Then
        translator.IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public void Given_only_the_bare_environment_name_When_it_is_registered_Then_that_key_is_used()
    {
        // Given — DEEPL_API_KEY is the name the credential already has, in
        // repository settings and in tools/translator.mjs
        using var provider = Provider(new Dictionary<string, string?>
        {
            ["DEEPL_API_KEY"] = "abc:fx",
        });

        // When
        var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

        // Then
        options.ApiKey.ShouldBe("abc:fx");
        provider.GetRequiredService<ITranslator>().IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public void Given_both_names_When_it_is_registered_Then_the_explicit_section_wins()
    {
        // Given
        using var provider = Provider(new Dictionary<string, string?>
        {
            ["Translation:ApiKey"] = "explicit",
            ["DEEPL_API_KEY"] = "fallback",
        });

        // When
        var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

        // Then
        options.ApiKey.ShouldBe("explicit");
    }

    [Fact]
    public void Given_a_configured_formality_When_it_is_registered_Then_it_overrides_the_default()
    {
        // Given
        using var provider = Provider(new Dictionary<string, string?>
        {
            ["Translation:Formality"] = "prefer_less",
        });

        // When
        var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

        // Then
        options.Formality.ShouldBe("prefer_less");
    }

    [Fact]
    public void Given_no_formality_When_it_is_registered_Then_it_defaults_to_the_formal_form()
    {
        // Given — a national association addressing pilots uses "vous"
        using var provider = Provider([]);

        // When
        var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

        // Then
        options.Formality.ShouldBe("prefer_more");
    }

    [Fact]
    public void Given_a_null_argument_When_translation_is_registered_Then_it_is_refused()
    {
        // Given / When / Then
        Should.Throw<ArgumentNullException>(() =>
            TranslationServiceCollectionExtensions.AddHpacSafetyTranslation(
                null!, new ConfigurationBuilder().Build()));

        Should.Throw<ArgumentNullException>(() =>
            new ServiceCollection().AddHpacSafetyTranslation(null!));
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddHpacSafetyTranslation(configuration)
            .BuildServiceProvider();
    }
}

/// <summary>
/// <see cref="TranslationUnavailableException"/> is what every translation
/// failure becomes, and its message is the one thing a caller is allowed to
/// show.
/// </summary>
public class TranslationUnavailableExceptionTests
{
    [Fact]
    public void Given_no_message_When_it_is_created_Then_it_still_says_something_usable()
    {
        // Given / When
        var cause = new TranslationUnavailableException();

        // Then
        cause.Message.ShouldBe("Translation is unavailable.");
    }

    [Fact]
    public void Given_a_message_When_it_is_created_Then_the_message_is_kept()
    {
        // Given / When
        var cause = new TranslationUnavailableException("The translation service answered 403.");

        // Then
        cause.Message.ShouldBe("The translation service answered 403.");
        cause.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Given_an_underlying_failure_When_it_is_wrapped_Then_the_cause_is_kept_but_not_the_message()
    {
        // Given
        var underlying = new HttpRequestException("connection refused to api.deepl.com with key abc:fx");

        // When
        var cause = new TranslationUnavailableException("The translation service could not be reached.", underlying);

        // Then — the safe message is what a caller sees; the detail stays inside
        cause.Message.ShouldBe("The translation service could not be reached.");
        cause.Message.ShouldNotContain("abc:fx");
        cause.InnerException.ShouldBe(underlying);
    }
}
