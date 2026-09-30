using HpacSafety.Core;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     The model provider's key, the summary model, and the reasoning level, bound together
///     from the <c>AiChatClient</c> configuration section (ADR-0104). There is no provider
///     setting: the model name picks the handler (<see cref="AiMediator" />).
/// </summary>
/// <remarks>
///     In every deployed environment the key is resolved from
///     <see cref="ApiKeySecretArn" /> at cold start (#597), not read from
///     configuration directly — the Lambda environment never carries the
///     key's own value. Everywhere else (Development, every test host) it is
///     the plain <c>AiChatClient__ApiKey</c> environment variable, never
///     committed. Either way it is never sent to the browser, logged, or
///     included in a problem response. Without a key the
///     <see cref="AiMediator" /> reports itself unconfigured and nothing else is
///     checked; with one, the validators stop the host
///     at startup unless every other setting is usable.
/// </remarks>
public sealed class AiChatClientOptions
{
	/// <summary>The configuration section this binds to.</summary>
	public const string SectionName = "AiChatClient";

	/// <summary>The provider's API key. Absent in an ordinary local checkout.</summary>
	public string? ApiKey { get; set; }

	/// <summary>
	///     The Secrets Manager ARN Terraform sets in every deployed environment
	///     (<c>infra/lambda.tf</c>). When present, <see cref="ApiKey" /> is
	///     resolved from this secret's current value at cold start instead of
	///     from <see cref="ApiKey" />'s own configured value (#597).
	/// </summary>
	public string? ApiKeySecretArn { get; set; }

	/// <summary>The summary's model identifier; its prefix, such as <c>gemini-</c>, picks the provider handler.</summary>
	public string? Model { get; set; }

	/// <summary>How much the model may think before it answers.</summary>
	public ReasoningEffort? ReasoningEffort { get; set; }

	/// <summary>
	///     Overrides the provider's API endpoint. Normally left unset — each handler
	///     has its own default.
	/// </summary>
	public string? Endpoint { get; set; }
}
