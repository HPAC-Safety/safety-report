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
	///     A translator is always registered, so the endpoint and the authoring
	///     screen take one path in every environment. Which adapter it gets
	///     depends on whether a credential is configured, and on whether a
	///     development stand-in is allowed.
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	/// <param name="useStandInWhenUnconfigured">
	///     True only in Development. When no credential is present the container
	///     then gets <see cref="EchoTranslator" />, so the Translate control works
	///     locally and exercises the same endpoint and the same port as
	///     production. Outside Development this is false and an unconfigured
	///     server reports translation unavailable — copying English into the
	///     French column of a live question bank would put untranslated English in
	///     front of French-speaking pilots. See ADR-0062.
	/// </param>
	public static IServiceCollection AddHpacSafetyTranslation(
		this IServiceCollection services,
		IConfiguration configuration,
		bool useStandInWhenUnconfigured = false)
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

		var configured = !string.IsNullOrWhiteSpace(
			configuration[$"{DeepLOptions.SectionName}:ApiKey"] ?? configuration["DEEPL_API_KEY"]);

		if (useStandInWhenUnconfigured && !configured)
		{
			services.AddScoped<ITranslator, EchoTranslator>();
		}
		else
		{
			services.AddScoped<ITranslator, DeepLTranslator>();
		}

		return services;
	}
}
