using HpacSafety.Core.Enums;
using HpacSafety.Core.Values;

namespace HpacSafety.Core.Questions;

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
/// Everything else is ordinary data, including the injury, date, province, and
/// aircraft questions. Where logic needs to find one of them it reads
/// <see cref="Role"/>, which an administrator may move or clear.
/// </para>
/// </remarks>
public class Question
{
    private readonly List<QuestionVersion> _versions = [];

    private Question(string key, bool isSystem, QuestionRole role, SensitivityTier sensitivity, int displayOrder, string? sectionKey, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        Key = QuestionKey.Normalize(key);
        IsSystem = isSystem;
        Role = role;
        Sensitivity = sensitivity;
        DisplayOrder = displayOrder;
        SectionKey = sectionKey is null ? null : QuestionKey.Normalize(sectionKey);
        CreatedAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>Stable invariant identity, used by exports and integrations.</summary>
    public string Key { get; private init; }

    /// <summary>True only for publication consent.</summary>
    public bool IsSystem { get; private init; }

    /// <summary>What downstream logic reads this answer for, if anything.</summary>
    public QuestionRole Role { get; private set; }

    /// <summary>
    /// The tier this question's answers live at. Restricted by default: a
    /// question added tomorrow is treated as personal information until someone
    /// decides otherwise. See docs/data-handling.md.
    /// </summary>
    public SensitivityTier Sensitivity { get; private set; }

    /// <summary>Where this question sits on the form. Not versioned.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>The section this question is grouped under, if any.</summary>
    public string? SectionKey { get; private set; }

    /// <summary>Whether the public form asks this question today.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When this question was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When this question was retired, if it was.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Every version, oldest first. Answers reference one of these.</summary>
    public IReadOnlyList<QuestionVersion> Versions => _versions;

    /// <summary>The version the form asks today.</summary>
    public QuestionVersion CurrentVersion =>
        _versions.Count > 0
            ? _versions[^1]
            : throw new DomainRuleViolationException("A question always has at least one version.");

    /// <summary>What this question currently asks for.</summary>
    public QuestionType Type => CurrentVersion.Type;

    /// <summary>Creates an ordinary question, authored in one locale.</summary>
    public static Question Create(
        string key,
        QuestionType type,
        Locale sourceLocale,
        string label,
        DateTimeOffset at,
        string? helpText = null,
        string? placeholder = null,
        bool isRequired = false,
        QuestionRole role = QuestionRole.None,
        SensitivityTier sensitivity = SensitivityTier.Restricted,
        int displayOrder = 0,
        string? sectionKey = null) =>
        Create(key, type, sourceLocale, label, at, isSystem: false, helpText, placeholder, isRequired, role, sensitivity, displayOrder, sectionKey);

    /// <summary>
    /// Creates the publication-consent question. The only question the system
    /// refuses to lose, and the only caller of this method.
    /// </summary>
    public static Question CreateConsentPublish(
        Locale sourceLocale,
        string label,
        DateTimeOffset at,
        string? helpText = null,
        int displayOrder = 0) =>
        Create(
            QuestionKey.ConsentPublish,
            QuestionType.YesNo,
            sourceLocale,
            label,
            at,
            isSystem: true,
            helpText,
            placeholder: null,
            isRequired: true,
            QuestionRole.ConsentPublish,
            SensitivityTier.Internal,
            displayOrder,
            sectionKey: null);

    private static Question Create(
        string key,
        QuestionType type,
        Locale sourceLocale,
        string label,
        DateTimeOffset at,
        bool isSystem,
        string? helpText,
        string? placeholder,
        bool isRequired,
        QuestionRole role,
        SensitivityTier sensitivity,
        int displayOrder,
        string? sectionKey)
    {
        var question = new Question(key, isSystem, role, sensitivity, displayOrder, sectionKey, at);
        question._versions.Add(
            QuestionVersion.Create(question.Id, 1, type, isRequired, sourceLocale, label, helpText, placeholder, at));
        return question;
    }

    /// <summary>
    /// Rewords or retypes the question, producing a new version. Answers already
    /// given keep pointing at the version they were given under, so an old report
    /// still shows the wording it was actually asked with.
    /// </summary>
    public QuestionVersion Revise(
        QuestionType type,
        bool isRequired,
        Locale sourceLocale,
        string label,
        DateTimeOffset at,
        string? helpText = null,
        string? placeholder = null)
    {
        EnsureNotDeleted();

        if (IsSystem && type != Type)
        {
            throw new DomainRuleViolationException(
                $"'{Key}' is a system question. Its wording can change; its type cannot.");
        }

        var version = QuestionVersion.Create(
            Id, CurrentVersion.VersionNumber + 1, type, isRequired, sourceLocale, label, helpText, placeholder, at);
        _versions.Add(version);
        return version;
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

    /// <summary>Changes the tier this question's answers are handled at.</summary>
    public void Reclassify(SensitivityTier sensitivity)
    {
        EnsureNotDeleted();
        Sensitivity = sensitivity;
    }

    /// <summary>
    /// Starts asking this question. Refused while either official language is
    /// missing: a reporter is never shown a form that is only half translated. A
    /// machine-translated counterpart is acceptable; an absent one is not.
    /// </summary>
    public void Activate()
    {
        EnsureNotDeleted();

        if (!CurrentVersion.IsFullyTranslated)
        {
            var missing = string.Join(", ", CurrentVersion.MissingLocales);
            throw new DomainRuleViolationException(
                $"'{Key}' has no wording in {(missing.Length > 0 ? missing : "every locale for its options")} and cannot be asked.");
        }

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

        if (DeletedAt is not null)
        {
            return;
        }

        IsActive = false;
        DeletedAt = at;
    }

    private void EnsureNotDeleted()
    {
        if (DeletedAt is not null)
        {
            throw new DomainRuleViolationException($"'{Key}' was deleted and cannot be changed.");
        }
    }
}
