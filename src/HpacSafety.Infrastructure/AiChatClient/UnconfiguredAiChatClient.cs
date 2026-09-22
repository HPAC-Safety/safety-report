using HpacSafety.Core;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     The fail-closed default. No AI chat provider has been reviewed and approved
///     for retention, regional processing, and data use, so every call is refused
///     rather than silently sending report content anywhere.
/// </summary>
/// <remarks>
///     Registered until a concretion (see ADR-0081's follow-on issue for the first,
///     Google Gemini) is reviewed and wired in for a given environment.
/// </remarks>
public sealed class UnconfiguredAiChatClient : IAiChatClient
{
	/// <inheritdoc />
	public bool IsConfigured => false;

	/// <inheritdoc />
	public Task<string> CompleteAsync(string model, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
	{
		throw new AiChatClientUnavailableException(
			"No AI chat provider is configured and approved for use.");
	}
}
