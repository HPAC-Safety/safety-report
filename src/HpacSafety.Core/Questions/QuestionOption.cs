using HpacSafety.Core.Values;

namespace HpacSafety.Core.Questions;

/// <summary>
/// One choice on a select-style question. The <see cref="Code"/> is invariant
/// and never changes — it is what every historical answer points at, so a
/// rename is a translation change and nothing more.
/// </summary>
public class QuestionOption
{
    private readonly List<QuestionOptionTranslation> _translations = [];

    private QuestionOption(Guid questionVersionId, string code, int displayOrder)
    {
        Id = Guid.NewGuid();
        QuestionVersionId = questionVersionId;
        Code = QuestionKey.Normalize(code);
        DisplayOrder = displayOrder;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The version this option belongs to.</summary>
    public Guid QuestionVersionId { get; private init; }

    /// <summary>The invariant code stored against an answer. Never display text.</summary>
    public string Code { get; private init; }

    /// <summary>Where this option sits among its siblings.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>This option's wording, one row per locale.</summary>
    public IReadOnlyCollection<QuestionOptionTranslation> Translations => _translations;

    internal static QuestionOption Create(
        Guid questionVersionId,
        string code,
        int displayOrder,
        Locale sourceLocale,
        string label,
        DateTimeOffset at)
    {
        var option = new QuestionOption(questionVersionId, code, displayOrder);
        option._translations.Add(QuestionOptionTranslation.Authored(option.Id, sourceLocale, label, at));
        return option;
    }

    /// <summary>Attaches the generated counterpart in the other official locale.</summary>
    public QuestionOptionTranslation AttachTranslation(Locale locale, string label, DateTimeOffset at)
    {
        if (Translation(locale) is not null)
        {
            throw new DomainRuleViolationException($"This option already has {locale} wording.");
        }

        var translation = QuestionOptionTranslation.Generated(Id, locale, label, at);
        _translations.Add(translation);
        return translation;
    }

    /// <summary>This option's wording in one locale, if it exists yet.</summary>
    public QuestionOptionTranslation? Translation(Locale locale) =>
        _translations.SingleOrDefault(t => t.Locale == locale);

    /// <summary>Moves this option among its siblings. Not a versioned change.</summary>
    public void Reorder(int displayOrder) => DisplayOrder = displayOrder;
}
