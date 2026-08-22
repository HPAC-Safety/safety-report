
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// An anonymized summary of a report, in one language. Every report ends up with
/// one row per official locale: the one generated from the report carries
/// <see cref="IsSource"/>, the other carries
/// <see cref="TranslatedFromSummaryId"/>.
/// </summary>
public class Summary
{
    private Summary(Guid reportId, Locale locale, string text, string model, string promptVersion, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        ReportId = reportId;
        Locale = locale;
        Text = text;
        Model = model;
        PromptVersion = promptVersion;
        CreatedAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The report summarized.</summary>
    public Guid ReportId { get; private init; }

    /// <summary>The language of this summary.</summary>
    public Locale Locale { get; private init; }

    /// <summary>The summary text. Publishable only once approved.</summary>
    public string Text { get; private set; }

    /// <summary>The model that produced it, stamped so published text traces back.</summary>
    public string Model { get; private init; }

    /// <summary>The prompt version that produced it.</summary>
    public string PromptVersion { get; private init; }

    /// <summary>True for the summary generated directly from the report.</summary>
    public bool IsSource { get; private init; }

    /// <summary>Set on the translated summary, pointing at the one it came from.</summary>
    public Guid? TranslatedFromSummaryId { get; private init; }

    /// <summary>The safety officer who approved this language.</summary>
    public Guid? ApprovedBy { get; private set; }

    /// <summary>When this language was approved.</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>When it was generated.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>True once a human has approved this language specifically.</summary>
    public bool IsApproved => ApprovedAt is not null;

    /// <summary>The summary generated from the report itself, in the language the reporter wrote in.</summary>
    public static Summary Generated(
        Guid reportId, Locale locale, string text, string model, string promptVersion, DateTimeOffset at) =>
        new(reportId, locale, text, model, promptVersion, at) { IsSource = true };

    /// <summary>The other language, translated from an already-anonymized summary.</summary>
    public static Summary TranslatedFrom(
        Summary source, Locale locale, string text, string model, string promptVersion, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new Summary(source.ReportId, locale, text, model, promptVersion, at)
        {
            IsSource = false,
            TranslatedFromSummaryId = source.Id,
        };
    }

    /// <summary>Replaces the text by hand — the escape hatch when the model failed.</summary>
    public void Rewrite(string text)
    {
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new DomainRuleViolationException("A summary cannot be blank.")
            : text;
        ApprovedBy = null;
        ApprovedAt = null;
    }

    /// <summary>Records a safety officer's approval of this language.</summary>
    public void Approve(Guid adminUserId, DateTimeOffset at)
    {
        ApprovedBy = adminUserId;
        ApprovedAt = at;
    }
}
