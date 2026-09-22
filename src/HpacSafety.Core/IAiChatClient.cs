namespace HpacSafety.Core;

/// <summary>The role a chat message was authored under, per the OpenAI chat-completions spec.</summary>
public enum ChatRole
{
	System,
	User
}

/// <summary>One message in a chat-completion request.</summary>
public sealed record ChatMessage(ChatRole Role, string Content);

/// <summary>
///     A single-turn chat-completion call to an AI provider, shaped after the OpenAI
///     chat-completions request/response contract so any OpenAI-spec-compatible
///     provider can implement it without a caller-visible change.
/// </summary>
/// <remarks>
///     This is a transport-level port. The one caller in this codebase is the Worker's
///     summarizer (see <c>ISummarizer</c>), which owns the prompt, the request content,
///     and strict validation of the response — this interface only carries a request to
///     a provider and returns whatever text it answered with.
/// </remarks>
public interface IAiChatClient
{
	/// <summary>
	///     Whether a provider is configured and enabled. False when no credential is
	///     present, or when a production provider has not yet been reviewed and
	///     explicitly approved for retention, regional processing, and data use — the
	///     caller fails closed rather than sending report content to an unreviewed
	///     provider.
	/// </summary>
	bool IsConfigured { get; }

	/// <summary>Requests one completion for the given messages.</summary>
	/// <param name="model">The provider-specific model identifier to use.</param>
	/// <param name="messages">The conversation, in order.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The provider's response text, unparsed and unvalidated.</returns>
	/// <exception cref="AiChatClientUnavailableException">
	///     No provider is configured/approved, or the provider could not be reached.
	/// </exception>
	Task<string> Complete(string model, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken);
}

/// <summary>
///     A chat completion could not be performed: no provider configured/approved, or
///     the provider refused or failed.
/// </summary>
/// <remarks>
///     The message is safe to log. It never contains the credential, the prompt, or
///     any report content — an exception is not a place to put content.
/// </remarks>
public sealed class AiChatClientUnavailableException : Exception
{
	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, operator-facing explanation.</param>
	public AiChatClientUnavailableException(string message)
		: base(message)
	{
	}

	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, operator-facing explanation.</param>
	/// <param name="innerException">The underlying failure. Never surfaced beyond this message.</param>
	public AiChatClientUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>Creates the exception.</summary>
	public AiChatClientUnavailableException()
		: base("The AI chat client is unavailable.")
	{
	}
}
