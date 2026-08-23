using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>One exact asked question and its nullable rendered answer.</summary>
public sealed record ReportQuestionAnswerDto(
    TinyId QuestionId,
    string QuestionKey,
    string Label,
    bool IsPrivate,
    string? Answer);

/// <summary>The read DTO returned by the Worker database query.</summary>
public sealed record ReportForSummaryDto(
    TinyId ReportId,
    Locale Language,
    IReadOnlyList<ReportQuestionAnswerDto> Questions);

/// <summary>One answered labeled field sent to the model.</summary>
public sealed record SummarizationField(string QuestionKey, string Label, string Value);

/// <summary>The only request shape accepted by the summarizer.</summary>
public sealed class SummarizationInput
{
    private SummarizationInput(
        TinyId reportId,
        Locale language,
        IReadOnlyList<SummarizationField> reportContent,
        IReadOnlyList<SummarizationField> privateContext)
    {
        ReportId = reportId;
        Language = language;
        ReportContent = reportContent;
        PrivateContext = privateContext;
    }

    /// <summary>Report id for correlation only.</summary>
    public TinyId ReportId { get; }

    /// <summary>Requested output language.</summary>
    public Locale Language { get; }

    /// <summary>Non-private answered fields and the only eligible facts.</summary>
    public IReadOnlyList<SummarizationField> ReportContent { get; }

    /// <summary>Private answered fields used only as recognition hints.</summary>
    public IReadOnlyList<SummarizationField> PrivateContext { get; }

    /// <summary>Partitions one Worker query DTO and omits skipped answers.</summary>
    public static SummarizationInput From(ReportForSummaryDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.Questions);

        var reportContent = new List<SummarizationField>();
        var privateContext = new List<SummarizationField>();

        foreach (var question in report.Questions)
        {
            ArgumentNullException.ThrowIfNull(question);

            if (string.IsNullOrWhiteSpace(question.Answer))
            {
                continue;
            }

            var field = new SummarizationField(question.QuestionKey, question.Label, question.Answer);
            (question.IsPrivate ? privateContext : reportContent).Add(field);
        }

        return new SummarizationInput(
            report.ReportId,
            report.Language,
            reportContent.AsReadOnly(),
            privateContext.AsReadOnly());
    }
}
