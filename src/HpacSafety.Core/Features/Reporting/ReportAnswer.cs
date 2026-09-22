using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     One answer to one question, as it was asked, stored as one string.
/// </summary>
/// <remarks>
///     <para>
///         The value is the words the reporter saw — the label they picked, the text
///         they typed, <c>yes</c> or <c>no</c>, or an ISO 8601 date or time. It is not
///         an option code and it resolves through nothing, so relabelling or removing a
///         choice tomorrow cannot change what this answer says (ADR-0072).
///     </para>
///     <para>
///         A select answer is recorded in the reporter's language only, and
///         <see cref="NeedsTranslation" /> puts it in front of an administrator to supply
///         the other one. Nothing on the submission path translates anything.
///     </para>
///     <para>
///         A multi-select produces one of these per chosen value, so "an answer is one
///         string" stays literally true and each value is translated on its own.
///     </para>
///     <para>
///         The reference is to a <see cref="QuestionRevision" /> as well as to the
///         question, because the revision is the record of the complete set of choices
///         this reporter was offered (ADR-0058) even though the answer no longer reads
///         its own content from it.
///     </para>
/// </remarks>
public class ReportAnswer
{
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private ReportAnswer()
    {
    }
#pragma warning restore CS8618

    private ReportAnswer(
        TinyId reportId, Question question, QuestionRevision revision, Locale locale, DateTimeOffset at)
    {
        Id = TinyId.New();
        ReportId = reportId;
        QuestionId = question.Id;
        QuestionRevisionId = revision.Id;
        QuestionKey = question.Key;
        IsPrivate = revision.IsPrivate;
        Locale = locale;
        AnsweredAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The report this answer belongs to.</summary>
    public TinyId ReportId { get; private init; }

    /// <summary>The question answered.</summary>
    public TinyId QuestionId { get; private init; }

    /// <summary>
    ///     The exact revision answered, which owns the wording and the
    ///     complete set of choices that were offered.
    /// </summary>
    public TinyId QuestionRevisionId { get; private init; }

    /// <summary>The question's invariant key, carried for exports and reads.</summary>
    public string QuestionKey { get; private init; }

    /// <summary>
    ///     Whether this answer is private redaction context, snapshotted from the
    ///     exact revision it was answered under when the answer is recorded.
    /// </summary>
    public bool IsPrivate { get; private init; }

    /// <summary>
    ///     The whole answer, as one string. Null for a question the reporter
    ///     skipped — nothing is ever synthesized in its place.
    /// </summary>
    public string? Value { get; private set; }

    /// <summary>The official language <see cref="Value" /> is written in.</summary>
    public Locale Locale { get; private init; }

    /// <summary>
    ///     The other official language of <see cref="Value" />, once an
    ///     administrator has supplied it. Null until then, and null forever for the
    ///     types whose stored form is the same in both languages.
    /// </summary>
    public string? TranslatedValue { get; private set; }

    /// <summary>
    ///     Whether this answer is waiting for an administrator to supply its second
    ///     language. True only for a localized select value.
    /// </summary>
    public bool NeedsTranslation { get; private set; }

    /// <summary>When the answer was given.</summary>
    public DateTimeOffset AnsweredAt { get; private init; }

    /// <summary>When this answer was deleted along with its report, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>
    ///     This answer as written in the given language, falling back to
    ///     what the reporter gave when the other language is not supplied yet.
    /// </summary>
    public string? ValueIn(Locale locale)
    {
        return locale == Locale ? Value : TranslatedValue ?? Value;
    }

    /// <summary>
    ///     Records one answer, of any type. A select value is checked against the
    ///     choices the revision offered; everything else is taken as given, in the
    ///     invariant written form its type calls for.
    /// </summary>
    internal static ReportAnswer For(
        TinyId reportId, Question question, string? value, Locale locale, DateTimeOffset at)
    {
        var revision = question.CurrentRevision;

        if (revision.IsRequired && string.IsNullOrWhiteSpace(value)) throw new DomainRuleViolationException($"'{question.Key}' is required.");

        if (value is not null && revision.ExpectsOptions && !revision.Offers(value, locale)) throw new DomainRuleViolationException($"'{question.Key}' did not offer that answer.");

        return new ReportAnswer(reportId, question, revision, locale, at)
        {
            Value = value,
            NeedsTranslation = value is not null && revision.StoresLocalizedValue
        };
    }

    /// <summary>
    ///     Supplies the second language of a select value. The reporter's own value
    ///     is never touched — this fills the language they did not answer in.
    /// </summary>
    public void SupplyTranslation(string translated)
    {
        if (!NeedsTranslation) throw new DomainRuleViolationException("This answer is not waiting for a translation.");

        if (string.IsNullOrWhiteSpace(translated)) throw new DomainRuleViolationException("A supplied translation cannot be blank.");

        TranslatedValue = translated;
        NeedsTranslation = false;
    }
}
