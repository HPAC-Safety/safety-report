using HpacSafety.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>Registers the AI chat client the Worker's summarizer calls.</summary>
public static class AiChatClientServiceCollectionExtensions
{
	/// <summary>
	///     Adds <see cref="IAiChatClient" />, backed by Gemini.
	/// </summary>
	/// <remarks>
	///     A client is always registered, so the summarizer takes one path in every
	///     environment. Which adapter it gets depends on whether a credential is
	///     configured: a real key gets <see cref="GeminiChatClient" />; no key
	///     reports the provider unavailable and the summarization attempt fails
	///     closed rather than silently sending report content anywhere. There is no
	///     Development stand-in — unlike question-authoring translation, a
	///     summarization attempt has nothing useful to fall back to, and failing
	///     closed is the correct behavior everywhere.
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	public static IServiceCollection AddHpacSafetyAiChatClient(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.Configure<GeminiOptions>(options =>
		{
			configuration.GetSection(GeminiOptions.SectionName).Bind(options);

			// GEMINI_API_KEY is the name the credential has in repository
			// secrets and in the deploy workflow — accepting it directly means
			// a developer who exports the same variable gets a working client
			// without learning a second name.
			options.ApiKey ??= configuration["GEMINI_API_KEY"];
		});

		services.AddHttpClient(GeminiChatClient.HttpClientName);

		var configured = !string.IsNullOrWhiteSpace(
			configuration[$"{GeminiOptions.SectionName}:ApiKey"] ?? configuration["GEMINI_API_KEY"]);

		if (configured)
		{
			services.AddScoped<IAiChatClient, GeminiChatClient>();
		}
		else
		{
			services.AddScoped<IAiChatClient, UnconfiguredAiChatClient>();
		}

		return services;
	}
}
