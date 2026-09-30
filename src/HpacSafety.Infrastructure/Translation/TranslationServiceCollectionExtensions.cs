using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>Registers the translation provider.</summary>
public static class TranslationServiceCollectionExtensions
{
	/// <summary>
	///     Adds <see cref="ITranslator" />, backed by Gemini through the <c>AiChatClient</c>
	///     strategy (ADR-0179).
	/// </summary>
	/// <remarks>
	///     A translator is always registered, so the endpoint, the authoring
	///     screen, and the Worker take one path in every environment. With no key it
	///     reports itself unconfigured and refuses to translate, in Development as
	///     everywhere else: there is no stand-in, because one that returns its input
	///     unchanged gets stored as a translation (ADR-0109). The key, and the
	///     provider that holds it, are the summary call's (<c>AiChatClient</c>,
	///     ADR-0104); only the model and reasoning level are the translator's own
	///     (<c>Translation</c>).
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	public static IServiceCollection AddHpacSafetyTranslation(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Checked at startup, key or no key: a blank model would otherwise
		// surface only as failed translations (REQ-WLD-040).
		services.AddSingleton<IValidateOptions<TranslationOptions>, TranslationOptionsValidator>();
		services.AddOptions<TranslationOptions>()
			.Bind(configuration.GetSection(TranslationOptions.SectionName))
			.ValidateOnStart();

		services.AddHpacSafetyAiChatProvider(configuration);
		services.AddScoped<ITranslator, AiChatTranslator>();

		return services;
	}
}
