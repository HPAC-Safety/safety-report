using HpacSafety.Core;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     The fail-closed strategy. No AI chat provider is configured — no credential
///     is present — so every call is refused rather than silently sending report
///     content anywhere.
/// </summary>
/// <remarks>Registered whenever <see cref="AiChatClientOptions.ApiKey" /> is absent.</remarks>
public sealed class UnconfiguredAiChatClient : IAiChatClient
{
	/// <inheritdoc />
	public bool IsConfigured => false;

	/// <inheritdoc />
	public Task<string> Complete(AiChatRequest request,
								 CancellationToken cancellationToken)
	{
		throw new AiChatClientUnavailableException(
			"No AI chat provider is configured and approved for use.");
	}
}
