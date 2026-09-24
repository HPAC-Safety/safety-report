using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.AiChatClient;

/// <summary>How the AI chat client is wired up, including the case that matters most in practice: no credential at all.</summary>
public class AiChatClientRegistrationTests
{
	[Fact]
	public void GivenNoConfigurationAtAll_WhenRegistered_ThenTheFailClosedDefaultIsUsed()
	{
		// Given — an ordinary local checkout
		using var provider = Provider([]);

		// When
		var client = provider.GetRequiredService<IAiChatClient>();

		// Then
		client.ShouldBeOfType<UnconfiguredAiChatClient>();
		client.IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void GivenKeyAndGeminiProvider_WhenRegistered_ThenGeminiStrategyIsUsed()
	{
		// Given
		using var provider = Provider(Usable());

		// When
		var client = provider.GetRequiredService<IAiChatClient>();

		// Then
		client.ShouldBeOfType<GeminiChatClient>();
		client.IsConfigured.ShouldBeTrue();
	}

	[Fact]
	public void GivenProviderNameInAnotherCase_WhenRegistered_ThenStillSelectsGemini()
	{
		// Given
		using var provider = Provider(Usable(("AiChatClient:Provider", "gemini")));

		// When / Then
		provider.GetRequiredService<IAiChatClient>().ShouldBeOfType<GeminiChatClient>();
		Should.NotThrow(() => Validate(provider));
	}

	[Fact]
	public void GivenUsableConfiguration_WhenBound_ThenModelAndReasoningLevelAreRead()
	{
		// Given
		using var provider = Provider(Usable(("AiChatClient:ReasoningEffort", "Medium")));

		// When
		var options = provider.GetRequiredService<IOptions<AiChatClientOptions>>().Value;

		// Then
		options.Model.ShouldBe("gemini-3.7-flash");
		options.ReasoningEffort.ShouldBe(ReasoningEffort.Medium);
		Should.NotThrow(() => Validate(provider));
	}

	[Theory]
	[InlineData("AiChatClient:Provider", "Anthropic", "Provider")]
	[InlineData("AiChatClient:Provider", "", "Provider")]
	[InlineData("AiChatClient:Model", "", "Model")]
	[InlineData("AiChatClient:Model", "   ", "Model")]
	[InlineData("AiChatClient:ReasoningEffort", "", "ReasoningEffort")]
	[InlineData("AiChatClient:ReasoningEffort", "7", "ReasoningEffort")]
	public void GivenKeyWithUnusableSetting_WhenStartupValidates_ThenStartupFails(string key,
																				   string value,
																				   string named)
	{
		// Given
		using var provider = Provider(Usable((key, value)));

		// When
		var failure = Should.Throw<OptionsValidationException>(() => Validate(provider));

		// Then
		failure.Message.ShouldContain(named);
		failure.Message.ShouldNotContain("abc123");
	}

	[Theory]
	[InlineData("minimal")]
	[InlineData("bogus")]
	public void GivenKeyWithUnknownReasoningWord_WhenStartupValidates_ThenStartupFails(string value)
	{
		// Given — the binder refuses a word that is not a ReasoningEffort name
		using var provider = Provider(Usable(("AiChatClient:ReasoningEffort", value)));

		// When / Then
		Should.Throw<InvalidOperationException>(() => Validate(provider));
	}

	[Fact]
	public void GivenKeyWithUnknownProvider_WhenRegistered_ThenFailClosedClientIsUsed()
	{
		// Given
		using var provider = Provider(Usable(("AiChatClient:Provider", "Anthropic")));

		// When / Then — nothing is sent even if startup validation were skipped
		provider.GetRequiredService<IAiChatClient>().ShouldBeOfType<UnconfiguredAiChatClient>();
	}

	[Fact]
	public void GivenNoKey_WhenStartupValidates_ThenPasses()
	{
		// Given — a local checkout: provider and model committed, no key
		using var provider = Provider(new Dictionary<string, string?>
		{
			["AiChatClient:Provider"] = "Gemini",
			["AiChatClient:Model"] = "gemini-3.7-flash",
			["AiChatClient:ReasoningEffort"] = "low",
		});

		// When / Then
		Should.NotThrow(() => Validate(provider));
		provider.GetRequiredService<IAiChatClient>().ShouldBeOfType<UnconfiguredAiChatClient>();
	}

	[Fact]
	public void GivenNullArguments_WhenRegistered_ThenRefused()
	{
		// Given / When / Then
		Should.Throw<ArgumentNullException>(() =>
			AiChatClientServiceCollectionExtensions.AddHpacSafetyAiChatClient(null!, new ConfigurationBuilder().Build()));

		Should.Throw<ArgumentNullException>(() =>
			new ServiceCollection().AddHpacSafetyAiChatClient(null!));
	}

	private static Dictionary<string, string?> Usable(params (string Key, string? Value)[] overrides)
	{
		var settings = new Dictionary<string, string?>
		{
			["AiChatClient:Provider"] = "Gemini",
			["AiChatClient:ApiKey"] = "abc123",
			["AiChatClient:Model"] = "gemini-3.7-flash",
			["AiChatClient:ReasoningEffort"] = "low",
		};

		foreach (var (key, value) in overrides)
		{
			settings[key] = value;
		}

		return settings;
	}

	/// <summary>What the host runs at startup for every <c>ValidateOnStart</c> registration.</summary>
	private static void Validate(ServiceProvider provider)
	{
		provider.GetRequiredService<IStartupValidator>().Validate();
	}

	private static ServiceProvider Provider(Dictionary<string, string?> settings)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		return new ServiceCollection()
			.AddHpacSafetyAiChatClient(configuration)
			.BuildServiceProvider();
	}
}

/// <summary>
///     <see cref="UnconfiguredAiChatClient" /> is the fail-closed default: it never
///     silently proceeds without a configured provider.
/// </summary>
public class UnconfiguredAiChatClientTests
{
	[Fact]
	public async Task GivenNoProvider_WhenACompletionIsRequested_ThenRefused()
	{
		// Given
		var client = new UnconfiguredAiChatClient();

		// When / Then
		await Should.ThrowAsync<AiChatClientUnavailableException>(() =>
			client.Complete(
				new AiChatRequest("any-model", ReasoningEffort.Low, [new ChatMessage(ChatRole.User, "hello")]),
				CancellationToken.None));
	}
}

/// <summary>
///     <see cref="AiChatClientUnavailableException" /> is what every chat-client
///     failure becomes, and its message is the one thing a caller is allowed to show.
/// </summary>
public class AiChatClientUnavailableExceptionTests
{
	[Fact]
	public void GivenNoMessage_WhenCreated_ThenStillSaysSomethingUsable()
	{
		// Given / When
		var cause = new AiChatClientUnavailableException();

		// Then
		cause.Message.ShouldBe("The AI chat client is unavailable.");
	}

	[Fact]
	public void GivenUnderlyingFailure_WhenWrapped_ThenCauseIsKeptButNotMessage()
	{
		// Given
		var underlying = new HttpRequestException("connection refused with key abc123");

		// When
		var cause = new AiChatClientUnavailableException("The provider could not be reached.", underlying);

		// Then
		cause.Message.ShouldBe("The provider could not be reached.");
		cause.Message.ShouldNotContain("abc123");
		cause.InnerException.ShouldBe(underlying);
	}
}
