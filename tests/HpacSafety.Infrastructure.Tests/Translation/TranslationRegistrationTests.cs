using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
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
		using var provider = Provider(new Dictionary<string, string?>());

		// When
		var translator = provider.GetRequiredService<ITranslator>();

		// Then — registration never fails for a missing credential; the
		// endpoint reports unavailability instead. See ADR-0062.
		translator.ShouldBeOfType<AiChatTranslator>();
		translator.IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void GivenSummaryGeminiKey_WhenRegistered_ThenTranslationIsConfiguredByIt()
	{
		// Given — the one key of ADR-0104; there is no translation-only key
		using var provider = Provider(new Dictionary<string, string?>
		{
			["AiChatClient:ApiKey"] = "gemini-key",
			["AiChatClient:Provider"] = "Gemini",
		});

		// When
		var translator = provider.GetRequiredService<ITranslator>();

		// Then
		translator.IsConfigured.ShouldBeTrue();
		provider.GetRequiredService<IAiChatClient>().ShouldBeOfType<GeminiChatClient>();
	}

	[Fact]
	public void GivenOnlyAKeyUnderTheOldTranslationName_WhenRegistered_ThenStillUnconfigured()
	{
		// Given — a stale environment that still carries the retired name
		using var provider = Provider(new Dictionary<string, string?>
		{
			["Translation:ApiKey"] = "abc:fx",
			["OLD_TRANSLATION_API_KEY"] = "abc:fx",
		});

		// When / Then
		provider.GetRequiredService<ITranslator>().IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void GivenKeyWithUnknownProvider_WhenStartupValidates_ThenStartupFailsNamingProvider()
	{
		// Given — the API validates the provider too, though it has no summary model
		using var provider = Provider(new Dictionary<string, string?>
		{
			["AiChatClient:ApiKey"] = "gemini-key",
			["AiChatClient:Provider"] = "Anthropic",
		});

		// When
		var failure = Record.Exception(() => provider.GetRequiredService<IStartupValidator>().Validate());

		// Then
		failure.ShouldBeOfType<OptionsValidationException>().Message.ShouldContain("AiChatClient:Provider");
	}

	[Fact]
	public void GivenKeyAndNoSummaryModel_WhenStartupValidates_ThenTranslationStillStarts()
	{
		// Given — the API's appsettings names no summary model or reasoning level
		using var provider = Provider(new Dictionary<string, string?>
		{
			["AiChatClient:ApiKey"] = "gemini-key",
			["AiChatClient:Provider"] = "Gemini",
		});

		// When / Then
		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	[Fact]
	public void GivenNoSettings_WhenBound_ThenDefaultsToGeminiFlashAtLowReasoning()
	{
		// Given
		using var provider = Provider(new Dictionary<string, string?>());

		// When
		var options = provider.GetRequiredService<IOptions<TranslationOptions>>().Value;

		// Then
		options.Model.ShouldBe("gemini-3.7-flash");
		options.ParsedReasoningEffort.ShouldBe(ReasoningEffort.Low);
		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	[Fact]
	public void GivenOwnSettings_WhenBound_ThenTheyAreRead()
	{
		// Given
		using var provider = Provider(new Dictionary<string, string?>
		{
			["Translation:Model"] = "gemini-3.5-pro",
			["Translation:ReasoningEffort"] = "High",
		});

		// When
		var options = provider.GetRequiredService<IOptions<TranslationOptions>>().Value;

		// Then
		options.Model.ShouldBe("gemini-3.5-pro");
		options.ParsedReasoningEffort.ShouldBe(ReasoningEffort.High);
	}

	[Theory]
	[InlineData("Translation:Model", "", "Translation:Model")]
	[InlineData("Translation:Model", "   ", "Translation:Model")]
	[InlineData("Translation:ReasoningEffort", "extreme", "Translation:ReasoningEffort")]
	[InlineData("Translation:ReasoningEffort", "", "Translation:ReasoningEffort")]
	[InlineData("Translation:ReasoningEffort", "7", "Translation:ReasoningEffort")]
	public void GivenUnusableSetting_WhenStartupValidates_ThenStartupFailsNamingIt(string key,
																				   string value,
																				   string named)
	{
		// Given — key or no key: a bad model is a stream of failed translations
		using var provider = Provider(new Dictionary<string, string?> { [key] = value });

		// When
		var failure = Record.Exception(() => provider.GetRequiredService<IStartupValidator>().Validate());

		// Then
		failure.ShouldBeOfType<OptionsValidationException>().Message.ShouldContain(named);
	}

	[Fact]
	public void GivenWorkerRegistersBothInEitherOrder_WhenResolved_ThenOneClientIsUsed()
	{
		// Given
		var settings = new Dictionary<string, string?>
		{
			["AiChatClient:ApiKey"] = "gemini-key",
			["AiChatClient:Provider"] = "Gemini",
			["AiChatClient:Model"] = "gemini-3.7-flash",
			["AiChatClient:ReasoningEffort"] = "low",
		};
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		foreach (var translationFirst in new[] { true, false })
		{
			var services = new ServiceCollection();

			if (translationFirst)
			{
				services.AddHpacSafetyTranslation(configuration).AddHpacSafetyAiChatClient(configuration);
			}
			else
			{
				services.AddHpacSafetyAiChatClient(configuration).AddHpacSafetyTranslation(configuration);
			}

			// When
			using var provider = services.BuildServiceProvider();

			// Then
			services.Count(descriptor => descriptor.ServiceType == typeof(IAiChatClient)).ShouldBe(1);
			provider.GetRequiredService<IAiChatClient>().ShouldBeOfType<GeminiChatClient>();
			Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
		}
	}

	[Fact]
	public async Task GivenNoCredential_WhenTextIsTranslated_ThenRefusedRatherThanEchoed()
	{
		// Given — any environment, Development included: a stand-in that
		// returned its input unchanged got stored as a translation (ADR-0109)
		using var provider = Provider(new Dictionary<string, string?>());
		var translator = provider.GetRequiredService<ITranslator>();

		// When / Then
		await Should.ThrowAsync<TranslationUnavailableException>(() =>
			translator.Translate(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));
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

	private static ServiceProvider Provider(Dictionary<string, string?> settings)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		return new ServiceCollection()
			.AddHpacSafetyTranslation(configuration)
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
		var underlying = new HttpRequestException("connection refused to the provider with key abc:fx");

		// When
		var cause = new TranslationUnavailableException("The translation service could not be reached.", underlying);

		// Then — the safe message is what a caller sees; the detail stays inside
		cause.Message.ShouldBe("The translation service could not be reached.");
		cause.Message.ShouldNotContain("abc:fx");
		cause.InnerException.ShouldBe(underlying);
	}
}
