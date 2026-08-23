
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// One choice's wording in one locale. The choice itself is stored as an
/// invariant <see cref="QuestionOption.Code"/>; this is the only place its
/// display text exists.
/// </summary>
public class QuestionOptionTranslation
{
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private QuestionOptionTranslation()
    {
    }
#pragma warning restore CS8618

    private QuestionOptionTranslation(
        TinyId questionOptionId,
        Locale locale,
        string label,
        bool isSource,
        bool isMachineTranslated,
        DateTimeOffset at)
    {
        Id = TinyId.New();
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
    public TinyId Id { get; private init; }

    /// <summary>The option this wording belongs to.</summary>
    public TinyId QuestionOptionId { get; private init; }

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
        TinyId optionId, Locale locale, string label, DateTimeOffset at) =>
        new(optionId, locale, label, isSource: true, isMachineTranslated: false, at);

    internal static QuestionOptionTranslation Generated(
        TinyId optionId, Locale locale, string label, DateTimeOffset at) =>
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
