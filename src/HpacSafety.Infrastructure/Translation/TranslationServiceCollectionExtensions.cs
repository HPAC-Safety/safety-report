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
	///     Adds <see cref="ITranslator" />, backed by <see cref="OpenAiTranslator" />, which sends
	///     its own model to the <see cref="IAiMediator" />, whose handler for that model name
	///     (Gemini's, for <c>gemini-*</c>) does the call (ADR-0179).
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

		services.AddHpacSafetyAiMediator(configuration);
		services.AddScoped<ITranslator, OpenAiTranslator>();

		return services;
	}

	/// <summary>
	///     Adds <see cref="ITranslator" />, backed by DeepL. Kept, dormant: nothing
	///     calls it, because the OpenAI-compatible translator is registered (ADR-0179), but the adapter, its
	///     options, and their validation stay compiled and tested so the provider
	///     can be switched back by calling this in place of
	///     <see cref="AddHpacSafetyTranslation" />.
	/// </summary>
	/// <remarks>
	///     Never call both: each registers <see cref="ITranslator" />. The key is
	///     <c>Translation:ApiKey</c> or <c>DEEPL_API_KEY</c>; the English target is
	///     checked at startup (REQ-WLD-029).
	/// </remarks>
	/// <param name="services">The container.</param>
	/// <param name="configuration">Application configuration.</param>
	/// <returns>The same container, for chaining.</returns>
	public static IServiceCollection AddHpacSafetyDeepLTranslation(this IServiceCollection services,
																   IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<DeepLOptions>().Configure(options =>
		{
			configuration.GetSection(DeepLOptions.SectionName).Bind(options);

			// `DEEPL_API_KEY` is the name the credential already has — in
			// repository settings, in the deploy workflow, and in
			// tools/translator.mjs.
			options.ApiKey ??= configuration["DEEPL_API_KEY"];
		})
			// Checked at startup, key or no key: an unsupported English target is
			// a 400 on every French-to-English translation (REQ-WLD-029).
			.Validate(
				options => options.HasSupportedEnglishTarget,
				$"{DeepLOptions.SectionName}:{nameof(DeepLOptions.EnglishTarget)} must be one of "
				+ $"{string.Join(", ", DeepLOptions.SupportedEnglishTargets)}. DeepL has no Canadian English.")
			.ValidateOnStart();

		services.AddHttpClient(DeepLTranslator.HttpClientName);
		services.AddScoped<ITranslator, DeepLTranslator>();

		return services;
	}
}
