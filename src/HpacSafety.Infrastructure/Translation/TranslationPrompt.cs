using System.Text.Json;
using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     The one current translation prompt and the term list, both read from
///     <c>locales/</c>, where <c>tools/i18n/translator.mjs</c> reads the very same files
///     (ADR-0179). They are embedded into this assembly at build time, so a
///     deployed API or Worker carries exactly the bytes CI translated with.
/// </summary>
/// <remarks>
///     A behavior change is a new version file (<c>v2</c>) and a new
///     <see cref="CurrentFileName" />; a version that has been used is never edited.
///     The rendering below must stay in step with <c>renderPrompt</c> and
///     <c>termInstructions</c> in the JavaScript tools: same slots, same
///     sentence, same "none" line.
/// </remarks>
public static class TranslationPrompt
{
	/// <summary>The current prompt file under <c>locales/</c>. Bump on any behavior change.</summary>
	public const string CurrentFileName = "translation-prompt.v2.md";

	/// <summary>The term list file under <c>locales/</c>.</summary>
	public const string TermsFileName = "terms.json";

	private const string SourceSlot = "[[source_language]]";
	private const string TargetSlot = "[[target_language]]";
	private const string TermsSlot = "[[terms]]";

	private static readonly Lazy<string> Template = new(() => ReadResource(CurrentFileName));
	private static readonly Lazy<IReadOnlyList<string>> Instructions = new(() => TermInstructions(ReadResource(TermsFileName)));

	/// <summary>What the prompt says each official locale means.</summary>
	private static readonly Dictionary<string, string> Languages = new(StringComparer.Ordinal)
	{
		["en-CA"] = "Canadian English (en-CA), with Canadian spelling and usage: colour, centre, metre, licence for the noun",
		["fr-CA"] = "Canadian French (fr-CA), following Canadian usage and addressing the reader formally with \"vous\"",
	};

	/// <summary>The system prompt for one translation direction, with the term list in it.</summary>
	/// <exception cref="TranslationUnavailableException">A locale has no configured language.</exception>
	public static string Render(Locale source,
								Locale target)
	{
		return Render(Template.Value, source, target, Instructions.Value);
	}

	/// <summary>Fills the template's slots. Separate so a test can supply its own terms.</summary>
	public static string Render(string template,
								Locale source,
								Locale target,
								IReadOnlyList<string> instructions)
	{
		ArgumentNullException.ThrowIfNull(template);
		ArgumentNullException.ThrowIfNull(instructions);

		var terms = instructions.Count > 0
			? string.Join('\n', instructions.Select(line => $"- {line}"))
			: "- (none)";

		return template
			.Replace(SourceSlot, LanguageFor(source), StringComparison.Ordinal)
			.Replace(TargetSlot, LanguageFor(target), StringComparison.Ordinal)
			.Replace(TermsSlot, terms, StringComparison.Ordinal);
	}

	/// <summary>
	///     One sentence per term, built from a <c>terms.json</c> document: what the
	///     English term must become in French, and the French it must never become.
	/// </summary>
	/// <exception cref="InvalidOperationException">An entry has no French or no forbidden forms.</exception>
	public static IReadOnlyList<string> TermInstructions(string termsJson)
	{
		using var document = JsonDocument.Parse(termsJson);
		var sentences = new List<string>();

		foreach (var entry in document.RootElement.EnumerateObject())
		{
			// Keys beginning "_" are file-level commentary, as in the glossary.
			if (entry.Name.StartsWith('_'))
			{
				continue;
			}

			var french = entry.Value.TryGetProperty("fr-CA", out var rendering) ? rendering.GetString() : null;
			var forbidden = entry.Value.TryGetProperty("forbidden", out var forms) && forms.ValueKind == JsonValueKind.Array
				? forms.EnumerateArray().Select(form => form.GetString()).ToList()
				: [];

			if (string.IsNullOrEmpty(french))
			{
				throw new InvalidOperationException($"terms.json entry '{entry.Name}' has no fr-CA rendering.");
			}

			if (forbidden.Count == 0 || forbidden.Any(string.IsNullOrEmpty))
			{
				throw new InvalidOperationException(
					$"terms.json entry '{entry.Name}' needs a non-empty \"forbidden\" list of French forms.");
			}

			var never = string.Join(" or ", forbidden.Select(form => $"\"{form}…\""));
			sentences.Add(
				$"\"{entry.Name}\" (English, in any form) is \"{french}\" in French, conjugated or as a noun to fit. "
				+ $"Never {never} in French.");
		}

		return sentences;
	}

	private static string LanguageFor(Locale locale)
	{
		return Languages.TryGetValue(locale.Code, out var language)
			? language
			: throw new TranslationUnavailableException($"No translation language is configured for '{locale.Code}'.");
	}

	private static string ReadResource(string fileName)
	{
		var name = $"Translation/{fileName}";
		using var stream = typeof(TranslationPrompt).Assembly.GetManifestResourceStream(name)
			?? throw new InvalidOperationException($"The embedded translation resource '{name}' is missing.");
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
