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
	public void GivenKeyInGeminiSection_WhenRegistered_ThenGeminiIsUsed()
	{
		// Given
		using var provider = Provider(new Dictionary<string, string?>
		{
			["Gemini:ApiKey"] = "abc123"
		});

		// When
		var client = provider.GetRequiredService<IAiChatClient>();

		// Then
		client.ShouldBeOfType<GeminiChatClient>();
		client.IsConfigured.ShouldBeTrue();
	}

	[Fact]
	public void GivenOnlyBareEnvironmentName_WhenRegistered_ThenKeyIsUsed()
	{
		// Given — GEMINI_API_KEY is the name the credential has in repository
		// secrets and in the deploy workflow
		using var provider = Provider(new Dictionary<string, string?>
		{
			["GEMINI_API_KEY"] = "abc123"
		});

		// When
		var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;

		// Then
		options.ApiKey.ShouldBe("abc123");
		provider.GetRequiredService<IAiChatClient>().IsConfigured.ShouldBeTrue();
	}

	[Fact]
	public void GivenBothNames_WhenRegistered_ThenExplicitSectionWins()
	{
		// Given
		using var provider = Provider(new Dictionary<string, string?>
		{
			["Gemini:ApiKey"] = "explicit",
			["GEMINI_API_KEY"] = "fallback"
		});

		// When
		var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;

		// Then
		options.ApiKey.ShouldBe("explicit");
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
			client.Complete("any-model", [new ChatMessage(ChatRole.User, "hello")], CancellationToken.None));
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
