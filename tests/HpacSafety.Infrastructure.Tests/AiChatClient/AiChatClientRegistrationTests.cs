using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.AiChatClient;

/// <summary>How the AI chat client is wired up, and the fail-closed default it registers.</summary>
public class AiChatClientRegistrationTests
{
	[Fact]
	public void GivenNoProviderReviewedYet_WhenRegistered_ThenTheFailClosedDefaultIsUsed()
	{
		// Given
		using var provider = new ServiceCollection()
			.AddHpacSafetyAiChatClient()
			.BuildServiceProvider();

		// When
		var client = provider.GetRequiredService<IAiChatClient>();

		// Then
		client.ShouldBeOfType<UnconfiguredAiChatClient>();
		client.IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void GivenNullServices_WhenRegistered_ThenRefused()
	{
		// Given / When / Then
		Should.Throw<ArgumentNullException>(() =>
			AiChatClientServiceCollectionExtensions.AddHpacSafetyAiChatClient(null!));
	}
}

/// <summary>
///     <see cref="UnconfiguredAiChatClient" /> is the fail-closed default: it never
///     silently proceeds without a reviewed provider.
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
			client.CompleteAsync("any-model", [new ChatMessage(ChatRole.User, "hello")], CancellationToken.None));
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
