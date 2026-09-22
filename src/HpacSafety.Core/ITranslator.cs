namespace HpacSafety.Core;

/// <summary>
///     Machine translation of short authored text between the two official
///     locales.
/// </summary>
/// <remarks>
///     <para>
///         Two callers. An administrator authoring a question presses Translate to
///         fill the other language, a drafting aid whose result is editable and saved
///         deliberately (ADR-0062). The Worker calls it mechanically, off the
///         submission path, to fill an answer's second language — including a
///         narrative one — before an administrator ever needs to (ADR-0080). Either
///         way the result is a literal machine translation, not a paraphrase, and an
///         administrator can always overwrite what the Worker supplied.
///     </para>
///     <para>
///         It is never called on the submission path itself, and it never produces
///         the bilingual summary — that comes from the Worker's single anonymized
///         model call, not from here.
///     </para>
///     <para>
///         The port exists because the provider is expected to change: DeepL today,
///         something else later, without a change reaching any caller
///         (ADR-0033, ADR-0022).
///     </para>
/// </remarks>
public interface ITranslator
{
	/// <summary>
	///     Whether a provider is configured. False when no credential is present,
	///     which is the ordinary state of a local checkout — the caller reports
	///     that translation is unavailable rather than failing on use.
	/// </summary>
	bool IsConfigured { get; }

	/// <summary>
	///     Translates each string from one official locale into the other,
	///     returning results in the order they were given.
	/// </summary>
	/// <param name="texts">
	///     The strings to translate. Several are accepted so that one Translate
	///     press is one request rather than one per field.
	/// </param>
	/// <param name="source">The locale the text is written in.</param>
	/// <param name="target">The locale to translate into.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>One translation per input, positionally.</returns>
	/// <exception cref="TranslationUnavailableException">
	///     No provider is configured, or the provider could not be reached.
	/// </exception>
	Task<IReadOnlyList<string>> TranslateAsync(
		IReadOnlyList<string> texts, Locale source, Locale target, CancellationToken cancellationToken);
}

/// <summary>
///     Translation could not be performed: no credential, or the provider refused
///     or failed.
/// </summary>
/// <remarks>
///     The message is safe to show an administrator. It never contains the
///     credential, and never the text that was being translated — an exception is
///     not a place to put content.
/// </remarks>
public sealed class TranslationUnavailableException : Exception
{
	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, administrator-facing explanation.</param>
	public TranslationUnavailableException(string message)
		: base(message)
	{
	}

	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, administrator-facing explanation.</param>
	/// <param name="innerException">
	///     The underlying failure. Never surfaced to a caller — the API reports
	///     <see cref="Exception.Message" /> only.
	/// </param>
	public TranslationUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>Creates the exception.</summary>
	public TranslationUnavailableException()
		: base("Translation is unavailable.")
	{
	}
}
