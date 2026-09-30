using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     The translator's own model and reasoning level, bound from the
///     <c>Translation</c> configuration section (ADR-0179).
/// </summary>
/// <remarks>
///     Translation reuses the Gemini key and provider the summary call already holds
///     (<see cref="HpacSafety.Infrastructure.AiChatClient.AiChatClientOptions" />,
///     ADR-0104): no second key, secret, or Secrets Manager entry exists. What it
///     owns is these two settings, so a literal translation can be tuned apart from
///     the summary without touching it. Both have defaults, so a host that names
///     neither gets <c>gemini-3.7-flash</c> at low reasoning; a blank or undefined
///     value stops the host at startup, naming the setting.
/// </remarks>
public sealed class TranslationOptions
{
	/// <summary>The configuration section this binds from.</summary>
	public const string SectionName = "Translation";

	/// <summary>The default model.</summary>
	public const string DefaultModel = "gemini-3.7-flash";

	/// <summary>The default reasoning level.</summary>
	public const string DefaultReasoningEffort = "low";

	/// <summary>The provider-specific model identifier translation asks for.</summary>
	public string? Model { get; set; } = DefaultModel;

	/// <summary>
	///     How much the model may think before it answers: <c>low</c>,
	///     <c>medium</c>, or <c>high</c>, in any case. Held as text, not the
	///     enum, so a bad value is a validation failure that names this setting
	///     rather than a binder exception.
	/// </summary>
	public string? ReasoningEffort { get; set; } = DefaultReasoningEffort;

	/// <summary>The reasoning level as the port's enum, or <see langword="null" /> when it is not one.</summary>
	public ReasoningEffort? ParsedReasoningEffort =>
		Enum.TryParse<ReasoningEffort>(ReasoningEffort?.Trim(), true, out var effort)
		&& Enum.IsDefined(effort)
			? effort
			: null;
}
