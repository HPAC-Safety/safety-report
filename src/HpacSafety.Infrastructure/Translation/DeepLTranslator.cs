using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HpacSafety.Core;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     DeepL, the translation provider decided in ADR-0022 and already used by the
///     CI locale workflow.
/// </summary>
/// <remarks>
///     <para>
///         This deliberately mirrors <c>tools/translator.mjs</c>: the same provider,
///         the same request shape, the same placeholder protection, and the same
///         formality default. Two translators that disagree about how to ask would
///         produce UI chrome and question text in noticeably different French.
///     </para>
///     <para>
///         It is the only type in the repository that names DeepL on the server side.
///         Everything above it depends on <see cref="ITranslator" /> (ADR-0033).
///     </para>
/// </remarks>
public sealed partial class DeepLTranslator : ITranslator
{
	/// <summary>The named <see cref="HttpClient" /> this resolves.</summary>
	public const string HttpClientName = "deepl";

	private const string FreeTierHost = "https://api-free.deepl.com/v2/translate";
	private const string ProTierHost = "https://api.deepl.com/v2/translate";

	/// <summary>
	///     DeepL's own language codes. <c>FR-CA</c> is a <b>target-only</b>
	///     variant, so it appears here as a target and French is asked for as
	///     plain <c>FR</c> when it is the source. See the remarks on
	///     <see cref="SourceCodeFor" />.
	/// </summary>
	private static readonly Dictionary<string, string> TargetCodes = new(StringComparer.Ordinal)
	{
		["en-CA"] = "EN-CA",
		["fr-CA"] = "FR-CA"
	};

	private static readonly Dictionary<string, string> SourceCodes = new(StringComparer.Ordinal)
	{
		["en-CA"] = "EN",
		["fr-CA"] = "FR"
	};

	private readonly IHttpClientFactory _clients;
	private readonly DeepLOptions _options;

	/// <summary>Creates the translator.</summary>
	/// <param name="clients">Supplies the named HTTP client.</param>
	/// <param name="options">Provider configuration.</param>
	public DeepLTranslator(IHttpClientFactory clients, IOptions<DeepLOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_clients = clients;
		_options = options.Value;
	}

	/// <inheritdoc />
	public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> TranslateAsync(
		IReadOnlyList<string> texts, Locale source, Locale target, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(texts);

		if (!IsConfigured)
		{
			throw new TranslationUnavailableException(
				"Translation is not configured on this server.");
		}

		if (source == target)
		{
			throw new TranslationUnavailableException(
				"A translation needs two different languages.");
		}

		if (texts.Count == 0)
		{
			return [];
		}

		var request = new DeepLRequest(
			[.. texts.Select(ProtectPlaceholders)],
			SourceCodeFor(source),
			TargetCodeFor(target),
			_options.Formality,
			true,
			"xml",
			["ph"]);

		DeepLResponse? payload;

		try
		{
			using var client = _clients.CreateClient(HttpClientName);
			client.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("DeepL-Auth-Key", _options.ApiKey);

			using var response = await client
				.PostAsJsonAsync(ResolvedEndpoint(), request, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				// The status only. A DeepL error body can echo the submitted
				// text back, and that text is question wording an administrator
				// is drafting — it does not belong in an exception or a log.
				throw new TranslationUnavailableException(
					$"The translation service answered {(int)response.StatusCode}.");
			}

			payload = await response.Content
				.ReadFromJsonAsync<DeepLResponse>(cancellationToken)
				.ConfigureAwait(false);
		}
		catch (HttpRequestException cause)
		{
			throw new TranslationUnavailableException("The translation service could not be reached.", cause);
		}
		catch (JsonException cause)
		{
			throw new TranslationUnavailableException("The translation service returned something unreadable.", cause);
		}

		var translations = payload?.Translations;

		// DeepL answers positionally, with no keys, so a length mismatch would
		// silently shift every field by one — French help text landing in the
		// label. Refusing is the only safe answer.
		if (translations is null || translations.Count != texts.Count)
		{
			throw new TranslationUnavailableException(
				"The translation service returned a different number of results than were sent.");
		}

		return [.. translations.Select(translation => UnprotectPlaceholders(translation.Text))];
	}

	/// <summary>
	///     The host derived from the key, unless one is configured. DeepL marks
	///     Free-tier keys with a <c>:fx</c> suffix and serves the two tiers from
	///     different hosts.
	/// </summary>
	private string ResolvedEndpoint()
	{
		return string.IsNullOrWhiteSpace(_options.Endpoint)
			? _options.ApiKey!.EndsWith(":fx", StringComparison.Ordinal) ? FreeTierHost : ProTierHost
			: _options.Endpoint;
	}

	/// <summary>
	///     DeepL's code for a source language.
	/// </summary>
	/// <remarks>
	///     <c>FR-CA</c> is documented as a target-only variant — it cannot be a
	///     source. The CI translator never met this because English is always its
	///     source; here an administrator may write the French first and translate
	///     back, so French is asked for as plain <c>FR</c> when it is the source.
	/// </remarks>
	private static string SourceCodeFor(Locale locale)
	{
		return SourceCodes.TryGetValue(locale.Code, out var code)
			? code
			: throw new TranslationUnavailableException($"No translation language is configured for '{locale.Code}'.");
	}

	private static string TargetCodeFor(Locale locale)
	{
		return TargetCodes.TryGetValue(locale.Code, out var code)
			? code
			: throw new TranslationUnavailableException($"No translation language is configured for '{locale.Code}'.");
	}

	/// <summary>
	///     Wraps each <c>{placeholder}</c> in a tag DeepL is told to leave alone,
	///     so "Showing {count} reports" cannot come back as "Showing {compte}
	///     reports". <c>tag_handling: xml</c> makes DeepL parse the string as
	///     markup, so literal angle brackets and ampersands are escaped first.
	/// </summary>
	private static string ProtectPlaceholders(string text)
	{
		return PlaceholderPattern().Replace(
			text
				.Replace("&", "&amp;", StringComparison.Ordinal)
				.Replace("<", "&lt;", StringComparison.Ordinal)
				.Replace(">", "&gt;", StringComparison.Ordinal),
			match => $"<ph>{match.Value}</ph>");
	}

	/// <summary>Reverses <see cref="ProtectPlaceholders" />.</summary>
	private static string UnprotectPlaceholders(string text)
	{
		return ProtectedPattern()
			.Replace(text, match => match.Groups[1].Value)
			.Replace("&lt;", "<", StringComparison.Ordinal)
			.Replace("&gt;", ">", StringComparison.Ordinal)
			.Replace("&amp;", "&", StringComparison.Ordinal);
	}

	[GeneratedRegex(@"\{[^{}]*\}")]
	private static partial Regex PlaceholderPattern();

	[GeneratedRegex(@"<ph>([^<]*)</ph>")]
	private static partial Regex ProtectedPattern();

	private sealed record DeepLRequest(
		[property: JsonPropertyName("text")] IReadOnlyList<string> Text,
		[property: JsonPropertyName("source_lang")]
		string SourceLang,
		[property: JsonPropertyName("target_lang")]
		string TargetLang,
		[property: JsonPropertyName("formality")]
		string Formality,
		[property: JsonPropertyName("preserve_formatting")]
		bool PreserveFormatting,
		[property: JsonPropertyName("tag_handling")]
		string TagHandling,
		[property: JsonPropertyName("ignore_tags")]
		IReadOnlyList<string> IgnoreTags);

	private sealed record DeepLResponse(
		[property: JsonPropertyName("translations")]
		IReadOnlyList<DeepLTranslation> Translations);

	private sealed record DeepLTranslation(
		[property: JsonPropertyName("text")] string Text);
}
