namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     Configuration for <see cref="GeminiChatClient" />, bound from the
///     <c>Gemini</c> configuration section.
/// </summary>
/// <remarks>
///     The key reaches a running task as an environment variable supplied by the
///     deploy workflow; it is never sent to the browser, never logged, and never
///     included in a problem response. The model identifier itself lives on the
///     provider-agnostic <see cref="AiChatClientOptions" /> — this options class
///     carries only what is specific to reaching Gemini.
/// </remarks>
public sealed class GeminiOptions
{
	/// <summary>The configuration section this binds from.</summary>
	public const string SectionName = "Gemini";

	/// <summary>
	///     The Gemini API key. Absent in an ordinary local checkout, which is why
	///     <see cref="GeminiChatClient.IsConfigured" /> exists rather than a startup
	///     failure.
	/// </summary>
	public string? ApiKey { get; set; }

	/// <summary>
	///     Overrides the API host. Normally left unset — Gemini's OpenAI-compatible
	///     chat-completions endpoint is the default.
	/// </summary>
	public string? Endpoint { get; set; }
}
