using HpacSafety.Core;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     One provider's way of answering a chat-completion request, claimed by the prefix of
///     the model names it serves. The <see cref="AiMediator" /> picks a handler by that
///     prefix and is the only thing that does (ADR-0104).
/// </summary>
/// <remarks>
///     A new OpenAI-compatible provider is one class implementing this and one
///     <c>services.AddSingleton&lt;IAiHandler&gt;(...)</c> registration in
///     <see cref="AiChatClientServiceCollectionExtensions" />. A handler reads the options
///     when a call is made, not at construction, so the validators can ask which models it
///     claims while the options are still being bound.
/// </remarks>
public interface IAiHandler
{
	/// <summary>The model-name prefix this handler claims, such as <c>gemini-</c>.</summary>
	string ModelPrefix { get; }

	/// <summary>Requests one completion from this handler's provider.</summary>
	/// <param name="request">The model, reasoning level, conversation, and reply format.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The provider's response text, unparsed and unvalidated.</returns>
	/// <exception cref="AiMediatorUnavailableException">The provider refused, failed, or could not be reached.</exception>
	Task<string> Complete(AiChatRequest request,
						  CancellationToken cancellationToken);
}
