using HpacSafety.Core;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>Registers the AI chat client the Worker's summarizer calls.</summary>
public static class AiChatClientServiceCollectionExtensions
{
	/// <summary>
	///     Adds <see cref="IAiChatClient" />. Registers <see cref="UnconfiguredAiChatClient" />
	///     until a provider concretion is reviewed and wired in for a given environment —
	///     see the follow-on issue to ADR-0082 for the first, Google Gemini.
	/// </summary>
	public static IServiceCollection AddHpacSafetyAiChatClient(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddScoped<IAiChatClient, UnconfiguredAiChatClient>();

		return services;
	}
}
