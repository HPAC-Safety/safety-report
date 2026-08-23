using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>A complete immutable revision of one form question.</summary>
public sealed class Question
{
    private readonly List<QuestionOption> _options = [];

#pragma warning disable CS8618 // EF Core sets every mapped property.
    private Question()
    {
    }
#pragma warning restore CS8618

    private Question(
        string key,
        int revision,
        QuestionType type,
        string labelEn,
        string labelFr,
        DateTimeOffset createdAt,
        bool isPrivate,
        int sortOrder,
        bool isActive,
        string? helpEn,
        string? helpFr,
        string? sectionKey,
        TinyId? supersedesQuestionId,
        bool isSystem,
        IReadOnlyList<QuestionOptionDefinition>? options)
    {
        Id = TinyId.New();
        Key = QuestionKey.Normalize(key);
        Revision = revision;
        Type = type;
        LabelEn = NotBlank(labelEn, "English");
        LabelFr = NotBlank(labelFr, "French");
        HelpEn = NullIfBlank(helpEn);
        HelpFr = NullIfBlank(helpFr);
        IsPrivate = isPrivate;
        SortOrder = sortOrder;
        IsActive = isActive;
        SectionKey = sectionKey is null ? null : QuestionKey.Normalize(sectionKey);
        SupersedesQuestionId = supersedesQuestionId;
        IsSystem = isSystem;
        IsRequired = isSystem;
        CreatedAt = createdAt;

        AddOptions(options ?? []);
    }

    /// <summary>Unique id for this exact revision.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Stable identity shared by every revision.</summary>
    public string Key { get; private init; }

    /// <summary>Monotonic revision number within the stable key.</summary>
    public int Revision { get; private init; }

    /// <summary>Input shape.</summary>
    public QuestionType Type { get; private init; }

    /// <summary>English label.</summary>
    public string LabelEn { get; private init; }

    /// <summary>French label.</summary>
    public string LabelFr { get; private init; }

    /// <summary>Optional English help text.</summary>
    public string? HelpEn { get; private init; }

    /// <summary>Optional French help text.</summary>
    public string? HelpFr { get; private init; }

    /// <summary>True when the answer belongs in model-only private context.</summary>
    public bool IsPrivate { get; private init; }

    /// <summary>Form order for this revision.</summary>
    public int SortOrder { get; private init; }

    /// <summary>Whether this revision is eligible for latest-live selection.</summary>
    public bool IsActive { get; private init; }

    /// <summary>Optional section key.</summary>
    public string? SectionKey { get; private init; }

    /// <summary>The prior immutable revision, when one exists.</summary>
    public TinyId? SupersedesQuestionId { get; private init; }

    /// <summary>True only for publication consent.</summary>
    public bool IsSystem { get; private init; }

    /// <summary>True only for publication consent.</summary>
    public bool IsRequired { get; private init; }

    /// <summary>Creation time for this revision.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Bilingual options in sort order.</summary>
    public IReadOnlyList<QuestionOption> Options => _options;

    /// <summary>The fixed values accepted by a yes/no question.</summary>
    public static IReadOnlyList<string> YesNoCodes => QuestionOption.YesNoCodes;

    /// <summary>True when this question takes option codes.</summary>
    public bool ExpectsOptions =>
        Type is QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.YesNo;

    /// <summary>True when this question collects no answer.</summary>
    public bool CollectsNoAnswer => Type is QuestionType.Statement or QuestionType.Group;

    /// <summary>Creates revision one of an optional ordinary question.</summary>
    public static Question Create(
        string key,
        QuestionType type,
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        bool isPrivate = true,
        int sortOrder = 0,
        bool isActive = false,
        string? helpEn = null,
        string? helpFr = null,
        string? sectionKey = null,
        IReadOnlyList<QuestionOptionDefinition>? options = null)
    {
        if (QuestionKey.Normalize(key) == QuestionKey.ConsentPublish)
        {
            throw new DomainRuleViolationException("Publication consent must be created through its system factory.");
        }

        return new Question(
            key, 1, type, labelEn, labelFr, at, isPrivate, sortOrder, isActive,
            helpEn, helpFr, sectionKey, supersedesQuestionId: null, isSystem: false, options);
    }

    /// <summary>Creates the one required publication-consent revision.</summary>
    public static Question CreateConsentPublish(
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        int sortOrder = 0,
        string? helpEn = null,
        string? helpFr = null) =>
        new(
            QuestionKey.ConsentPublish,
            1,
            QuestionType.YesNo,
            labelEn,
            labelFr,
            at,
            isPrivate: true,
            sortOrder,
            isActive: true,
            helpEn,
            helpFr,
            sectionKey: null,
            supersedesQuestionId: null,
            isSystem: true,
            options: null);

    /// <summary>Returns a new complete revision and leaves this row unchanged.</summary>
    public Question Revise(
        QuestionType type,
        string labelEn,
        string labelFr,
        DateTimeOffset at,
        bool isPrivate,
        int sortOrder,
        bool isActive,
        string? helpEn = null,
        string? helpFr = null,
        string? sectionKey = null,
        IReadOnlyList<QuestionOptionDefinition>? options = null)
    {
        if (IsSystem && (type != QuestionType.YesNo || !isPrivate || !isActive))
        {
            throw new DomainRuleViolationException(
                "Publication consent must remain active, private, required, and yes/no.");
        }

        return new Question(
            Key,
            Revision + 1,
            type,
            labelEn,
            labelFr,
            at,
            isPrivate,
            sortOrder,
            isActive,
            helpEn,
            helpFr,
            sectionKey,
            Id,
            IsSystem,
            options);
    }

    /// <summary>Whether this revision accepts an option code.</summary>
    public bool Accepts(string code) =>
        Type == QuestionType.YesNo
            ? QuestionOption.YesNoCodes.Contains(code, StringComparer.OrdinalIgnoreCase)
            : _options.Exists(option => option.Code == QuestionKey.Normalize(code));

    private void AddOptions(IReadOnlyList<QuestionOptionDefinition> definitions)
    {
        if (Type == QuestionType.YesNo && definitions.Count > 0)
        {
            throw new DomainRuleViolationException("A yes/no question has the fixed options yes and no.");
        }

        if (!ExpectsOptions && definitions.Count > 0)
        {
            throw new DomainRuleViolationException($"A {Type} question cannot have options.");
        }

        if (Type is QuestionType.SingleSelect or QuestionType.MultiSelect && definitions.Count == 0)
        {
            throw new DomainRuleViolationException($"A {Type} question needs at least one option.");
        }

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var option = QuestionOption.Create(Id, definition, index);

            if (_options.Exists(existing => existing.Code == option.Code))
            {
                throw new DomainRuleViolationException($"Option code '{option.Code}' appears more than once.");
            }

            _options.Add(option);
        }
    }

    private static string NotBlank(string value, string language) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new DomainRuleViolationException($"A question needs a {language} label.")
            : value.Trim();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>One bilingual option supplied while creating a question revision.</summary>
public sealed record QuestionOptionDefinition(string Code, string LabelEn, string LabelFr);
