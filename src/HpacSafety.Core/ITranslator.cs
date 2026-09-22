namespace HpacSafety.Core;

/// <summary>
///     Machine translation of short authored text between the two official
///     locales.
/// </summary>
/// <remarks>
///     <para>
///         This exists for <b>one</b> caller: an administrator authoring a question who
///         presses Translate to fill the other language. It is a drafting aid. The
///         result is editable, the administrator saves it deliberately, and the stored
///         revision is theirs — see ADR-0062.
///     </para>
///     <para>
///         It is deliberately not used for anything else. A reporter's narrative is
///         never translated (a translated account of a crash is a paraphrased account
///         of a crash), and the bilingual summary comes from the Worker's single model
///         call, not from here.
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
