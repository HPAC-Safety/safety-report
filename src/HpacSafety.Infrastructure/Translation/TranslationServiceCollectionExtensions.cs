using HpacSafety.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>Registers the translation provider.</summary>
public static class TranslationServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="ITranslator"/>, backed by DeepL.
    /// </summary>
    /// <remarks>
    /// Registered whether or not a credential is present. An unconfigured
    /// translator answers <see cref="ITranslator.IsConfigured"/> with false and
    /// the endpoint reports that translation is unavailable — a local checkout
    /// without the key still runs, and the authoring screen still works with
    /// the button disabled.
    /// </remarks>
    /// <param name="services">The container.</param>
    /// <param name="configuration">Application configuration.</param>
    public static IServiceCollection AddHpacSafetyTranslation(
        this IServiceCollection services, IConfiguration configuration)
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
