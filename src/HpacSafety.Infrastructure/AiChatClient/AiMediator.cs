using HpacSafety.Core;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     The registered <see cref="IAiMediator" />: hands each request to the
///     <see cref="IAiHandler" /> whose model-name prefix matches the request's model, and
///     refuses it when no key is held or no handler claims the model (ADR-0104).
/// </summary>
/// <remarks>
///     The summary and the translator each ask for their own model, so each may be served by
///     a different handler over the same key. A model no handler claims is refused at startup
///     by the validators while a key is held; this refusal is the backstop, and carries no
///     model text.
/// </remarks>
public sealed class AiMediator(IEnumerable<IAiHandler> handlers,
							   IOptions<AiChatClientOptions> options) : IAiMediator
{
	private readonly IReadOnlyList<IAiHandler> _handlers = [.. handlers];

	/// <inheritdoc />
	public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

	/// <inheritdoc />
	public Task<string> Complete(AiChatRequest request,
								 CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!IsConfigured)
		{
			throw new AiMediatorUnavailableException("No AI chat provider is configured and approved for use.");
		}

		var handler = HandlerFor(_handlers, request.Model)
					  ?? throw new AiMediatorUnavailableException("No AI handler claims the requested model.");

		return handler.Complete(request, cancellationToken);
	}

	/// <summary>The handler that claims this model name, or <see langword="null" /> when none does.</summary>
	internal static IAiHandler? HandlerFor(IEnumerable<IAiHandler> handlers,
										   string? model)
	{
		if (string.IsNullOrWhiteSpace(model))
		{
			return null;
		}

		return handlers.FirstOrDefault(handler =>
			model.Trim().StartsWith(handler.ModelPrefix, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>A startup failure message for a model no handler claims, naming the setting.</summary>
	internal static string Unclaimed(IEnumerable<IAiHandler> handlers,
									 string setting)
	{
		return $"{setting} must name a model a provider handler claims "
			   + $"(a name starting with {string.Join(", ", handlers.Select(handler => $"'{handler.ModelPrefix}'"))}).";
	}
}
