
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>What a model returned, with the provenance stamped alongside it so
/// any published text traces back to exactly what produced it.</summary>
/// <param name="Text">The summary.</param>
/// <param name="Model">The model identifier.</param>
/// <param name="PromptVersion">The versioned prompt from <c>prompts/</c>.</param>
public sealed record SummaryDraft(string Text, string Model, string PromptVersion);

/// <summary>
/// Produces an anonymized summary in the language the reporter wrote in.
/// </summary>
/// <remarks>
/// The model receives non-private report content and private redaction context
/// as distinct sections. Private context may identify information to omit or
/// generalize, but it is never an eligible source of summary facts.
/// </remarks>
public interface ISummarizer
{
    /// <summary>Summarizes partitioned report fields.</summary>
    Task<SummaryDraft> SummarizeAsync(SummarizationInput input, Locale locale, CancellationToken cancellationToken);
}
