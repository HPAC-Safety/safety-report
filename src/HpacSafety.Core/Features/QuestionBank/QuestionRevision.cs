namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     A question exactly as it was asked at a point in time: its type, its complete
///     bilingual wording, its order, privacy, active state, required
///     state, system state, and option set. Immutable once created — rewording,
///     retyping, reordering, changing privacy, activating,
///     deactivating, or changing the options produces a new revision, so a report
///     filed last year still renders the revision it was actually answering.
/// </summary>
/// <remarks>
///     <para>
///         A revision is born complete: both official languages are supplied together,
///         atomically, by whoever authors it. There is no partially translated, pending,
///         or machine-generated state to reach a database — see product invariant #1 and
///         <c>docs/data-and-persistence.md</c>.
///     </para>
///     <para>
///         Order, privacy, active state, system state, required state, and the
///         complete ordered option set are all revision fields — see
///         <c>features/question-bank-and-form/question-bank-and-form.feature</c>. None
///         of them can be mutated on an existing revision; every change, including
///         these, is a new revision row created by <see cref="Question" />.
///     </para>
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
        TinyId? dependsOnQuestionId,
        string? dependsOnOptionCode,
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
        DependsOnQuestionId = ValidatedDependency(dependsOnQuestionId, questionId, isSystem);
        DependsOnOptionCode = ValidatedOptionCode(dependsOnOptionCode, DependsOnQuestionId);
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
    ///     True only for the publication-consent revision. Copied from the
    ///     question at revision-creation time — every revision of the same
    ///     question carries the same value, since a question's system status
    ///     never changes across its history.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    ///     Whether a reporter must answer before submitting. Authored by an
    ///     administrator on every ordinary question, and forced true on the
    ///     publication-consent question, which cannot be made optional. See
    ///     ADR-0061.
    /// </summary>
    public bool IsRequired { get; private init; }

    /// <summary>
    ///     Whether this answer is private redaction context rather than a fact
    ///     eligible for the summary.
    /// </summary>
    public bool IsPrivate { get; private init; }

    /// <summary>Whether this revision is the one the form asks.</summary>
    public bool IsActive { get; private init; }

    /// <summary>Where this revision sits on the form.</summary>
    public int DisplayOrder { get; private init; }

    /// <summary>
    ///     The question this one is conditional on, if any. The form enables this
    ///     question only when that question's answer satisfies the condition:
    ///     "yes" for a yes/no parent, or the option named by
    ///     <see cref="DependsOnOptionCode" /> for a single-select parent.
    /// </summary>
    /// <remarks>
    ///     This names the stable <see cref="Question" />, not a revision of it, so
    ///     rewording the parent does not break the child. Only a
    ///     <see cref="QuestionType.YesNo" /> or <see cref="QuestionType.SingleSelect" />
    ///     question may be a parent; that is a fact about the parent's current
    ///     revision, which this row cannot see, so it is checked by
    ///     <see cref="QuestionDependencies" /> rather than here. See ADR-0060,
    ///     ADR-0074.
    /// </remarks>
    public TinyId? DependsOnQuestionId { get; private init; }

    /// <summary>
    ///     The invariant option code a <see cref="QuestionType.SingleSelect" />
    ///     parent must be answered with to enable this question. Always
    ///     <c>null</c> when <see cref="DependsOnQuestionId" /> is null or names a
    ///     <see cref="QuestionType.YesNo" /> parent, whose condition is the
    ///     invariant "yes" instead. See ADR-0074.
    /// </summary>
    public string? DependsOnOptionCode { get; private init; }

    /// <summary>
    ///     The shared <see cref="OptionSet" /> this revision's options were copied
    ///     from, if any. Provenance only — the copy in <see cref="Options" /> is
    ///     what this revision offers, whatever later happens to the set. See
    ///     ADR-0058.
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

    /// <summary>
    ///     The choices, for select-style types. Empty otherwise. Fixed at
    ///     creation — see <see cref="QuestionOptionInput" />.
    /// </summary>
    public IReadOnlyCollection<QuestionRevisionOption> Options => _options;

    /// <summary>
    ///     True when this type answers from a fixed set of choices rather
    ///     than free text.
    /// </summary>
    public bool ExpectsOptions =>
        Type is QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.YesNo
            or QuestionType.Autocomplete;

    /// <summary>
    ///     True when an answer to this type is stored in the reporter's language and
    ///     so needs an administrator to supply the other one (ADR-0072). Yes/no is
    ///     excluded although it answers from a fixed set: its stored form is the
    ///     invariant <c>yes</c> or <c>no</c>, identical in both languages, which is
    ///     what lets a conditional question compare it without knowing the locale
    ///     (ADR-0060).
    /// </summary>
    public bool StoresLocalizedValue => ExpectsOptions && Type != QuestionType.YesNo;

    /// <summary>
    ///     True when this type's options may come from a shared
    ///     <see cref="OptionSet" />. Yes/no is excluded: its two answers are not
    ///     option rows at all.
    /// </summary>
    public bool AcceptsOptionSet =>
        Type is QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.Autocomplete;

    /// <summary>True when this type takes at most one answer.</summary>
    public bool TakesOneAnswer =>
        Type is QuestionType.SingleSelect or QuestionType.YesNo or QuestionType.Autocomplete;

    /// <summary>
    ///     The two codes a <see cref="QuestionType.YesNo" /> question accepts. Fixed,
    ///     unorderable, and with no third state — a yes/no question has no default
    ///     and the reporter must choose one.
    /// </summary>
    public static IReadOnlyList<string> YesNoCodes { get; } = ["yes", "no"];

    /// <summary>The wording in one locale.</summary>
    public string Label(Locale locale)
    {
        return locale == Locale.FrCa ? LabelFr : LabelEn;
    }

    /// <summary>The help text in one locale.</summary>
    public string? HelpText(Locale locale)
    {
        return locale == Locale.FrCa ? HelpTextFr : HelpTextEn;
    }

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
        TinyId? dependsOnQuestionId,
        string? dependsOnOptionCode,
        TinyId? optionSetId,
        IReadOnlyList<QuestionOptionInput> options,
        DateTimeOffset at)
    {
        return new QuestionRevision(
            questionId, revisionNumber, type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
            isSystem, isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnOptionCode,
            optionSetId, options, at);
    }

    /// <summary>
    ///     Finds a choice by its invariant code. Null for
    ///     <see cref="QuestionType.YesNo" />, whose two answers are not option rows —
    ///     their labels are ordinary UI chrome and live in <c>locales/</c>.
    /// </summary>
    public QuestionRevisionOption? Option(string code)
    {
        return _options.Find(o => o.Code == code);
    }

    /// <summary>Whether this revision accepts an answer code.</summary>
    public bool Accepts(string code)
    {
        return Type == QuestionType.YesNo
            ? YesNoCodes.Contains(code, StringComparer.OrdinalIgnoreCase)
            : Option(code) is not null;
    }

    /// <summary>
    ///     Whether this revision offered the given value, written as the reporter
    ///     saw it in their own language. This is the check an answer is validated
    ///     against now that answers store their words rather than a code
    ///     (ADR-0072), and it reads the same frozen snapshot <see cref="Accepts" />
    ///     always did.
    /// </summary>
    /// <remarks>
    ///     Yes/no is invariant: its two stored forms are <c>yes</c> and <c>no</c> in
    ///     both languages, and the words a reporter actually saw are UI chrome from
    ///     <c>locales/</c> rather than option rows.
    /// </remarks>
    public bool Offers(string value, Locale locale)
    {
        return Type == QuestionType.YesNo
            ? YesNoCodes.Contains(value, StringComparer.Ordinal)
            : _options.Exists(option => string.Equals(option.Label(locale), value, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Builds the complete, ordered option set this revision is born with.
    ///     There is no public equivalent that runs after construction — see the
    ///     class remarks.
    /// </summary>
    private void PopulateOptions(IReadOnlyList<QuestionOptionInput> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0) return;

        if (Type == QuestionType.YesNo)
            throw new DomainRuleViolationException(
                "A yes/no question has exactly two answers, yes and no. It cannot be given more, and it has no default.");

        if (!ExpectsOptions) throw new DomainRuleViolationException($"A {Type} question does not have options.");

        for (var i = 0; i < options.Count; i++)
        {
            var input = options[i];
            var normalized = QuestionKey.Normalize(input.Code);

            if (_options.Exists(o => o.Code == normalized)) throw new DomainRuleViolationException($"This question already has an option coded '{normalized}'.");

            _options.Add(QuestionRevisionOption.Create(Id, normalized, i, input.LabelEn, input.LabelFr, input.SourceItemId));
        }
    }

    /// <summary>
    ///     Checks the part of a dependency this row can see on its own: that it
    ///     does not point at itself, and that the question is not the
    ///     publication-consent system question. Whether the <i>parent</i> is a
    ///     question type that can enable another one is a fact about a different
    ///     row, so <see cref="QuestionDependencies" /> checks that. See ADR-0060.
    /// </summary>
    private static TinyId? ValidatedDependency(TinyId? dependsOnQuestionId, TinyId questionId, bool isSystem)
    {
        if (dependsOnQuestionId is not { } parent) return null;

        if (parent == questionId) throw new DomainRuleViolationException("A question cannot be conditional on itself.");

        if (isSystem)
            throw new DomainRuleViolationException(
                "Publication consent is always asked. Making it conditional would let a report reach the form with no consent question at all.");

        return parent;
    }

    /// <summary>
    ///     Normalizes a required option code, and refuses one with no parent to
    ///     attach it to. Whether the parent's type actually takes an option code
    ///     (a <see cref="QuestionType.SingleSelect" /> parent does, a
    ///     <see cref="QuestionType.YesNo" /> one does not) is, like the parent's
    ///     type itself, a fact <see cref="QuestionDependencies" /> checks against
    ///     the live bank. See ADR-0074.
    /// </summary>
    private static string? ValidatedOptionCode(string? dependsOnOptionCode, TinyId? dependsOnQuestionId)
    {
        if (dependsOnOptionCode is null) return null;

        if (dependsOnQuestionId is null) throw new DomainRuleViolationException("A required option needs a parent question to name it.");

        return QuestionKey.Normalize(dependsOnOptionCode);
    }

    /// <summary>
    ///     Whether the reporter's answer to <paramref name="parent" /> satisfies
    ///     this revision's condition, so the question it belongs to should be
    ///     shown. Always true when this revision is unconditional. See ADR-0074.
    /// </summary>
    /// <param name="parent">
    ///     The question named by <see cref="DependsOnQuestionId" />, or null when
    ///     this revision is unconditional or the caller has not loaded it.
    /// </param>
    /// <param name="parentAnswerValue">
    ///     The reporter's answer to <paramref name="parent" /> so far, in
    ///     <paramref name="locale" />, or null when they have not answered it yet.
    /// </param>
    /// <param name="locale">The locale <paramref name="parentAnswerValue" /> was given in.</param>
    public bool IsEnabledGiven(Question? parent, string? parentAnswerValue, Locale locale)
    {
        if (DependsOnQuestionId is null) return true;

        if (parent is null || parentAnswerValue is null) return false;

        var parentRevision = parent.CurrentRevision;

        return parentRevision.Type == QuestionType.YesNo
            ? string.Equals(parentAnswerValue, "yes", StringComparison.Ordinal)
            : DependsOnOptionCode is { } requiredOptionCode
              && string.Equals(
                  parentRevision.Option(requiredOptionCode)?.Label(locale), parentAnswerValue, StringComparison.Ordinal);
    }

    private static string NotBlank(string label)
    {
        return string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("A question revision needs wording in both official languages.")
            : label;
    }
}
