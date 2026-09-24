using HpacSafety.Core;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     The model provider, its key, the model, and the reasoning level, bound together
///     from the <c>AiChatClient</c> configuration section (ADR-0104).
/// </summary>
/// <remarks>
///     The key reaches a running task as an environment variable
///     (<c>AiChatClient__ApiKey</c>); it is never committed, sent to the browser,
///     logged, or included in a problem response. Without a key the fail-closed
///     <see cref="UnconfiguredAiChatClient" /> is registered and nothing else is
///     checked; with one, <see cref="AiChatClientOptionsValidator" /> stops the host
///     at startup unless every other setting is usable.
/// </remarks>
public sealed class AiChatClientOptions
{
	/// <summary>The configuration section this binds to.</summary>
	public const string SectionName = "AiChatClient";

	/// <summary>Which <see cref="IAiChatClient" /> strategy runs, such as <c>Gemini</c>.</summary>
	public string? Provider { get; set; }

	/// <summary>The provider's API key. Absent in an ordinary local checkout.</summary>
	public string? ApiKey { get; set; }

	/// <summary>The provider-specific model identifier to request completions from.</summary>
	public string? Model { get; set; }

	/// <summary>How much the model may think before it answers.</summary>
	public ReasoningEffort? ReasoningEffort { get; set; }

	/// <summary>
	///     Overrides the provider's API endpoint. Normally left unset — each strategy
	///     has its own default.
	/// </summary>
	public string? Endpoint { get; set; }
}
