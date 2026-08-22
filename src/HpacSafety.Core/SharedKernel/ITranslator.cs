
namespace HpacSafety.Core.SharedKernel;

/// <summary>Translated text and the model that produced it.</summary>
/// <param name="Text">The translation.</param>
/// <param name="Model">The model identifier.</param>
public sealed record TranslationResult(string Text, string Model);

/// <summary>
/// Translates between the two official locales, in both directions.
/// </summary>
/// <remarks>
/// <para>Two callers, both of them safe:</para>
/// <list type="bullet">
/// <item><description>The summary pipeline, which translates an
/// <b>already-anonymized</b> summary. A raw report is never translated and never
/// leaves the system.</description></item>
/// <item><description>The question builder, which translates question wording an
/// administrator typed. Question text contains no reporter data.</description></item>
/// </list>
/// </remarks>
public interface ITranslator
{
    /// <summary>Translates one string between official locales.</summary>
    Task<TranslationResult> TranslateAsync(string text, Locale source, Locale target, CancellationToken cancellationToken);
}
