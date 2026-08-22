using HpacSafety.Core.Values;

namespace HpacSafety.Core.Questions;

/// <summary>
/// One choice's wording in one locale. The choice itself is stored as an
/// invariant <see cref="QuestionOption.Code"/>; this is the only place its
/// display text exists.
/// </summary>
public class QuestionOptionTranslation
{
    private QuestionOptionTranslation(
        Guid questionOptionId,
        Locale locale,
        string label,
        bool isSource,
        bool isMachineTranslated,
        DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        QuestionOptionId = questionOptionId;
        Locale = locale;
        Label = string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("A question option needs a label.")
            : label;
        IsSource = isSource;
        IsMachineTranslated = isMachineTranslated;
        TranslatedAt = isMachineTranslated ? at : null;
        UpdatedAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The option this wording belongs to.</summary>
    public Guid QuestionOptionId { get; private init; }

    /// <summary>The locale this wording is in.</summary>
    public Locale Locale { get; private init; }

    /// <summary>The choice as the reporter reads it.</summary>
    public string Label { get; private set; }

    /// <summary>True for the one locale a human authored.</summary>
    public bool IsSource { get; private init; }

    /// <summary>True while nobody has reviewed the generated text.</summary>
    public bool IsMachineTranslated { get; private set; }

    /// <summary>When the machine translation was produced, if it was.</summary>
    public DateTimeOffset? TranslatedAt { get; private set; }

    /// <summary>When this wording last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static QuestionOptionTranslation Authored(
        Guid optionId, Locale locale, string label, DateTimeOffset at) =>
        new(optionId, locale, label, isSource: true, isMachineTranslated: false, at);

    internal static QuestionOptionTranslation Generated(
        Guid optionId, Locale locale, string label, DateTimeOffset at) =>
        new(optionId, locale, label, isSource: false, isMachineTranslated: true, at);

    /// <summary>Corrects generated wording by hand.</summary>
    public void ReviseByHand(string label, DateTimeOffset at)
    {
        if (IsSource)
        {
            throw new DomainRuleViolationException(
                "Rewording the source language changes what the option means. Revise the question instead.");
        }

        Label = string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("A question option needs a label.")
            : label;
        IsMachineTranslated = false;
        UpdatedAt = at;
    }
}
