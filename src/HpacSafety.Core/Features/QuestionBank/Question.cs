
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// A question on the occurrence form. The question set is data — an
/// administrator adds, rewords, retypes, reorders, and removes questions without
/// a deploy — so this is the aggregate root of the question bank.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one question is a <b>system question</b>: publication consent. It
/// cannot be deleted, deactivated, retyped, or rekeyed, because it is the gate
/// every publication path checks and there is no defined behaviour without it.
/// Its wording is still editable, and it reorders like any other.
/// </para>
/// <para>
/// Nothing but consent projects onto a typed property of
/// <see cref="Reporting.Report"/> — the admin review DTO reads exact asked
/// questions and answers directly. See <c>docs/data-and-persistence.md</c>.
/// </para>
/// <para>
/// Order, section, privacy, active state, system state, required state, and the
/// complete ordered option set all live on <see cref="QuestionRevision"/>, not
/// here — a referenced revision has to preserve the complete question exactly
/// as it was shown, and none of those facts can be reconstructed from the
/// current state of a mutable question row. Every read here that looks
/// question-scoped (<see cref="IsPrivate"/>, <see cref="DisplayOrder"/>,
/// <see cref="SectionKey"/>, <see cref="IsActive"/>) reads through to
/// <see cref="CurrentRevision"/>, and every change to one of them is made by
/// creating a new revision. See
/// <c>features/question-bank-and-form/question-bank-and-form.feature</c>.
/// </para>
/// </remarks>
public class Question
{
    private readonly List<QuestionRevision> _revisions = [];

    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private Question()
    {
    }
#pragma warning restore CS8618

    private Question(string key, bool isSystem, QuestionRole role, DateTimeOffset at)
    {
        Id = TinyId.New();
        Key = QuestionKey.Normalize(key);
        IsSystem = isSystem;
        Role = role;
        CreatedAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Stable invariant identity, used by exports and integrations.</summary>
    public string Key { get; private init; }

    /// <summary>True only for publication consent.</summary>
    public bool IsSystem { get; private init; }

    /// <summary>What downstream logic reads this answer for, if anything.</summary>
    public QuestionRole Role { get; private set; }

    /// <summary>
    /// Whether answers are private redaction context rather than facts eligible
    /// for the summary, on the current revision. See ADR-0038 and the class
    /// remarks: this is a revision field, changed by creating a new revision.
    /// </summary>
    public bool IsPrivate => CurrentRevision.IsPrivate;

    /// <summary>Where this question sits on the form today. Not versioned
    /// independently — see the class remarks.</summary>
    public int DisplayOrder => CurrentRevision.DisplayOrder;

    /// <summary>The section this question is grouped under today, if any.</summary>
    public string? SectionKey => CurrentRevision.SectionKey;

    /// <summary>Whether the public form asks this question today. Always false
    /// once the question itself is deleted, regardless of what the current
    /// revision says.</summary>
    public bool IsActive => Deleted is null && CurrentRevision.IsActive;

    /// <summary>When this question was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When this question was retired, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>Every revision, oldest first. Answers reference one of these.</summary>
    public IReadOnlyList<QuestionRevision> Revisions => _revisions;

    /// <summary>
    /// The revision the form asks today. Selected by the highest revision
    /// number rather than list position: EF Core does not guarantee the order
    /// of a loaded navigation collection, so the last element of
    /// <see cref="_revisions"/> can be an arbitrary historical row after a
    /// load.
    /// </summary>
    public QuestionRevision CurrentRevision =>
        _revisions.Count > 0
            ? _revisions.MaxBy(revision => revision.RevisionNumber)!
            : throw new DomainRuleViolationException("A question always has at least one revision.");

    /// <summary>What this question currently asks for.</summary>
    public QuestionType Type => CurrentRevision.Type;

    /// <summary>Creates an ordinary question, complete in both official languages.</summary>
    public static Question Create(
        string key,
        QuestionType type,
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        string? helpTextEn = null,
        string? helpTextFr = null,
        string? placeholderEn = null,
        string? placeholderFr = null,
        QuestionRole role = QuestionRole.None,
        bool isPrivate = true,
        bool isActive = false,
        int displayOrder = 0,
        string? sectionKey = null,
        IReadOnlyList<QuestionOptionInput>? options = null) =>
        Create(
            key, type, labelEn, labelFr, at, isSystem: false, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
            role, isPrivate, isActive, displayOrder, sectionKey, options);

    /// <summary>
    /// Creates the publication-consent question. The only question the system
    /// refuses to lose, and the only caller of this method.
    /// </summary>
    public static Question CreateConsentPublish(
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        string? helpTextEn = null,
        string? helpTextFr = null,
        int displayOrder = 0) =>
        Create(
            QuestionKey.ConsentPublish,
            QuestionType.YesNo,
            labelEn,
            labelFr,
            at,
            isSystem: true,
            helpTextEn,
            helpTextFr,
            placeholderEn: null,
            placeholderFr: null,
            QuestionRole.ConsentPublish,
            isPrivate: true,
            isActive: true,
            displayOrder,
            sectionKey: null,
            options: null);

    private static Question Create(
        string key,
        QuestionType type,
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        bool isSystem,
        string? helpTextEn,
        string? helpTextFr,
        string? placeholderEn,
        string? placeholderFr,
        QuestionRole role,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        string? sectionKey,
        IReadOnlyList<QuestionOptionInput>? options)
    {
        var question = new Question(key, isSystem, role, at);
        question._revisions.Add(
            QuestionRevision.Create(
                question.Id, 1, type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
                isSystem, isPrivate, isActive, displayOrder, sectionKey, options ?? [], at));
        return question;
    }

    /// <summary>
    /// Rewords, retypes, reorders, moves, reclassifies, activates, deactivates,
    /// or changes the options of this question, producing one new complete
    /// bilingual revision. Answers already given keep pointing at the revision
    /// they were given under, so an old report still shows exactly what it was
    /// actually asked, including the order, section, privacy, and active state
    /// in force at the time.
    /// </summary>
    public QuestionRevision Revise(
        QuestionType type,
        string labelEn,
        string labelFr,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        string? sectionKey,
        DateTimeOffset at,
        string? helpTextEn = null,
        string? helpTextFr = null,
        string? placeholderEn = null,
        string? placeholderFr = null,
        IReadOnlyList<QuestionOptionInput>? options = null)
    {
        if (IsSystem && type != Type)
        {
            throw new DomainRuleViolationException(
                $"'{Key}' is a system question. Its wording can change; its type cannot.");
        }

        return ReviseInternal(
            type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
            isPrivate, isActive, displayOrder, sectionKey, options ?? [], at);
    }

    /// <summary>Moves the question on the form, as a new revision. Every other
    /// field is carried forward unchanged from <see cref="CurrentRevision"/>.</summary>
    public QuestionRevision Reorder(int displayOrder, DateTimeOffset at) =>
        ReviseInternal(
            Type, CurrentRevision.LabelEn, CurrentRevision.LabelFr, CurrentRevision.HelpTextEn, CurrentRevision.HelpTextFr,
            CurrentRevision.PlaceholderEn, CurrentRevision.PlaceholderFr, CurrentRevision.IsPrivate, CurrentRevision.IsActive,
            displayOrder, CurrentRevision.SectionKey, CurrentOptions(), at);

    /// <summary>Moves the question into a section, or out of one, as a new
    /// revision. Every other field is carried forward unchanged.</summary>
    public QuestionRevision MoveToSection(string? sectionKey, DateTimeOffset at) =>
        ReviseInternal(
            Type, CurrentRevision.LabelEn, CurrentRevision.LabelFr, CurrentRevision.HelpTextEn, CurrentRevision.HelpTextFr,
            CurrentRevision.PlaceholderEn, CurrentRevision.PlaceholderFr, CurrentRevision.IsPrivate, CurrentRevision.IsActive,
            CurrentRevision.DisplayOrder, sectionKey, CurrentOptions(), at);

    /// <summary>Reassigns what logic reads this answer for. A role lives on at
    /// most one active question at a time; that is enforced by the question bank,
    /// not here.</summary>
    public void AssignRole(QuestionRole role)
    {
        EnsureNotDeleted();

        if (IsSystem && role != QuestionRole.ConsentPublish)
        {
            throw new DomainRuleViolationException($"'{Key}' carries publication consent and cannot give up that role.");
        }

        Role = role;
    }

    /// <summary>
    /// Starts asking this question, as a new revision. Every revision is born
    /// complete in both official languages, so there is nothing left to check
    /// here beyond whether the question itself is still live.
    /// </summary>
    public QuestionRevision Activate(DateTimeOffset at) =>
        ReviseInternal(
            Type, CurrentRevision.LabelEn, CurrentRevision.LabelFr, CurrentRevision.HelpTextEn, CurrentRevision.HelpTextFr,
            CurrentRevision.PlaceholderEn, CurrentRevision.PlaceholderFr, CurrentRevision.IsPrivate, isActive: true,
            CurrentRevision.DisplayOrder, CurrentRevision.SectionKey, CurrentOptions(), at);

    /// <summary>Stops asking this question, as a new revision. Every answer
    /// already given to it is kept.</summary>
    public QuestionRevision Deactivate(DateTimeOffset at)
    {
        if (IsSystem)
        {
            throw new DomainRuleViolationException(
                $"'{Key}' gates publication. A form that does not ask it cannot publish anything.");
        }

        return ReviseInternal(
            Type, CurrentRevision.LabelEn, CurrentRevision.LabelFr, CurrentRevision.HelpTextEn, CurrentRevision.HelpTextFr,
            CurrentRevision.PlaceholderEn, CurrentRevision.PlaceholderFr, CurrentRevision.IsPrivate, isActive: false,
            CurrentRevision.DisplayOrder, CurrentRevision.SectionKey, CurrentOptions(), at);
    }

    /// <summary>
    /// Retires the question. A soft delete, always: answers to it are part of a
    /// real report and are never removed with it.
    /// </summary>
    public void Delete(DateTimeOffset at)
    {
        if (IsSystem)
        {
            throw new DomainRuleViolationException(
                $"'{Key}' is publication consent and cannot be deleted. Nothing may be published without it.");
        }

        if (Deleted is not null)
        {
            return;
        }

        Deleted = at;
    }

    private QuestionRevision ReviseInternal(
        QuestionType type,
        string labelEn,
        string labelFr,
        string? helpTextEn,
        string? helpTextFr,
        string? placeholderEn,
        string? placeholderFr,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        string? sectionKey,
        IReadOnlyList<QuestionOptionInput> options,
        DateTimeOffset at)
    {
        EnsureNotDeleted();

        var revision = QuestionRevision.Create(
            Id, CurrentRevision.RevisionNumber + 1, type, labelEn, labelFr, helpTextEn, helpTextFr,
            placeholderEn, placeholderFr, IsSystem, isPrivate, isActive, displayOrder, sectionKey, options, at);
        _revisions.Add(revision);
        return revision;
    }

    /// <summary>The current revision's option set, in order, as input for a new revision.</summary>
    private List<QuestionOptionInput> CurrentOptions() =>
        CurrentRevision.Options.Select(option => new QuestionOptionInput(option.Code, option.LabelEn, option.LabelFr)).ToList();

    private void EnsureNotDeleted()
    {
        if (Deleted is not null)
        {
            throw new DomainRuleViolationException($"'{Key}' was deleted and cannot be changed.");
        }
    }
}
