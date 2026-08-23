namespace HpacSafety.Core.Features.Reporting;

/// <summary>A labeled answer prepared for the summarization model.</summary>
public sealed record SummarizationField(string QuestionKey, string Label, string Value);

/// <summary>
/// An answer paired with the immutable privacy classification copied from its
/// question when the report was submitted.
/// </summary>
public sealed record ClassifiedReportField(SummarizationField Field, bool IsPrivate);

/// <summary>
/// The only input shape accepted by a summarizer. Report content supplies facts
/// for the summary; private context supplies redaction hints and must not be
/// restated as facts.
/// </summary>
public sealed class SummarizationInput
{
    private SummarizationInput(
        IReadOnlyList<SummarizationField> reportContent,
        IReadOnlyList<SummarizationField> privateContext)
    {
        ReportContent = reportContent;
        PrivateContext = privateContext;
    }

    /// <summary>Non-private fields eligible to contribute facts.</summary>
    public IReadOnlyList<SummarizationField> ReportContent { get; }

    /// <summary>Private values the model may use only to recognize and remove identifiers.</summary>
    public IReadOnlyList<SummarizationField> PrivateContext { get; }

    /// <summary>Partitions fields so callers cannot mix private values into report content.</summary>
    public static SummarizationInput Partition(IEnumerable<ClassifiedReportField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var reportContent = new List<SummarizationField>();
        var privateContext = new List<SummarizationField>();

        foreach (var classified in fields)
        {
            ArgumentNullException.ThrowIfNull(classified);
            ArgumentNullException.ThrowIfNull(classified.Field);
            (classified.IsPrivate ? privateContext : reportContent).Add(classified.Field);
        }

        return new SummarizationInput(reportContent.AsReadOnly(), privateContext.AsReadOnly());
    }
}
