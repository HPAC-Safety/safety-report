
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// One answer to one question, as it was asked. The reference is to a
/// <see cref="QuestionRevision"/> rather than to the question, so rewording a
/// question tomorrow cannot change what an answer given today appears to mean.
/// </summary>
public class ReportAnswer
{
    // Not readonly: option codes are a primitive collection, which EF Core
    // assigns to the backing field rather than adding into an existing list.
    private List<string> _selectedOptionCodes = [];

    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private ReportAnswer()
    {
    }
#pragma warning restore CS8618

    private ReportAnswer(TinyId reportId, Question question, QuestionRevision revision, DateTimeOffset at)
    {
        Id = TinyId.New();
        ReportId = reportId;
        QuestionId = question.Id;
        QuestionRevisionId = revision.Id;
        QuestionKey = question.Key;
        IsPrivate = question.IsPrivate;
        AnsweredAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The report this answer belongs to.</summary>
    public TinyId ReportId { get; private init; }

    /// <summary>The question answered.</summary>
    public TinyId QuestionId { get; private init; }

    /// <summary>The exact revision answered, which owns the wording and options.</summary>
    public TinyId QuestionRevisionId { get; private init; }

    /// <summary>The question's invariant key, carried for exports and reads.</summary>
    public string QuestionKey { get; private init; }

    /// <summary>
    /// Whether this answer is private redaction context, copied from the
    /// immutable question contract when the answer is recorded.
    /// </summary>
    public bool IsPrivate { get; private init; }

    /// <summary>Free-text value, for the text-shaped types. Null for select types.</summary>
    public string? Value { get; private set; }

    /// <summary>Invariant option codes, for select types. Never display text.</summary>
    public IReadOnlyList<string> SelectedOptionCodes => _selectedOptionCodes;

    /// <summary>When the answer was given.</summary>
    public DateTimeOffset AnsweredAt { get; private init; }

    /// <summary>When this answer was deleted along with its report, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    internal static ReportAnswer ForText(TinyId reportId, Question question, string? value, DateTimeOffset at)
    {
        var revision = question.CurrentRevision;

        if (revision.CollectsNoAnswer)
        {
            throw new DomainRuleViolationException($"'{question.Key}' is a {revision.Type} and collects no answer.");
        }

        if (revision.ExpectsOptions)
        {
            throw new DomainRuleViolationException($"'{question.Key}' expects option codes, not free text.");
        }

        if (revision.IsRequired && string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleViolationException($"'{question.Key}' is required.");
        }

        return new ReportAnswer(reportId, question, revision, at) { Value = value };
    }

    internal static ReportAnswer ForOptions(
        TinyId reportId, Question question, IReadOnlyList<string> codes, DateTimeOffset at)
    {
        var revision = question.CurrentRevision;

        if (!revision.ExpectsOptions)
        {
            throw new DomainRuleViolationException($"'{question.Key}' is a {revision.Type} and takes a value, not option codes.");
        }

        if (revision.TakesOneAnswer && codes.Count > 1)
        {
            throw new DomainRuleViolationException($"'{question.Key}' takes one answer, not {codes.Count}.");
        }

        if (revision.IsRequired && codes.Count == 0)
        {
            throw new DomainRuleViolationException($"'{question.Key}' is required.");
        }

        foreach (var code in codes)
        {
            if (!revision.Accepts(code))
            {
                throw new DomainRuleViolationException($"'{code}' is not an option on '{question.Key}'.");
            }
        }

        var answer = new ReportAnswer(reportId, question, revision, at);
        answer._selectedOptionCodes.AddRange(codes);
        return answer;
    }

    /// <summary>The single selected code, for single-select questions.</summary>
    public string? SingleOptionCode => _selectedOptionCodes.Count == 1 ? _selectedOptionCodes[0] : null;
}
