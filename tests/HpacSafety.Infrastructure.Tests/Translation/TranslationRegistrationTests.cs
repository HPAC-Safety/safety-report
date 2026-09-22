using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Translation;

/// <summary>
///     How the translation provider is wired up, including the case that matters
///     most in practice: a checkout with no credential at all.
/// </summary>
public class TranslationRegistrationTests
{
	[Fact]
	public void GivenNoConfigurationAtAll_WhenTranslationIsRegistered_ThenResolvesAndReportsUnconfigured()
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
	public void GivenKeyInTranslationSection_WhenRegistered_ThenConfigured()
	{
		// Given
		using var provider = Provider(new Dictionary<string, string?>
		{
			["Translation:ApiKey"] = "abc:fx"
		});

		// When
		var translator = provider.GetRequiredService<ITranslator>();

		// Then
		translator.IsConfigured.ShouldBeTrue();
	}

	[Fact]
	public void GivenOnlyBareEnvironmentName_WhenRegistered_ThenKeyIsUsed()
	{
		// Given — DEEPL_API_KEY is the name the credential already has, in
		// repository settings and in tools/translator.mjs
		using var provider = Provider(new Dictionary<string, string?>
		{
			["DEEPL_API_KEY"] = "abc:fx"
		});

		// When
		var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

		// Then
		options.ApiKey.ShouldBe("abc:fx");
		provider.GetRequiredService<ITranslator>().IsConfigured.ShouldBeTrue();
	}

	[Fact]
	public void GivenBothNames_WhenRegistered_ThenExplicitSectionWins()
	{
		// Given
		using var provider = Provider(new Dictionary<string, string?>
		{
			["Translation:ApiKey"] = "explicit",
			["DEEPL_API_KEY"] = "fallback"
		});

		// When
		var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

		// Then
		options.ApiKey.ShouldBe("explicit");
	}

	[Fact]
	public void GivenConfiguredFormality_WhenRegistered_ThenOverridesDefault()
	{
		// Given
		using var provider = Provider(new Dictionary<string, string?>
		{
			["Translation:Formality"] = "prefer_less"
		});

		// When
		var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

		// Then
		options.Formality.ShouldBe("prefer_less");
	}

	[Fact]
	public void GivenNoFormality_WhenRegistered_ThenDefaultsToFormalForm()
	{
		// Given — a national association addressing pilots uses "vous"
		using var provider = Provider([]);

		// When
		var options = provider.GetRequiredService<IOptions<DeepLOptions>>().Value;

		// Then
		options.Formality.ShouldBe("prefer_more");
	}

	[Fact]
	public void GivenDevelopmentAndNoCredential_WhenRegistered_ThenStandInIsUsed()
	{
		// Given — a developer's checkout
		using var provider = Provider([], true);

		// When
		var translator = provider.GetRequiredService<ITranslator>();

		// Then — the control works locally and exercises the same port
		translator.ShouldBeOfType<EchoTranslator>();
		translator.IsConfigured.ShouldBeTrue();
	}

	[Fact]
	public void GivenDevelopmentAndCredential_WhenRegistered_ThenRealProviderWins()
	{
		// Given — a developer who does have a key wants the real thing
		using var provider = Provider(
			new Dictionary<string, string?> { ["Translation:ApiKey"] = "abc:fx" }, true);

		// When / Then
		provider.GetRequiredService<ITranslator>().ShouldBeOfType<DeepLTranslator>();
	}

	[Fact]
	public void GivenNoCredentialOutsideDevelopment_WhenRegistered_ThenNoStandInIsUsed()
	{
		// Given — copying English into the French column of a live question
		// bank would put untranslated English in front of French-speaking
		// pilots, so this is never a production fallback
		using var provider = Provider([]);

		// When
		var translator = provider.GetRequiredService<ITranslator>();

		// Then
		translator.ShouldBeOfType<DeepLTranslator>();
		translator.IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenStandIn_WhenTextIsTranslated_ThenComesBackUnchanged()
	{
		// Given
		var translator = new EchoTranslator();

		// When
		var translated = await translator.TranslateAsync(
			["Were you injured?", "Describe the weather"], Locale.EnCa, Locale.FrCa, CancellationToken.None);

		// Then
		translated.ShouldBe(["Were you injured?", "Describe the weather"]);
	}

	[Fact]
	public async Task GivenStandIn_WhenOneLanguageIsTranslatedIntoItself_ThenRefused()
	{
		// Given — the stand-in still honours the contract it stands in for
		var translator = new EchoTranslator();

		// When / Then
		await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.TranslateAsync(["One"], Locale.EnCa, Locale.EnCa, CancellationToken.None));
	}

	[Fact]
	public void GivenNullArgument_WhenTranslationIsRegistered_ThenRefused()
	{
		// Given / When / Then
		Should.Throw<ArgumentNullException>(() =>
			TranslationServiceCollectionExtensions.AddHpacSafetyTranslation(
				null!, new ConfigurationBuilder().Build()));

		Should.Throw<ArgumentNullException>(() =>
			new ServiceCollection().AddHpacSafetyTranslation(null!));
	}

	private static ServiceProvider Provider(Dictionary<string, string?> settings, bool useStandIn = false)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		return new ServiceCollection()
			.AddHpacSafetyTranslation(configuration, useStandIn)
			.BuildServiceProvider();
	}
}

/// <summary>
///     <see cref="TranslationUnavailableException" /> is what every translation
///     failure becomes, and its message is the one thing a caller is allowed to
///     show.
/// </summary>
public class TranslationUnavailableExceptionTests
{
	[Fact]
	public void GivenNoMessage_WhenCreated_ThenStillSaysSomethingUsable()
	{
		// Given / When
		var cause = new TranslationUnavailableException();

		// Then
		cause.Message.ShouldBe("Translation is unavailable.");
	}

	[Fact]
	public void GivenMessage_WhenCreated_ThenMessageIsKept()
	{
		// Given / When
		var cause = new TranslationUnavailableException("The translation service answered 403.");

		// Then
		cause.Message.ShouldBe("The translation service answered 403.");
		cause.InnerException.ShouldBeNull();
	}

	[Fact]
	public void GivenUnderlyingFailure_WhenWrapped_ThenCauseIsKeptButNotMessage()
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
