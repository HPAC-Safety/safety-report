using HpacSafety.Core.Values;

namespace HpacSafety.Core.Abstractions;

/// <summary>What a model returned, with the provenance stamped alongside it so
/// any published text traces back to exactly what produced it.</summary>
/// <param name="Text">The summary.</param>
/// <param name="Model">The model identifier.</param>
/// <param name="PromptVersion">The versioned prompt from <c>prompts/</c>.</param>
public sealed record SummaryDraft(string Text, string Model, string PromptVersion);

/// <summary>
/// Produces an anonymized summary of a report, in the language the reporter
/// wrote in.
/// </summary>
/// <remarks>
/// Implementations receive text that has <b>already</b> been through the
/// deterministic scrub. The raw report never reaches a model.
/// </remarks>
public interface ISummarizer
{
    /// <summary>Summarizes already-scrubbed narrative text.</summary>
    Task<SummaryDraft> SummarizeAsync(string scrubbedNarrative, Locale locale, CancellationToken cancellationToken);
}
