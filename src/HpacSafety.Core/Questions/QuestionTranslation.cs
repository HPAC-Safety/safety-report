using HpacSafety.Core.Values;

namespace HpacSafety.Core.Questions;

/// <summary>
/// A question's wording in one locale. Every version carries exactly one row
/// with <see cref="IsSource"/> set — the language a human actually wrote in —
/// and one generated counterpart, the same shape <c>summaries</c> uses.
/// </summary>
/// <remarks>
/// Question text is content, not UI chrome. It is authored in the admin UI, so
/// it cannot live in <c>locales/</c>, which is git-tracked and generated in CI.
/// See docs/localization.md.
/// </remarks>
public class QuestionTranslation
{
    private QuestionTranslation(
        Guid questionVersionId,
        Locale locale,
        string label,
        string? helpText,
        string? placeholder,
        bool isSource,
        bool isMachineTranslated,
        DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        QuestionVersionId = questionVersionId;
        Locale = locale;
        Label = NotBlank(label);
        HelpText = helpText;
        Placeholder = placeholder;
        IsSource = isSource;
        IsMachineTranslated = isMachineTranslated;
        TranslatedAt = isMachineTranslated ? at : null;
        UpdatedAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The version this wording belongs to.</summary>
    public Guid QuestionVersionId { get; private init; }

    /// <summary>The locale this wording is in.</summary>
    public Locale Locale { get; private init; }

    /// <summary>The question as the reporter reads it.</summary>
    public string Label { get; private set; }

    /// <summary>Supporting copy shown under the label.</summary>
    public string? HelpText { get; private set; }

    /// <summary>Placeholder text for free-text types.</summary>
    public string? Placeholder { get; private set; }

    /// <summary>True for the one locale a human authored this version in.</summary>
    public bool IsSource { get; private init; }

    /// <summary>
    /// True while nobody has read the generated text. Cleared the moment an
    /// admin edits it, so the builder can show which half is unreviewed.
    /// </summary>
    public bool IsMachineTranslated { get; private set; }

    /// <summary>When the machine translation was produced, if it was.</summary>
    public DateTimeOffset? TranslatedAt { get; private set; }

    /// <summary>When this wording last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static QuestionTranslation Authored(
        Guid questionVersionId,
        Locale locale,
        string label,
        string? helpText,
        string? placeholder,
        DateTimeOffset at) =>
        new(questionVersionId, locale, label, helpText, placeholder,
            isSource: true, isMachineTranslated: false, at);

    internal static QuestionTranslation Generated(
        Guid questionVersionId,
        Locale locale,
        string label,
        string? helpText,
        string? placeholder,
        DateTimeOffset at) =>
        new(questionVersionId, locale, label, helpText, placeholder,
            isSource: false, isMachineTranslated: true, at);

    /// <summary>
    /// Corrects generated wording by hand. A correction is not a rewording of
    /// the question — it says the same thing the source says — so it does not
    /// create a new version, and it stops the text reading as unreviewed.
    /// </summary>
    public void ReviseByHand(string label, string? helpText, string? placeholder, DateTimeOffset at)
    {
        if (IsSource)
        {
            throw new DomainRuleViolationException(
                "Rewording the source language changes what the question asks. Revise the question instead, which creates a new version.");
        }

        Label = NotBlank(label);
        HelpText = helpText;
        Placeholder = placeholder;
        IsMachineTranslated = false;
        UpdatedAt = at;
    }

    private static string NotBlank(string label) =>
        string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("A question translation needs a label.")
            : label;
}
