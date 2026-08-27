
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
/// Everything else is ordinary data. Nothing but consent projects onto a typed
/// property of <see cref="Reporting.Report"/> — the admin review DTO reads exact
/// asked questions and answers directly. See <c>docs/data-and-persistence.md</c>.
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

    private Question(string key, bool isSystem, QuestionRole role, bool isPrivate, int displayOrder, string? sectionKey, DateTimeOffset at)
    {
        Id = TinyId.New();
        Key = QuestionKey.Normalize(key);
        IsSystem = isSystem;
        Role = role;
        IsPrivate = isPrivate;
        DisplayOrder = displayOrder;
        SectionKey = sectionKey is null ? null : QuestionKey.Normalize(sectionKey);
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
    /// for the summary. Private by default, and immutable after creation: an
    /// administrator must retire this question and create a new one to change
    /// that contract. See ADR-0038.
    /// </summary>
    public bool IsPrivate { get; private init; }

    /// <summary>Where this question sits on the form. Not versioned.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>The section this question is grouped under, if any.</summary>
    public string? SectionKey { get; private set; }

    /// <summary>Whether the public form asks this question today.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When this question was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When this question was retired, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>Every revision, oldest first. Answers reference one of these.</summary>
    public IReadOnlyList<QuestionRevision> Revisions => _revisions;

    /// <summary>The revision the form asks today.</summary>
    public QuestionRevision CurrentRevision =>
        _revisions.Count > 0
            ? _revisions[^1]
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
        bool isRequired = false,
        QuestionRole role = QuestionRole.None,
        bool isPrivate = true,
        int displayOrder = 0,
        string? sectionKey = null) =>
        Create(
            key, type, labelEn, labelFr, at, isSystem: false, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
            isRequired, role, isPrivate, displayOrder, sectionKey);

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
            isRequired: true,
            QuestionRole.ConsentPublish,
            isPrivate: true,
            displayOrder,
            sectionKey: null);

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
        bool isRequired,
        QuestionRole role,
        bool isPrivate,
        int displayOrder,
        string? sectionKey)
    {
        var question = new Question(key, isSystem, role, isPrivate, displayOrder, sectionKey, at);
        question._revisions.Add(
            QuestionRevision.Create(
                question.Id, 1, type, isRequired, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr, at));
        return question;
    }

    /// <summary>
    /// Rewords or retypes the question, producing a new complete bilingual
    /// revision. Answers already given keep pointing at the revision they were
    /// given under, so an old report still shows the wording it was actually
    /// asked with.
    /// </summary>
    public QuestionRevision Revise(
        QuestionType type,
        bool isRequired,
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        string? helpTextEn = null,
        string? helpTextFr = null,
        string? placeholderEn = null,
        string? placeholderFr = null)
    {
        EnsureNotDeleted();

        if (IsSystem && type != Type)
        {
            throw new DomainRuleViolationException(
                $"'{Key}' is a system question. Its wording can change; its type cannot.");
        }

        var revision = QuestionRevision.Create(
            Id, CurrentRevision.RevisionNumber + 1, type, isRequired, labelEn, labelFr,
            helpTextEn, helpTextFr, placeholderEn, placeholderFr, at);
        _revisions.Add(revision);
        return revision;
    }

    /// <summary>Moves the question on the form. Not a versioned change — moving a
    /// question does not alter what any answer means.</summary>
    public void Reorder(int displayOrder)
    {
        EnsureNotDeleted();
        DisplayOrder = displayOrder;
    }

    /// <summary>Moves the question into a section, or out of one.</summary>
    public void MoveToSection(string? sectionKey)
    {
        EnsureNotDeleted();
        SectionKey = sectionKey is null ? null : QuestionKey.Normalize(sectionKey);
    }

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
    /// Starts asking this question. Every revision is born complete in both
    /// official languages, so there is nothing left to check here beyond
    /// whether the question itself is still live.
    /// </summary>
    public void Activate()
    {
        EnsureNotDeleted();
        IsActive = true;
    }

    /// <summary>Stops asking this question. Every answer already given to it is
    /// kept.</summary>
    public void Deactivate()
    {
        EnsureNotDeleted();

        if (IsSystem)
        {
            throw new DomainRuleViolationException(
                $"'{Key}' gates publication. A form that does not ask it cannot publish anything.");
        }

        IsActive = false;
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

        IsActive = false;
        Deleted = at;
    }

    private void EnsureNotDeleted()
    {
        if (Deleted is not null)
        {
            throw new DomainRuleViolationException($"'{Key}' was deleted and cannot be changed.");
        }
    }
}
