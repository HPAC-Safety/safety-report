namespace HpacSafety.Core.Features.Reporting;

/// <summary>One anonymized summary with model provenance.</summary>
public sealed record SummaryDraft(string Text, string Model, string PromptVersion);

/// <summary>Produces one anonymized summary from the partitioned Worker DTO.</summary>
public interface ISummarizer
{
    /// <summary>Runs the one summary model call.</summary>
    Task<SummaryDraft> SummarizeAsync(
        SummarizationInput input,
        CancellationToken cancellationToken);
}
