namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     Configuration for <see cref="DeepLTranslator" />, bound from the
///     <c>Translation</c> configuration section.
/// </summary>
/// <remarks>
///     The key is the same DeepL credential the CI translation workflow uses
///     (ADR-0022). It reaches a running task as an environment variable supplied by
///     the deploy workflow; it is never sent to the browser, never logged, and
///     never included in a problem response.
/// </remarks>
public sealed class DeepLOptions
{
    /// <summary>The configuration section this binds from.</summary>
    public const string SectionName = "Translation";

    /// <summary>
    ///     The DeepL auth key. Absent in an ordinary local checkout, which is why
    ///     <see cref="DeepLTranslator.IsConfigured" /> exists rather than a startup
    ///     failure.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     Overrides the API host. Normally left unset: DeepL marks Free-tier keys
    ///     with a <c>:fx</c> suffix and the two tiers have different hosts, so the
    ///     host is derived from the key. Getting that wrong is a 403 that reads
    ///     like a bad credential.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    ///     DeepL formality. Defaults to <c>prefer_more</c>: a national safety
    ///     association addressing pilots uses "vous", and the <c>prefer_</c>
    ///     variants degrade to the default on a target language that does not
    ///     support formality rather than failing the request outright.
    /// </summary>
    public string Formality { get; set; } = "prefer_more";
}
