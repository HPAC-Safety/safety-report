using HpacSafety.Core.Enums;
using HpacSafety.Core.Questions;

namespace HpacSafety.Core.Reports;

/// <summary>
/// One answer to one question, as it was asked. The reference is to a
/// <see cref="QuestionVersion"/> rather than to the question, so rewording a
/// question tomorrow cannot change what an answer given today appears to mean.
/// </summary>
public class ReportAnswer
{
    private readonly List<string> _selectedOptionCodes = [];

    private ReportAnswer(Guid reportId, Question question, QuestionVersion version, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        ReportId = reportId;
        QuestionId = question.Id;
        QuestionVersionId = version.Id;
        QuestionKey = question.Key;
        Sensitivity = question.Sensitivity;
        AnsweredAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The report this answer belongs to.</summary>
    public Guid ReportId { get; private init; }

    /// <summary>The question answered.</summary>
    public Guid QuestionId { get; private init; }

    /// <summary>The exact version answered, which owns the wording and options.</summary>
    public Guid QuestionVersionId { get; private init; }

    /// <summary>The question's invariant key, carried for exports and reads.</summary>
    public string QuestionKey { get; private init; }

    /// <summary>
    /// The tier this answer is handled at, copied from the question at answer
    /// time. Reclassifying a question later must not silently downgrade the
    /// handling of text a reporter already trusted us with.
    /// </summary>
    public SensitivityTier Sensitivity { get; private init; }

    /// <summary>Free-text value, for the text-shaped types. Null for select types.</summary>
    public string? Value { get; private set; }

    /// <summary>Invariant option codes, for select types. Never display text.</summary>
    public IReadOnlyList<string> SelectedOptionCodes => _selectedOptionCodes;

    /// <summary>When the answer was given.</summary>
    public DateTimeOffset AnsweredAt { get; private init; }

    internal static ReportAnswer ForText(Guid reportId, Question question, string? value, DateTimeOffset at)
    {
        var version = question.CurrentVersion;

        if (version.CollectsNoAnswer)
        {
            throw new DomainRuleViolationException($"'{question.Key}' is a {version.Type} and collects no answer.");
        }

        if (version.ExpectsOptions)
        {
            throw new DomainRuleViolationException($"'{question.Key}' expects option codes, not free text.");
        }

        if (version.IsRequired && string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleViolationException($"'{question.Key}' is required.");
        }

        return new ReportAnswer(reportId, question, version, at) { Value = value };
    }

    internal static ReportAnswer ForOptions(
        Guid reportId, Question question, IReadOnlyList<string> codes, DateTimeOffset at)
    {
        var version = question.CurrentVersion;

        if (!version.ExpectsOptions)
        {
            throw new DomainRuleViolationException($"'{question.Key}' is a {version.Type} and takes a value, not option codes.");
        }

        if (version.TakesOneAnswer && codes.Count > 1)
        {
            throw new DomainRuleViolationException($"'{question.Key}' takes one answer, not {codes.Count}.");
        }

        if (version.IsRequired && codes.Count == 0)
        {
            throw new DomainRuleViolationException($"'{question.Key}' is required.");
        }

        foreach (var code in codes)
        {
            if (!version.Accepts(code))
            {
                throw new DomainRuleViolationException($"'{code}' is not an option on '{question.Key}'.");
            }
        }

        var answer = new ReportAnswer(reportId, question, version, at);
        answer._selectedOptionCodes.AddRange(codes);
        return answer;
    }

    /// <summary>The single selected code, for single-select questions.</summary>
    public string? SingleOptionCode => _selectedOptionCodes.Count == 1 ? _selectedOptionCodes[0] : null;
}
