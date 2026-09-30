namespace HpacSafety.Core;

/// <summary>The role a chat message was authored under, per the OpenAI chat-completions spec.</summary>
public enum ChatRole
{
	System,
	User,
}

/// <summary>One message in a chat-completion request.</summary>
public sealed record ChatMessage(ChatRole Role, string Content);

/// <summary>
///     How much the model may think before it answers. Each handler maps
///     this to its own setting; a provider that has no such setting ignores it.
/// </summary>
public enum ReasoningEffort
{
	Low,
	Medium,
	High,
}

/// <summary>The shape of reply a request asks the model for.</summary>
public enum AiResponseFormat
{
	/// <summary>Free text.</summary>
	Text,

	/// <summary>One JSON object.</summary>
	JsonObject,
}

/// <summary>One provider-neutral, OpenAI-style chat-completion request.</summary>
/// <param name="Model">The provider-specific model identifier to use; its name picks the provider.</param>
/// <param name="ReasoningEffort">How much the model may think before it answers, or <see langword="null" /> for the provider's default.</param>
/// <param name="Messages">The conversation, in order.</param>
/// <param name="ResponseFormat">The shape of reply asked for; a JSON object unless a caller says otherwise.</param>
public sealed record AiChatRequest(string Model,
								   ReasoningEffort? ReasoningEffort,
								   IReadOnlyList<ChatMessage> Messages,
								   AiResponseFormat ResponseFormat = AiResponseFormat.JsonObject);

/// <summary>
///     The mediator between the callers that want a model's answer and the provider
///     handlers that know how to ask for it: a single-turn, OpenAI-style chat-completion
///     call (ADR-0104).
/// </summary>
/// <remarks>
///     The mediator is the only place that tells providers apart. It picks the handler
///     whose model-name prefix matches the request's model, so a caller names a model and
///     never a provider. It has two callers, each owning its prompt, its request content,
///     and strict validation of the response: <c>OpenAiSummarizer</c> in the Worker
///     (the summary, see <c>ISummarizer</c>) and <c>OpenAiTranslator</c> (see
///     <c>ITranslator</c>, ADR-0179), a separate call that is sent only the strings to
///     translate. The mediator only carries a request to a provider and returns whatever
///     text it answered with; it leaves the sampling temperature at the provider's default.
/// </remarks>
public interface IAiMediator
{
	/// <summary>
	///     Whether a provider key is held. False when no credential is present — the
	///     caller fails closed rather than sending report content anywhere.
	/// </summary>
	bool IsConfigured { get; }

	/// <summary>Requests one completion for the given request from the handler its model names.</summary>
	/// <param name="request">The model, reasoning level, conversation, and reply format.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The provider's response text, unparsed and unvalidated.</returns>
	/// <exception cref="AiMediatorUnavailableException">
	///     No key is configured, no handler claims the model, or the provider could not be reached.
	/// </exception>
	Task<string> Complete(AiChatRequest request,
						  CancellationToken cancellationToken);
}

/// <summary>
///     A chat completion could not be performed: no key configured, no handler for the
///     model, or the provider refused or failed.
/// </summary>
/// <remarks>
///     The message is safe to log. It never contains the credential, the prompt, or
///     any report content — an exception is not a place to put content.
/// </remarks>
public sealed class AiMediatorUnavailableException : Exception
{
	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, operator-facing explanation.</param>
	public AiMediatorUnavailableException(string message)
		: base(message)
	{
	}

	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, operator-facing explanation.</param>
	/// <param name="innerException">The underlying failure. Never surfaced beyond this message.</param>
	public AiMediatorUnavailableException(string message,
											Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>Creates the exception.</summary>
	public AiMediatorUnavailableException()
		: base("The AI mediator is unavailable.")
	{
	}
}
