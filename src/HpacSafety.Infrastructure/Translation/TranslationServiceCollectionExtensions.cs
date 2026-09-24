using HpacSafety.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>Registers the translation provider.</summary>
public static class TranslationServiceCollectionExtensions
{
	/// <summary>
	///     Adds <see cref="ITranslator" />, backed by DeepL.
	/// </summary>
	/// <remarks>
	///     A translator is always registered, so the endpoint, the authoring
	///     screen, and the Worker take one path in every environment. With no
	///     credential it reports itself unconfigured and refuses to translate, in
	///     Development as everywhere else: there is no stand-in, because one that
	///     returns its input unchanged gets stored as a translation (ADR-0109).
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	public static IServiceCollection AddHpacSafetyTranslation(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.Configure<DeepLOptions>(options =>
		{
			configuration.GetSection(DeepLOptions.SectionName).Bind(options);

			// `DEEPL_API_KEY` is the name the credential already has — in
			// repository settings, in the deploy workflow, and in
			// tools/translator.mjs. Accepting it directly means a developer who
			// exports the same variable the CI tooling uses gets a working
			// Translate button without learning a second name.
			options.ApiKey ??= configuration["DEEPL_API_KEY"];
		});

		services.AddHttpClient(DeepLTranslator.HttpClientName);
		services.AddScoped<ITranslator, DeepLTranslator>();

		return services;
	}
}
