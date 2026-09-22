namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     A question on the occurrence form. The question set is data — an
///     administrator adds, rewords, retypes, reorders, and removes questions without
///     a deploy — so this is the aggregate root of the question bank.
/// </summary>
/// <remarks>
///     <para>
///         Exactly one question is a <b>system question</b>: publication consent. It
///         cannot be deleted, deactivated, retyped, or rekeyed, because it is the gate
///         every publication path checks and there is no defined behaviour without it.
///         Its wording is still editable, and it reorders like any other.
///     </para>
///     <para>
///         Nothing but consent projects onto a typed property of
///         <see cref="Reporting.Report" /> — the admin review DTO reads exact asked
///         questions and answers directly. See <c>docs/data-and-persistence.md</c>.
///     </para>
///     <para>
///         Order, privacy, active state, system state, required state, and the
///         complete ordered option set all live on <see cref="QuestionRevision" />, not
///         here — a referenced revision has to preserve the complete question exactly
///         as it was shown, and none of those facts can be reconstructed from the
///         current state of a mutable question row. Every read here that looks
///         question-scoped (<see cref="IsPrivate" />, <see cref="DisplayOrder" />,
///         <see cref="IsActive" />) reads through to
///         <see cref="CurrentRevision" />, and every change to one of them is made by
///         creating a new revision. See
///         <c>features/question-bank-and-form/question-bank-and-form.feature</c>.
///     </para>
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
    ///     Whether answers are private redaction context rather than facts eligible
    ///     for the summary, on the current revision. See ADR-0038 and the class
    ///     remarks: this is a revision field, changed by creating a new revision.
    /// </summary>
    public bool IsPrivate => CurrentRevision.IsPrivate;

    /// <summary>
    ///     Whether a reporter must answer this question today. Authored —
    ///     see <see cref="QuestionRevision.IsRequired" /> and ADR-0061.
    /// </summary>
    public bool IsRequired => CurrentRevision.IsRequired;

    /// <summary>The question this one is conditional on today, if any.</summary>
    public TinyId? DependsOnQuestionId => CurrentRevision.DependsOnQuestionId;

    /// <summary>
    ///     The required option a single-select parent must be answered
    ///     with today, if any. See ADR-0074.
    /// </summary>
    public string? DependsOnOptionCode => CurrentRevision.DependsOnOptionCode;

    /// <summary>
    ///     Where this question sits on the form today. Not versioned
    ///     independently — see the class remarks.
    /// </summary>
    public int DisplayOrder => CurrentRevision.DisplayOrder;

    /// <summary>
    ///     Whether the public form asks this question today. Always false
    ///     once the question itself is deleted, regardless of what the current
    ///     revision says.
    /// </summary>
    public bool IsActive => Deleted is null && CurrentRevision.IsActive;

    /// <summary>When this question was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When this question was retired, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>Every revision, oldest first. Answers reference one of these.</summary>
    public IReadOnlyList<QuestionRevision> Revisions => _revisions;

    /// <summary>
    ///     The revision the form asks today. Selected by the highest revision
    ///     number rather than list position: EF Core does not guarantee the order
    ///     of a loaded navigation collection, so the last element of
    ///     <see cref="_revisions" /> can be an arbitrary historical row after a
    ///     load.
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
        bool isRequired = false,
        bool isPrivate = true,
        bool isActive = false,
        int displayOrder = 0,
        TinyId? dependsOnQuestionId = null,
        string? dependsOnOptionCode = null,
        TinyId? optionSetId = null,
        IReadOnlyList<QuestionOptionInput>? options = null)
    {
        return Create(
            key, type, labelEn, labelFr, at, false, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
            role, isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnOptionCode, optionSetId,
            options);
    }

    /// <summary>
    ///     Creates the publication-consent question. The only question the system
    ///     refuses to lose, and the only caller of this method.
    /// </summary>
    public static Question CreateConsentPublish(
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        string? helpTextEn = null,
        string? helpTextFr = null,
        int displayOrder = 0)
    {
        return Create(
            QuestionKey.ConsentPublish,
            QuestionType.YesNo,
            labelEn,
            labelFr,
            at,
            true,
            helpTextEn,
            helpTextFr,
            null,
            null,
            QuestionRole.ConsentPublish,
            true,
            true,
            true,
            displayOrder,
            null,
            null,
            null,
            null);
    }

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
        bool isRequired,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        TinyId? dependsOnQuestionId,
        string? dependsOnOptionCode,
        TinyId? optionSetId,
        IReadOnlyList<QuestionOptionInput>? options)
    {
        var question = new Question(key, isSystem, role, at);
        question._revisions.Add(
            QuestionRevision.Create(
                question.Id, 1, type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
                isSystem, isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnOptionCode,
                optionSetId, options ?? [], at));
        return question;
    }

    /// <summary>
    ///     Rewords, retypes, reorders, moves, reclassifies, activates, deactivates,
    ///     or changes the options of this question, producing one new complete
    ///     bilingual revision. Answers already given keep pointing at the revision
    ///     they were given under, so an old report still shows exactly what it was
    ///     actually asked, including the order, privacy, and active state
    ///     in force at the time.
    /// </summary>
    public QuestionRevision Revise(
        QuestionType type,
        string labelEn,
        string labelFr,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        DateTimeOffset at,
        string? helpTextEn = null,
        string? helpTextFr = null,
        string? placeholderEn = null,
        string? placeholderFr = null,
        bool isRequired = false,
        TinyId? dependsOnQuestionId = null,
        string? dependsOnOptionCode = null,
        TinyId? optionSetId = null,
        IReadOnlyList<QuestionOptionInput>? options = null)
    {
        if (IsSystem && type != Type)
            throw new DomainRuleViolationException(
                $"'{Key}' is a system question. Its wording can change; its type cannot.");

        return ReviseInternal(
            new RevisionDraft(
                type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
                isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnOptionCode, optionSetId,
                options ?? []),
            at);
    }

    /// <summary>
    ///     Applies an administrator's edit and returns the question that is live
    ///     afterwards — this one, revised, or a new one that replaces it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is where ADR-0071 lives. While nothing has answered the question, an
    ///         edit is a revision and the question keeps its identity. Once an answer
    ///         exists, a reworded question is a different question: this one is retired
    ///         and a new one takes its place, carrying the same stable key, so every
    ///         answer already given keeps pointing at the wording it was given under.
    ///     </para>
    ///     <para>
    ///         Publication consent never forks. It cannot be deleted, so it revises in
    ///         place however many answers it has.
    ///     </para>
    ///     <para>
    ///         Whether the question has been answered is a fact about reports, which
    ///         this aggregate cannot see, so the caller reads it and passes it in. It
    ///         must count answers on deleted reports too — a deleted report is still a
    ///         record of what somebody was asked.
    ///     </para>
    /// </remarks>
    public Question ApplyEdit(
        bool hasBeenAnswered,
        QuestionType type,
        string labelEn,
        string labelFr,
        bool isPrivate,
        bool isActive,
        int displayOrder,
        DateTimeOffset at,
        string? helpTextEn = null,
        string? helpTextFr = null,
        string? placeholderEn = null,
        string? placeholderFr = null,
        bool isRequired = false,
        TinyId? dependsOnQuestionId = null,
        string? dependsOnOptionCode = null,
        TinyId? optionSetId = null,
        IReadOnlyList<QuestionOptionInput>? options = null)
    {
        var draft = new RevisionDraft(
            type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
            isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnOptionCode, optionSetId,
            options ?? []);

        if (!ForksWhenEdited(hasBeenAnswered))
        {
            Revise(
                type, labelEn, labelFr, isPrivate, isActive, displayOrder, at,
                helpTextEn, helpTextFr, placeholderEn, placeholderFr, isRequired, dependsOnQuestionId,
                dependsOnOptionCode, optionSetId, options);
            return this;
        }

        return Fork(draft, at);
    }

    /// <summary>
    ///     Whether an edit would replace this question rather than revise it. False
    ///     for a question nobody has answered, and false for publication consent
    ///     however many answers it has.
    /// </summary>
    public bool ForksWhenEdited(bool hasBeenAnswered)
    {
        return hasBeenAnswered && !IsSystem;
    }

    /// <summary>
    ///     Moves the question on the form, as a new revision. Every other
    ///     field is carried forward unchanged from <see cref="CurrentRevision" />.
    /// </summary>
    public QuestionRevision Reorder(int displayOrder, DateTimeOffset at)
    {
        return ReviseInternal(CurrentDraft() with { DisplayOrder = displayOrder }, at);
    }

    /// <summary>
    ///     Makes the question conditional on another question, or unconditional
    ///     again, as a new revision. <paramref name="dependsOnOptionCode" /> names
    ///     the required option when the parent is single-select, and must be null
    ///     when it is yes/no or when there is no parent. Whether the named
    ///     question is a type that can be a parent at all — and, for
    ///     single-select, whether it currently offers the named option — is
    ///     checked by <see cref="QuestionDependencies" />, which can see the rest
    ///     of the bank. See ADR-0060, ADR-0074.
    /// </summary>
    public QuestionRevision DependOn(TinyId? dependsOnQuestionId, string? dependsOnOptionCode, DateTimeOffset at)
    {
        return ReviseInternal(
            CurrentDraft() with { DependsOnQuestionId = dependsOnQuestionId, DependsOnOptionCode = dependsOnOptionCode },
            at);
    }

    /// <summary>
    ///     Reassigns what logic reads this answer for. A role lives on at
    ///     most one active question at a time; that is enforced by the question bank,
    ///     not here.
    /// </summary>
    public void AssignRole(QuestionRole role)
    {
        EnsureNotDeleted();

        if (IsSystem && role != QuestionRole.ConsentPublish) throw new DomainRuleViolationException($"'{Key}' carries publication consent and cannot give up that role.");

        Role = role;
    }

    /// <summary>
    ///     Starts asking this question, as a new revision. Every revision is born
    ///     complete in both official languages, so there is nothing left to check
    ///     here beyond whether the question itself is still live.
    /// </summary>
    public QuestionRevision Activate(DateTimeOffset at)
    {
        return ReviseInternal(CurrentDraft() with { IsActive = true }, at);
    }

    /// <summary>
    ///     Stops asking this question, as a new revision. Every answer
    ///     already given to it is kept.
    /// </summary>
    public QuestionRevision Deactivate(DateTimeOffset at)
    {
        if (IsSystem)
            throw new DomainRuleViolationException(
                $"'{Key}' gates publication. A form that does not ask it cannot publish anything.");

        return ReviseInternal(CurrentDraft() with { IsActive = false }, at);
    }

    /// <summary>
    ///     Retires the question. A soft delete, always: answers to it are part of a
    ///     real report and are never removed with it.
    /// </summary>
    /// <remarks>
    ///     There is no undelete, deliberately. A retired question may already have
    ///     answers frozen against its retirement, and a row that can come back is
    ///     not frozen (ADR-0071). An administrator who wants it again authors it
    ///     again.
    /// </remarks>
    public void Delete(DateTimeOffset at)
    {
        if (IsSystem)
            throw new DomainRuleViolationException(
                $"'{Key}' is publication consent and cannot be deleted. Nothing may be published without it.");

        if (Deleted is not null) return;

        Deleted = at;
    }

    /// <summary>
    ///     Retires this question and returns its replacement, carrying the same
    ///     stable key and starting a fresh revision chain. The key is shared with
    ///     every retired question in the chain and is unique only among live ones,
    ///     which is what the partial unique index enforces (ADR-0071).
    /// </summary>
    private Question Fork(RevisionDraft draft, DateTimeOffset at)
    {
        EnsureNotDeleted();

        var replacement = new Question(Key, false, Role, at);
        replacement._revisions.Add(
            QuestionRevision.Create(
                replacement.Id, 1, draft.Type, draft.LabelEn, draft.LabelFr,
                draft.HelpTextEn, draft.HelpTextFr, draft.PlaceholderEn, draft.PlaceholderFr,
                false, draft.IsRequired, draft.IsPrivate, draft.IsActive, draft.DisplayOrder,
                draft.DependsOnQuestionId, draft.DependsOnOptionCode, draft.OptionSetId, draft.Options, at));

        Delete(at);
        return replacement;
    }

    private QuestionRevision ReviseInternal(RevisionDraft draft, DateTimeOffset at)
    {
        EnsureNotDeleted();

        var revision = QuestionRevision.Create(
            Id, CurrentRevision.RevisionNumber + 1, draft.Type, draft.LabelEn, draft.LabelFr,
            draft.HelpTextEn, draft.HelpTextFr, draft.PlaceholderEn, draft.PlaceholderFr,
            IsSystem, draft.IsRequired, draft.IsPrivate, draft.IsActive, draft.DisplayOrder,
            draft.DependsOnQuestionId, draft.DependsOnOptionCode, draft.OptionSetId, draft.Options, at);
        _revisions.Add(revision);
        return revision;
    }

    /// <summary>
    ///     The current revision, field for field, as input for the next one. A
    ///     change that touches one field says so with a <c>with</c> expression, so
    ///     adding a revision field cannot quietly drop it from the five methods
    ///     that carry everything else forward.
    /// </summary>
    private RevisionDraft CurrentDraft()
    {
        var current = CurrentRevision;

        return new RevisionDraft(
            current.Type, current.LabelEn, current.LabelFr, current.HelpTextEn, current.HelpTextFr,
            current.PlaceholderEn, current.PlaceholderFr, current.IsRequired, current.IsPrivate, current.IsActive,
            current.DisplayOrder, current.DependsOnQuestionId, current.DependsOnOptionCode, current.OptionSetId,
            CurrentOptions());
    }

    /// <summary>The current revision's option set, in order, as input for a new revision.</summary>
    private List<QuestionOptionInput> CurrentOptions()
    {
        return
        [
            .. CurrentRevision.Options
                .OrderBy(option => option.DisplayOrder)
                .Select(option => new QuestionOptionInput(option.Code, option.LabelEn, option.LabelFr, option.SourceItemId))
        ];
    }

    private void EnsureNotDeleted()
    {
        if (Deleted is not null) throw new DomainRuleViolationException($"'{Key}' was deleted and cannot be changed.");
    }

    /// <summary>
    ///     Every field of a revision-to-be. Exists so that a change to one field is
    ///     written as one <c>with</c> expression rather than as a positional
    ///     argument list that a reader has to count.
    /// </summary>
    private sealed record RevisionDraft(
        QuestionType Type,
        string LabelEn,
        string LabelFr,
        string? HelpTextEn,
        string? HelpTextFr,
        string? PlaceholderEn,
        string? PlaceholderFr,
        bool IsRequired,
        bool IsPrivate,
        bool IsActive,
        int DisplayOrder,
        TinyId? DependsOnQuestionId,
        string? DependsOnOptionCode,
        TinyId? OptionSetId,
        IReadOnlyList<QuestionOptionInput> Options);
}
