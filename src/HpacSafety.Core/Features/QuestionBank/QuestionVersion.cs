
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// A question exactly as it was asked at a point in time: its type, its wording
/// in both locales, and its option set. Immutable once created — rewording,
/// retyping, or changing the options produces a new version, so a report filed
/// last year still renders the question it was actually answering.
/// </summary>
/// <remarks>
/// Reordering and activation are deliberately <b>not</b> versioned. Neither
/// changes what an answer means.
/// </remarks>
public class QuestionVersion
{
    private readonly List<QuestionOption> _options = [];
    private readonly List<QuestionTranslation> _translations = [];

    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
    private QuestionVersion()
    {
    }

    private QuestionVersion(Guid questionId, int versionNumber, QuestionType type, bool isRequired, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        QuestionId = questionId;
        VersionNumber = versionNumber;
        Type = type;
        IsRequired = isRequired;
        CreatedAt = at;
    }

    /// <summary>Surrogate key. Answers reference this, never the question row.</summary>
    public Guid Id { get; private init; }

    /// <summary>The question this is a version of.</summary>
    public Guid QuestionId { get; private init; }

    /// <summary>Increments by one per revision, starting at 1.</summary>
    public int VersionNumber { get; private init; }

    /// <summary>What this version asks for.</summary>
    public QuestionType Type { get; private init; }

    /// <summary>Whether a reporter must answer before submitting.</summary>
    public bool IsRequired { get; private init; }

    /// <summary>When this version was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>The choices, for select-style types. Empty otherwise.</summary>
    public IReadOnlyCollection<QuestionOption> Options => _options;

    /// <summary>The wording, one row per locale.</summary>
    public IReadOnlyCollection<QuestionTranslation> Translations => _translations;

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

    /// <summary>The locale a human actually wrote this version in.</summary>
    public QuestionTranslation SourceTranslation =>
        _translations.SingleOrDefault(t => t.IsSource)
        ?? throw new DomainRuleViolationException("A question version must have exactly one source translation.");

    /// <summary>True once wording exists in every official locale.</summary>
    public bool IsFullyTranslated =>
        Locale.All.All(locale => Translation(locale) is not null)
        && _options.TrueForAll(option => Locale.All.All(locale => option.Translation(locale) is not null));

    /// <summary>The locales this version is still missing.</summary>
    public IReadOnlyList<Locale> MissingLocales =>
        [.. Locale.All.Where(locale => Translation(locale) is null)];

    internal static QuestionVersion Create(
        Guid questionId,
        int versionNumber,
        QuestionType type,
        bool isRequired,
        Locale sourceLocale,
        string label,
        string? helpText,
        string? placeholder,
        DateTimeOffset at)
    {
        var version = new QuestionVersion(questionId, versionNumber, type, isRequired, at);
        version._translations.Add(
            QuestionTranslation.Authored(version.Id, sourceLocale, label, helpText, placeholder, at));
        return version;
    }

    /// <summary>The wording in one locale, if it exists yet.</summary>
    public QuestionTranslation? Translation(Locale locale) =>
        _translations.SingleOrDefault(t => t.Locale == locale);

    /// <summary>
    /// Attaches the generated counterpart produced by <c>ITranslator</c>. A
    /// question is authored in one language and translated into the other, in
    /// both directions.
    /// </summary>
    public QuestionTranslation AttachTranslation(
        Locale locale, string label, string? helpText, string? placeholder, DateTimeOffset at)
    {
        if (Translation(locale) is not null)
        {
            throw new DomainRuleViolationException($"This question version already has {locale} wording.");
        }

        var translation = QuestionTranslation.Generated(Id, locale, label, helpText, placeholder, at);
        _translations.Add(translation);
        return translation;
    }

    /// <summary>Adds a choice, in the locale this version was authored in.</summary>
    public QuestionOption AddOption(string code, string label, DateTimeOffset at)
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

        var option = QuestionOption.Create(Id, normalized, _options.Count, SourceTranslation.Locale, label, at);
        _options.Add(option);
        return option;
    }

    /// <summary>Finds a choice by its invariant code. Null for
    /// <see cref="QuestionType.YesNo"/>, whose two answers are not option rows —
    /// their labels are ordinary UI chrome and live in <c>locales/</c>.</summary>
    public QuestionOption? Option(string code) =>
        _options.Find(o => o.Code == code);

    /// <summary>Whether this version accepts an answer code.</summary>
    public bool Accepts(string code) =>
        Type == QuestionType.YesNo
            ? YesNoCodes.Contains(code, StringComparer.OrdinalIgnoreCase)
            : Option(code) is not null;
}
