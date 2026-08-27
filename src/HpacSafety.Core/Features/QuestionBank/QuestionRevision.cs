
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// A question exactly as it was asked at a point in time: its type, its complete
/// bilingual wording, and its option set. Immutable once created — rewording,
/// retyping, or changing the options produces a new revision, so a report filed
/// last year still renders the revision it was actually answering.
/// </summary>
/// <remarks>
/// <para>
/// A revision is born complete: both official languages are supplied together,
/// atomically, by whoever authors it. There is no partially translated, pending,
/// or machine-generated state to reach a database — see product invariant #1 and
/// <c>docs/data-and-persistence.md</c>.
/// </para>
/// <para>
/// Reordering and activation are deliberately <b>not</b> versioned. Neither
/// changes what an answer means.
/// </para>
/// </remarks>
public class QuestionRevision
{
    private readonly List<QuestionRevisionOption> _options = [];

    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private QuestionRevision()
    {
    }
#pragma warning restore CS8618

    private QuestionRevision(
        TinyId questionId,
        int revisionNumber,
        QuestionType type,
        bool isRequired,
        string labelEn,
        string labelFr,
        string? helpTextEn,
        string? helpTextFr,
        string? placeholderEn,
        string? placeholderFr,
        DateTimeOffset at)
    {
        Id = TinyId.New();
        QuestionId = questionId;
        RevisionNumber = revisionNumber;
        Type = type;
        IsRequired = isRequired;
        LabelEn = NotBlank(labelEn);
        LabelFr = NotBlank(labelFr);
        HelpTextEn = helpTextEn;
        HelpTextFr = helpTextFr;
        PlaceholderEn = placeholderEn;
        PlaceholderFr = placeholderFr;
        CreatedAt = at;
    }

    /// <summary>Surrogate key. Answers reference this, never the question row.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The question this is a revision of.</summary>
    public TinyId QuestionId { get; private init; }

    /// <summary>Increments by one per revision, starting at 1.</summary>
    public int RevisionNumber { get; private init; }

    /// <summary>What this revision asks for.</summary>
    public QuestionType Type { get; private init; }

    /// <summary>Whether a reporter must answer before submitting.</summary>
    public bool IsRequired { get; private init; }

    /// <summary>The English wording.</summary>
    public string LabelEn { get; private init; }

    /// <summary>The French wording.</summary>
    public string LabelFr { get; private init; }

    /// <summary>Supporting English copy shown under the label.</summary>
    public string? HelpTextEn { get; private init; }

    /// <summary>Supporting French copy shown under the label.</summary>
    public string? HelpTextFr { get; private init; }

    /// <summary>English placeholder text, for free-text types.</summary>
    public string? PlaceholderEn { get; private init; }

    /// <summary>French placeholder text, for free-text types.</summary>
    public string? PlaceholderFr { get; private init; }

    /// <summary>When this revision was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When this revision was deleted along with its question, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>The choices, for select-style types. Empty otherwise.</summary>
    public IReadOnlyCollection<QuestionRevisionOption> Options => _options;

    /// <summary>The wording in one locale.</summary>
    public string Label(Locale locale) => locale == Locale.FrCa ? LabelFr : LabelEn;

    /// <summary>The help text in one locale.</summary>
    public string? HelpText(Locale locale) => locale == Locale.FrCa ? HelpTextFr : HelpTextEn;

    /// <summary>True when this type stores an option code rather than free text.</summary>
    public bool ExpectsOptions =>
        Type is QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.YesNo;

    /// <summary>True when this type takes at most one answer.</summary>
    public bool TakesOneAnswer => Type is QuestionType.SingleSelect or QuestionType.YesNo;

    /// <summary>
    /// The two codes a <see cref="QuestionType.YesNo"/> question accepts. Fixed,
    /// unorderable, and with no third state — a yes/no question has no default
    /// and the reporter must choose one.
    /// </summary>
    public static IReadOnlyList<string> YesNoCodes { get; } = ["yes", "no"];

    /// <summary>True when this type collects no answer at all.</summary>
    public bool CollectsNoAnswer => Type is QuestionType.Statement or QuestionType.Group;

    internal static QuestionRevision Create(
        TinyId questionId,
        int revisionNumber,
        QuestionType type,
        bool isRequired,
        string labelEn,
        string labelFr,
        string? helpTextEn,
        string? helpTextFr,
        string? placeholderEn,
        string? placeholderFr,
        DateTimeOffset at) =>
        new(questionId, revisionNumber, type, isRequired, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr, at);

    /// <summary>Adds a choice, in both official languages at once.</summary>
    public QuestionRevisionOption AddOption(string code, string labelEn, string labelFr)
    {
        if (Type == QuestionType.YesNo)
        {
            throw new DomainRuleViolationException(
                "A yes/no question has exactly two answers, yes and no. It cannot be given more, and it has no default.");
        }

        if (!ExpectsOptions)
        {
            throw new DomainRuleViolationException($"A {Type} question does not have options.");
        }

        var normalized = QuestionKey.Normalize(code);
        if (_options.Exists(o => o.Code == normalized))
        {
            throw new DomainRuleViolationException($"This question already has an option coded '{normalized}'.");
        }

        var option = QuestionRevisionOption.Create(Id, normalized, _options.Count, labelEn, labelFr);
        _options.Add(option);
        return option;
    }

    /// <summary>Finds a choice by its invariant code. Null for
    /// <see cref="QuestionType.YesNo"/>, whose two answers are not option rows —
    /// their labels are ordinary UI chrome and live in <c>locales/</c>.</summary>
    public QuestionRevisionOption? Option(string code) =>
        _options.Find(o => o.Code == code);

    /// <summary>Whether this revision accepts an answer code.</summary>
    public bool Accepts(string code) =>
        Type == QuestionType.YesNo
            ? YesNoCodes.Contains(code, StringComparer.OrdinalIgnoreCase)
            : Option(code) is not null;

    private static string NotBlank(string label) =>
        string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("A question revision needs wording in both official languages.")
            : label;
}
