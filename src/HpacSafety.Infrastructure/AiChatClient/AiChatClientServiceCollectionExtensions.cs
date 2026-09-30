using HpacSafety.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>Registers the AI chat client the Worker's summarizer calls.</summary>
public static class AiChatClientServiceCollectionExtensions
{
	/// <summary>
	///     Each provider strategy, by the name <c>AiChatClient:Provider</c> gives it. A new
	///     provider is one <see cref="IAiChatClient" /> class and one entry here (ADR-0104).
	/// </summary>
	private static readonly Dictionary<string, Action<IServiceCollection>> Strategies =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["Gemini"] = services => services.AddScoped<IAiChatClient, GeminiChatClient>(),
		};

	/// <summary>The provider names a configuration may use.</summary>
	internal static IEnumerable<string> KnownProviders => Strategies.Keys;

	/// <summary>
	///     Adds <see cref="IAiChatClient" />, the strategy <c>AiChatClient:Provider</c> names.
	/// </summary>
	/// <remarks>
	///     A client is always registered, so the summarizer takes one path in every
	///     environment. With a key and a known provider it is that provider's strategy;
	///     with no key it is <see cref="UnconfiguredAiChatClient" />, and every
	///     summarization attempt fails closed rather than sending report content
	///     anywhere. There is no Development stand-in — a summarization attempt has
	///     nothing useful to fall back to. A key with an unusable configuration stops
	///     the host at startup (<see cref="AiChatProviderOptionsValidator" /> and <see cref="AiChatClientOptionsValidator" />).
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	public static IServiceCollection AddHpacSafetyAiChatClient(this IServiceCollection services,
															   IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddSingleton<IValidateOptions<AiChatClientOptions>, AiChatClientOptionsValidator>();
		services.AddHpacSafetyAiChatProvider(configuration);

		return services;
	}

	/// <summary>
	///     The provider half of <see cref="AddHpacSafetyAiChatClient" />: the options, the
	///     strategy the configured provider names, and the check that the provider is one
	///     this host knows. Translation (ADR-0179) registers this alone, because the API
	///     shares the Worker's key and provider but has no summary model to validate.
	///     Idempotent, so a host that registers both gets one client.
	/// </summary>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	internal static IServiceCollection AddHpacSafetyAiChatProvider(this IServiceCollection services,
																   IConfiguration configuration)
	{
		if (services.Any(descriptor => descriptor.ServiceType == typeof(IAiChatClient)))
		{
			return services;
		}

		var section = configuration.GetSection(AiChatClientOptions.SectionName);

		services.AddSingleton<IValidateOptions<AiChatClientOptions>, AiChatProviderOptionsValidator>();
		services.AddOptions<AiChatClientOptions>()
			.Bind(section)
			.ValidateOnStart();

		services.AddHttpClient(GeminiChatClient.HttpClientName);

		var provider = section[nameof(AiChatClientOptions.Provider)];

		if (!string.IsNullOrWhiteSpace(section[nameof(AiChatClientOptions.ApiKey)])
			&& provider is not null
			&& Strategies.TryGetValue(provider, out var register))
		{
			register(services);
		}
		else
		{
			services.AddScoped<IAiChatClient, UnconfiguredAiChatClient>();
		}

		return services;
	}

	/// <summary>Whether a strategy is registered under this provider name.</summary>
	internal static bool IsKnownProvider(string? provider)
	{
		return provider is not null && Strategies.ContainsKey(provider);
	}
}
