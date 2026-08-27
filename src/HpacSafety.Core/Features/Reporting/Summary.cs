
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// The anonymized bilingual summary of a report. Exactly one row per report: the
/// Worker's single model call produces both languages together, so there is
/// nothing to keep in sync between them, and one shared approval covers the
/// pair. See product invariant #6 and <c>docs/data-and-persistence.md</c>.
/// </summary>
public class Summary
{
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private Summary()
    {
    }
#pragma warning restore CS8618

    private Summary(TinyId reportId, string aiSummaryEn, string aiSummaryFr, string model, string promptVersion, DateTimeOffset at)
    {
        Id = TinyId.New();
        ReportId = reportId;
        AiSummaryEn = NotBlank(aiSummaryEn);
        AiSummaryFr = NotBlank(aiSummaryFr);
        Model = model;
        PromptVersion = promptVersion;
        GeneratedAt = at;
        UpdatedAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The report summarized. Unique: exactly one summary per report.</summary>
    public TinyId ReportId { get; private init; }

    /// <summary>The English text. Publishable only once the pair is approved.</summary>
    public string AiSummaryEn { get; private set; }

    /// <summary>The French text. Publishable only once the pair is approved.</summary>
    public string AiSummaryFr { get; private set; }

    /// <summary>The model that produced it, stamped so published text traces back.</summary>
    public string Model { get; private init; }

    /// <summary>The prompt version that produced it.</summary>
    public string PromptVersion { get; private init; }

    /// <summary>The safety officer who approved the pair.</summary>
    public TinyId? ApprovedBy { get; private set; }

    /// <summary>When the pair was approved.</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>When it was generated.</summary>
    public DateTimeOffset GeneratedAt { get; private init; }

    /// <summary>When either language last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>When this summary was deleted along with its report, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>True once a human has approved the pair.</summary>
    public bool IsApproved => ApprovedAt is not null;

    /// <summary>Creates the summary generated from one Worker call.</summary>
    public static Summary Generate(
        TinyId reportId, string aiSummaryEn, string aiSummaryFr, string model, string promptVersion, DateTimeOffset at) =>
        new(reportId, aiSummaryEn, aiSummaryFr, model, promptVersion, at);

    /// <summary>
    /// Replaces the English text by hand — the escape hatch when the model
    /// failed. Editing either language clears the pair's approval.
    /// </summary>
    public void RewriteEn(string text, DateTimeOffset at)
    {
        AiSummaryEn = NotBlank(text);
        UpdatedAt = at;
        ClearApproval();
    }

    /// <summary>
    /// Replaces the French text by hand. Editing either language clears the
    /// pair's approval.
    /// </summary>
    public void RewriteFr(string text, DateTimeOffset at)
    {
        AiSummaryFr = NotBlank(text);
        UpdatedAt = at;
        ClearApproval();
    }

    /// <summary>Records a safety officer's approval of the whole pair.</summary>
    public void Approve(TinyId adminUserId, DateTimeOffset at)
    {
        ApprovedBy = adminUserId;
        ApprovedAt = at;
    }

    private void ClearApproval()
    {
        ApprovedBy = null;
        ApprovedAt = null;
    }

    private static string NotBlank(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? throw new DomainRuleViolationException("A summary cannot be blank.")
            : text;
}
