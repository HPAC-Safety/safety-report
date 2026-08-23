using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>One nullable answer to the exact immutable question revision shown.</summary>
public sealed class ReportAnswer
{
    private List<string> _selectedOptionCodes = [];

#pragma warning disable CS8618 // EF Core sets every mapped property.
    private ReportAnswer()
    {
    }
#pragma warning restore CS8618

    private ReportAnswer(TinyId reportId, Question question, DateTimeOffset at)
    {
        Id = TinyId.New();
        ReportId = reportId;
        QuestionId = question.Id;
        QuestionKey = question.Key;
        AnsweredAt = at;
    }

    /// <summary>Answer id.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Owning report.</summary>
    public TinyId ReportId { get; private init; }

    /// <summary>The exact immutable question revision shown.</summary>
    public TinyId QuestionId { get; private init; }

    /// <summary>Stable question key for exports.</summary>
    public string QuestionKey { get; private init; }

    /// <summary>Nullable text answer; null records an optional skip.</summary>
    public string? Value { get; private init; }

    /// <summary>Selected invariant option codes; empty records an optional skip.</summary>
    public IReadOnlyList<string> SelectedOptionCodes => _selectedOptionCodes;

    /// <summary>When the answer was recorded.</summary>
    public DateTimeOffset AnsweredAt { get; private init; }

    internal static ReportAnswer ForText(
        TinyId reportId,
        Question question,
        string? value,
        DateTimeOffset at)
    {
        if (question.CollectsNoAnswer)
        {
            throw new DomainRuleViolationException($"'{question.Key}' collects no answer.");
        }

        if (question.ExpectsOptions)
        {
            throw new DomainRuleViolationException($"'{question.Key}' expects option codes.");
        }

        return new ReportAnswer(reportId, question, at)
        {
            Value = string.IsNullOrWhiteSpace(value) ? null : value,
        };
    }

    internal static ReportAnswer ForOptions(
        TinyId reportId,
        Question question,
        IReadOnlyList<string> codes,
        DateTimeOffset at)
    {
        if (!question.ExpectsOptions)
        {
            throw new DomainRuleViolationException($"'{question.Key}' does not take option codes.");
        }

        if (question.Type is QuestionType.SingleSelect or QuestionType.YesNo && codes.Count > 1)
        {
            throw new DomainRuleViolationException($"'{question.Key}' takes at most one answer.");
        }

        if (question.IsRequired && codes.Count == 0)
        {
            throw new DomainRuleViolationException($"'{question.Key}' is required.");
        }

        foreach (var code in codes)
        {
            if (!question.Accepts(code))
            {
                throw new DomainRuleViolationException($"'{code}' is not an option on '{question.Key}'.");
            }
        }

        var answer = new ReportAnswer(reportId, question, at);
        answer._selectedOptionCodes.AddRange(codes.Select(QuestionBank.QuestionKey.Normalize));
        return answer;
    }

    /// <summary>The one selected code, or null.</summary>
    public string? SingleOptionCode => _selectedOptionCodes.Count == 1 ? _selectedOptionCodes[0] : null;
}
