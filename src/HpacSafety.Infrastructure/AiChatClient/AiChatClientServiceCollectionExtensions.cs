using HpacSafety.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>Registers the AI mediator and its provider handlers.</summary>
public static class AiChatClientServiceCollectionExtensions
{
	/// <summary>
	///     Adds <see cref="IAiMediator" /> with the summary's model validation: the Worker's
	///     registration (ADR-0104).
	/// </summary>
	/// <remarks>
	///     The mediator is always registered, so the summarizer takes one path in every
	///     environment. With no key it reports itself unconfigured and every summarization
	///     attempt fails closed rather than sending report content anywhere. There is no
	///     Development stand-in — a summarization attempt has nothing useful to fall back to.
	///     A key with an unusable configuration stops the host at startup
	///     (<see cref="AiChatClientOptionsValidator" />, and the translator's own).
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	public static IServiceCollection AddHpacSafetyAiSummarization(this IServiceCollection services,
																  IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddSingleton<IValidateOptions<AiChatClientOptions>, AiChatClientOptionsValidator>();
		services.AddHpacSafetyAiMediator(configuration);

		return services;
	}

	/// <summary>
	///     The mediator half of <see cref="AddHpacSafetyAiSummarization" />: the options, the
	///     handlers, and the mediator. Translation (ADR-0179) registers this alone, because the
	///     API shares the Worker's key but has no summary model to validate; it asks for its own
	///     model, which picks its own handler. Idempotent, so a host that registers both gets one
	///     mediator.
	/// </summary>
	/// <remarks>
	///     A new OpenAI-compatible provider is one <see cref="IAiHandler" /> class and one
	///     <c>AddSingleton&lt;IAiHandler&gt;(...)</c> registration below, built the same way.
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	internal static IServiceCollection AddHpacSafetyAiMediator(this IServiceCollection services,
															   IConfiguration configuration)
	{
		if (services.Any(descriptor => descriptor.ServiceType == typeof(IAiMediator)))
		{
			return services;
		}

		services.AddOptions<AiChatClientOptions>()
			.Bind(configuration.GetSection(AiChatClientOptions.SectionName))
			.ValidateOnStart();

		services.AddHttpClient(GeminiHandler.HttpClientName);
		services.AddSingleton<IAiHandler>(provider => new GeminiHandler(
			provider.GetRequiredService<IHttpClientFactory>(),
			() => provider.GetRequiredService<IOptions<AiChatClientOptions>>().Value));

		services.AddSingleton<IAiMediator, AiMediator>();

		return services;
	}
}
