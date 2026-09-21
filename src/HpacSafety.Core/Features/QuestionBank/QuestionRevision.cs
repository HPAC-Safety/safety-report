

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// A question exactly as it was asked at a point in time: its type, its complete
/// bilingual wording, its order, section, privacy, active state, required
/// state, system state, and option set. Immutable once created — rewording,
/// retyping, reordering, moving into a section, changing privacy, activating,
/// deactivating, or changing the options produces a new revision, so a report
/// filed last year still renders the revision it was actually answering.
/// </summary>
/// <remarks>
/// <para>
/// A revision is born complete: both official languages are supplied together,
/// atomically, by whoever authors it. There is no partially translated, pending,
/// or machine-generated state to reach a database — see product invariant #1 and
/// <c>docs/data-and-persistence.md</c>.
/// </para>
/// <para>
/// Order, section, privacy, active state, system state, required state, and the
/// complete ordered option set are all revision fields — see
/// <c>features/question-bank-and-form/question-bank-and-form.feature</c>. None
/// of them can be mutated on an existing revision; every change, including
/// these, is a new revision row created by <see cref="Question"/>.
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
        string labelEn,
        string labelFr,
        string? helpTextEn,
        string? helpTextFr,
        string? placeholderEn,
        string? placeholderFr,
        bool isSystem,
        bool isRequired,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        string? sectionKey,
        TinyId? dependsOnQuestionId,
        TinyId? optionSetId,
        IReadOnlyList<QuestionOptionInput> options,
        DateTimeOffset at)
    {
        Id = TinyId.New();
        QuestionId = questionId;
        RevisionNumber = revisionNumber;
        Type = type;
        // Only the publication-consent question is a system question, and it is
        // always required — a form that lets a reporter skip consent cannot
        // publish anything. Every other question's required state is authored
        // by an administrator. See ADR-0061.
        IsSystem = isSystem;
        IsRequired = isSystem || isRequired;
        IsPrivate = isPrivate;
        IsActive = isActive;
        DisplayOrder = displayOrder;
        SectionKey = sectionKey is null ? null : QuestionKey.Normalize(sectionKey);
        DependsOnQuestionId = ValidatedDependency(dependsOnQuestionId, questionId, type, isSystem);
        OptionSetId = optionSetId;
        LabelEn = NotBlank(labelEn);
        LabelFr = NotBlank(labelFr);
        HelpTextEn = helpTextEn;
        HelpTextFr = helpTextFr;
        PlaceholderEn = placeholderEn;
        PlaceholderFr = placeholderFr;
        CreatedAt = at;

        PopulateOptions(options);
    }

    /// <summary>Surrogate key. Answers reference this, never the question row.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The question this is a revision of.</summary>
    public TinyId QuestionId { get; private init; }

    /// <summary>Increments by one per revision, starting at 1.</summary>
    public int RevisionNumber { get; private init; }

    /// <summary>What this revision asks for.</summary>
    public QuestionType Type { get; private init; }

    /// <summary>
    /// True only for the publication-consent revision. Copied from the
    /// question at revision-creation time — every revision of the same
    /// question carries the same value, since a question's system status
    /// never changes across its history.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Whether a reporter must answer before submitting. Authored by an
    /// administrator on every ordinary question, and forced true on the
    /// publication-consent question, which cannot be made optional. See
    /// ADR-0061.
    /// </summary>
    public bool IsRequired { get; private init; }

    /// <summary>
    /// Whether this answer is private redaction context rather than a fact
    /// eligible for the summary.
    /// </summary>
    public bool IsPrivate { get; private init; }

    /// <summary>Whether this revision is the one the form asks.</summary>
    public bool IsActive { get; private init; }

    /// <summary>Where this revision sits on the form.</summary>
    public int DisplayOrder { get; private init; }

    /// <summary>The section this revision is grouped under, if any.</summary>
    public string? SectionKey { get; private init; }

    /// <summary>
    /// The question this one is conditional on, if any. The form enables this
    /// question only when that question is answered yes.
    /// </summary>
    /// <remarks>
    /// This names the stable <see cref="Question"/>, not a revision of it, so
    /// rewording the parent does not break the child. Only a
    /// <see cref="QuestionType.YesNo"/> question may be a parent; that is a
    /// fact about the parent's current revision, which this row cannot see, so
    /// it is checked by <see cref="QuestionDependencies"/> rather than here. See
    /// ADR-0060.
    /// </remarks>
    public TinyId? DependsOnQuestionId { get; private init; }

    /// <summary>
    /// The shared <see cref="OptionSet"/> this revision's options were copied
    /// from, if any. Provenance only — the copy in <see cref="Options"/> is
    /// what this revision offers, whatever later happens to the set. See
    /// ADR-0058.
    /// </summary>
    public TinyId? OptionSetId { get; private init; }

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

    /// <summary>The choices, for select-style types. Empty otherwise. Fixed at
    /// creation — see <see cref="QuestionOptionInput"/>.</summary>
    public IReadOnlyCollection<QuestionRevisionOption> Options => _options;

    /// <summary>The wording in one locale.</summary>
    public string Label(Locale locale) => locale == Locale.FrCa ? LabelFr : LabelEn;

    /// <summary>The help text in one locale.</summary>
    public string? HelpText(Locale locale) => locale == Locale.FrCa ? HelpTextFr : HelpTextEn;

    /// <summary>True when this type stores an option code rather than free text.</summary>
    public bool ExpectsOptions =>
        Type is QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.YesNo
            or QuestionType.Autocomplete;

    /// <summary>
    /// True when this type's options may come from a shared
    /// <see cref="OptionSet"/>. Yes/no is excluded: its two answers are not
    /// option rows at all.
    /// </summary>
    public bool AcceptsOptionSet =>
        Type is QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.Autocomplete;

    /// <summary>True when this type takes at most one answer.</summary>
    public bool TakesOneAnswer =>
        Type is QuestionType.SingleSelect or QuestionType.YesNo or QuestionType.Autocomplete;

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
        string labelEn,
        string labelFr,
        string? helpTextEn,
        string? helpTextFr,
        string? placeholderEn,
        string? placeholderFr,
        bool isSystem,
        bool isRequired,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        string? sectionKey,
        TinyId? dependsOnQuestionId,
        TinyId? optionSetId,
        IReadOnlyList<QuestionOptionInput> options,
        DateTimeOffset at) =>
        new(
            questionId, revisionNumber, type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
            isSystem, isRequired, isPrivate, isActive, displayOrder, sectionKey, dependsOnQuestionId, optionSetId,
            options, at);

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

    /// <summary>
    /// Builds the complete, ordered option set this revision is born with.
    /// There is no public equivalent that runs after construction — see the
    /// class remarks.
    /// </summary>
    private void PopulateOptions(IReadOnlyList<QuestionOptionInput> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            return;
        }

        if (Type == QuestionType.YesNo)
        {
            throw new DomainRuleViolationException(
                "A yes/no question has exactly two answers, yes and no. It cannot be given more, and it has no default.");
        }

        if (!ExpectsOptions)
        {
            throw new DomainRuleViolationException($"A {Type} question does not have options.");
        }

        for (var i = 0; i < options.Count; i++)
        {
            var input = options[i];
            var normalized = QuestionKey.Normalize(input.Code);

            if (_options.Exists(o => o.Code == normalized))
            {
                throw new DomainRuleViolationException($"This question already has an option coded '{normalized}'.");
            }

            _options.Add(QuestionRevisionOption.Create(Id, normalized, i, input.LabelEn, input.LabelFr, input.SourceItemId));
        }
    }

    /// <summary>
    /// Checks the part of a dependency this row can see on its own: that it
    /// does not point at itself, and that the question is one a reporter can
    /// answer conditionally at all. Whether the <i>parent</i> is a yes/no
    /// question is a fact about a different row, so <see cref="QuestionDependencies"/>
    /// checks that. See ADR-0060.
    /// </summary>
    private static TinyId? ValidatedDependency(
        TinyId? dependsOnQuestionId, TinyId questionId, QuestionType type, bool isSystem)
    {
        if (dependsOnQuestionId is not { } parent)
        {
            return null;
        }

        if (parent == questionId)
        {
            throw new DomainRuleViolationException("A question cannot be conditional on itself.");
        }

        if (isSystem)
        {
            throw new DomainRuleViolationException(
                "Publication consent is always asked. Making it conditional would let a report reach the form with no consent question at all.");
        }

        return type is QuestionType.Statement or QuestionType.Group
            ? throw new DomainRuleViolationException($"A {type} question collects no answer and cannot be made conditional.")
            : parent;
    }

    private static string NotBlank(string label) =>
        string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("A question revision needs wording in both official languages.")
            : label;
}
