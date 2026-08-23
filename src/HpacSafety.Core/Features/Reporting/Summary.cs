using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>One anonymized summary candidate in the report language.</summary>
public sealed class Summary
{
#pragma warning disable CS8618 // EF Core sets every mapped property.
    private Summary()
    {
    }
#pragma warning restore CS8618

    private Summary(
        TinyId reportId,
        Locale locale,
        string text,
        string model,
        string promptVersion,
        DateTimeOffset at)
    {
        Id = TinyId.New();
        ReportId = reportId;
        Locale = locale;
        Text = NotBlank(text);
        Model = model;
        PromptVersion = promptVersion;
        CreatedAt = at;
    }

    /// <summary>Summary id.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Owning report.</summary>
    public TinyId ReportId { get; private init; }

    /// <summary>Summary language.</summary>
    public Locale Locale { get; private init; }

    /// <summary>Candidate text.</summary>
    public string Text { get; private set; }

    /// <summary>Provider model identifier.</summary>
    public string Model { get; private init; }

    /// <summary>Runtime prompt version.</summary>
    public string PromptVersion { get; private init; }

    /// <summary>Approving safety officer.</summary>
    public TinyId? ApprovedBy { get; private set; }

    /// <summary>Approval time.</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Whether a person approved the current text.</summary>
    public bool IsApproved => ApprovedAt is not null;

    /// <summary>Creates a model-generated candidate.</summary>
    public static Summary Generated(
        TinyId reportId,
        Locale locale,
        string text,
        string model,
        string promptVersion,
        DateTimeOffset at) =>
        new(reportId, locale, text, model, promptVersion, at);

    /// <summary>Edits the candidate and clears any earlier approval.</summary>
    public void Rewrite(string text)
    {
        Text = NotBlank(text);
        ApprovedBy = null;
        ApprovedAt = null;
    }

    /// <summary>Approves the current candidate.</summary>
    public void Approve(TinyId adminUserId, DateTimeOffset at)
    {
        ApprovedBy = adminUserId;
        ApprovedAt = at;
    }

    private static string NotBlank(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new DomainRuleViolationException("A summary cannot be blank.")
            : value;
}
