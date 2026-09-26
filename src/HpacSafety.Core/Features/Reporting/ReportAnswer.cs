using System.Globalization;
using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     One answer to one question, as it was asked: the choice it names, one
///     boolean, or one string.
/// </summary>
/// <remarks>
///     <para>
///         A single-select, multi-select, or type-ahead answer names its
///         <see cref="QuestionChoice" /> by <see cref="ChoiceId" /> and stores none of
///         its wording: both languages are read from the choice (ADR-0128). A choice an
///         answer names is never erased, so the answer always resolves.
///     </para>
///     <para>
///         A yes/no or checkbox answer holds no words at all: it is
///         <see cref="BooleanValue" />, and only the interface turns it into Yes / Oui
///         or No / Non (ADR-0130). Every other answer is one string, the words the
///         reporter gave — the text they typed, or an ISO 8601 date or time
///         (ADR-0072).
///     </para>
///     <para>
///         Every answer is recorded in the reporter's language, and is immutable once
///         written — nothing on the submission path, or anywhere else, ever
///         overwrites <see cref="Value" />, <see cref="ChoiceId" />, or
///         <see cref="Locale" />. Whether it has a second language at all is decided
///         when it is recorded (<see cref="TranslationMode" />, ADR-0112): a choice
///         answer reads its choice's other label (<see cref="TranslationSource.Choice" />);
///         free text marked for translation is filled later, off the submission
///         path, mechanically by the Worker (<see cref="TranslationSource.Auto" />) or
///         by an administrator (<see cref="TranslationSource.Human" />); and anything
///         else never has one. See ADR-0080.
///     </para>
///     <para>
///         A select answer stored before ADR-0128 keeps the label it copied in
///         <see cref="Value" />, and names its choice as well: the migration linked it
///         without rewriting it. The choice wins wherever the answer is read.
///     </para>
///     <para>
///         A multi-select produces one of these per chosen choice.
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
		TinyId reportId,
		Question question,
		QuestionRevision revision,
		Locale locale,
		DateTimeOffset at)
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
	///     The whole answer, as one string, exactly as the reporter submitted it.
	///     Null for a question the reporter skipped — nothing is ever synthesized in
	///     its place. Immutable once written (ADR-0080): nothing ever edits this after
	///     submission.
	/// </summary>
	public string? Value { get; private init; }

	/// <summary>
	///     A yes/no or checkbox answer: <c>true</c> or <c>false</c>, with
	///     <see cref="Value" /> null and no second language. Null for every other
	///     type and for a skipped question. Immutable, like <see cref="Value" />
	///     (ADR-0130).
	/// </summary>
	public bool? BooleanValue { get; private init; }

	/// <summary>
	///     The choice this answer names, for a single-select, multi-select, or
	///     type-ahead question; null for every other type and for a skipped answer.
	///     Immutable. See ADR-0128.
	/// </summary>
	public TinyId? ChoiceId { get; private init; }

	/// <summary>
	///     The choice <see cref="ChoiceId" /> names, when the reader loaded it. Every
	///     read of a choice answer's wording goes through it; reading one without it
	///     loaded throws rather than showing a choice answer as skipped.
	/// </summary>
	public QuestionChoice? Choice { get; private set; }

	/// <summary>Whether the reporter gave this answer at all: it names a choice, holds a boolean, or holds a value.</summary>
	public bool IsAnswered => ChoiceId is not null || BooleanValue is not null || Value is not null;

	/// <summary>
	///     The official language <see cref="Value" /> is written in. Immutable, like
	///     <see cref="Value" /> itself.
	/// </summary>
	public Locale Locale { get; private init; }

	/// <summary>
	///     The other official language of <see cref="Value" />, once it has been
	///     supplied. Null until then, and null forever for a skipped answer.
	/// </summary>
	public string? TranslatedValue { get; private set; }

	/// <summary>
	///     How <see cref="TranslatedValue" /> was produced, or null while it is still
	///     unset. See ADR-0080.
	/// </summary>
	public TranslationSource? TranslationSource { get; private set; }

	/// <summary>
	///     How this answer gets its second language, decided when it is recorded and
	///     never changed. See ADR-0112.
	/// </summary>
	public TranslationMode TranslationMode { get; private init; }

	/// <summary>
	///     True while this answer is waiting for machine translation: it has a value,
	///     its mode is <see cref="Reporting.TranslationMode.Machine" />, and no
	///     translation has been supplied yet.
	/// </summary>
	public bool NeedsTranslation =>
		ChoiceId is null && Value is not null && TranslatedValue is null && TranslationMode == TranslationMode.Machine;

	/// <summary>
	///     The second language as a reader should see it: null for an answer that
	///     never has one, even if an older row stored one before ADR-0112.
	/// </summary>
	public string? DisplayedTranslation =>
		ChoiceId is not null
			? NamedChoice.OtherLabel(Locale)
			: TranslationMode == TranslationMode.None
				? null
				: TranslatedValue;

	/// <summary>
	///     The answer's words in the language it was given in: its choice's label in
	///     that language (or the one language the choice has), or its value. Null for
	///     a skipped answer and for a yes/no or checkbox, which has no words.
	/// </summary>
	public string? Text => ChoiceId is not null ? NamedChoice.Label(Locale) : Value;

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
		return ChoiceId is not null
			? NamedChoice.Label(locale)
			: locale == Locale
				? Value
				: DisplayedTranslation ?? Value;
	}

	// A merged value reads as the one it was merged into (ADR-0129).
	private QuestionChoice NamedChoice =>
		(Choice ?? throw new InvalidOperationException(
			"This answer names a choice that was not loaded with it. Include the answer's choice to read its wording.")).Resolved;

	/// <summary>
	///     Records one answer, of any type, against the question's current revision. A
	///     select value is checked against the choices that revision offered;
	///     everything else is taken as given, in the invariant written form its type
	///     calls for.
	/// </summary>
	internal static ReportAnswer For(
		TinyId reportId,
		Question question,
		string? value,
		Locale locale,
		DateTimeOffset at)
	{
		return For(reportId, question, question.CurrentRevision, value, locale, at);
	}

	/// <summary>
	///     Records one answer against an exact revision — the current one, or a known,
	///     non-deleted, superseded one a reporter's browser session spanned an
	///     Administrator's edit across. Validation always runs against that exact
	///     revision's historical type and privacy (choices are the question's own, ADR-0095), never against whatever
	///     the question's current revision happens to be now.
	/// </summary>
	internal static ReportAnswer For(
		TinyId reportId,
		Question question,
		QuestionRevision revision,
		string? value,
		Locale locale,
		DateTimeOffset at)
	{
		if (revision.QuestionId != question.Id)
		{
			throw new DomainRuleViolationException("That revision does not belong to this question.");
		}

		value = InStoredForm(question, revision, value);

		if (revision.IsRequired
			&& string.IsNullOrWhiteSpace(value))
		{
			throw new DomainRuleViolationException($"'{question.Key}' is required.");
		}

		if (revision.StoresLocalizedValue)
		{
			return Naming(reportId, question, revision, ChoiceWorded(question, revision, value, locale, at), locale, at);
		}

		return new ReportAnswer(reportId, question, revision, locale, at)
		{
			Value = value,
			TranslationMode = revision.IsTranslatable ? TranslationMode.Machine : TranslationMode.None,
		};
	}

	/// <summary>
	///     Records a yes/no or checkbox answer against an exact revision, as a boolean
	///     with no words and no second language (ADR-0130). Any other type refuses
	///     it.
	/// </summary>
	internal static ReportAnswer For(
		TinyId reportId,
		Question question,
		QuestionRevision revision,
		bool value,
		Locale locale,
		DateTimeOffset at)
	{
		if (revision.QuestionId != question.Id)
		{
			throw new DomainRuleViolationException("That revision does not belong to this question.");
		}

		if (!revision.IsBoolean)
		{
			throw new DomainRuleViolationException($"'{question.Key}' is not a yes or no question.");
		}

		return new ReportAnswer(reportId, question, revision, locale, at)
		{
			BooleanValue = value,
			TranslationMode = TranslationMode.None,
		};
	}

	/// <summary>
	///     Records a single-select, multi-select, or type-ahead answer naming one of
	///     the question's live choices by its identifier — what the submission sends
	///     (ADR-0128). A removed choice, or another question's, is refused.
	/// </summary>
	internal static ReportAnswer ForChoice(
		TinyId reportId,
		Question question,
		QuestionRevision revision,
		TinyId choiceId,
		Locale locale,
		DateTimeOffset at)
	{
		if (revision.QuestionId != question.Id)
		{
			throw new DomainRuleViolationException("That revision does not belong to this question.");
		}

		if (!revision.StoresLocalizedValue)
		{
			throw new DomainRuleViolationException($"'{question.Key}' does not take a choice.");
		}

		var choice = question.OfferedChoice(choiceId)
					 ?? throw new DomainRuleViolationException($"'{question.Key}' did not offer that answer.");

		return Naming(reportId, question, revision, choice, locale, at);
	}

	/// <summary>
	///     The choice a reporter's words name, for a choice type answered in words
	///     rather than by identifier. A single-select or multi-select accepts only a
	///     live choice labelled exactly that way in the reporter's language; a
	///     type-ahead matches or adds one (ADR-0129). Null for a skipped answer.
	/// </summary>
	private static QuestionChoice? ChoiceWorded(Question question,
												QuestionRevision revision,
												string? value,
												Locale locale,
												DateTimeOffset at)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (revision.TakesReporterAdditions)
		{
			return question.AddChoiceFromReporter(value, locale, at);
		}

		return question.OfferedChoiceLabelled(value, locale)
			   ?? throw new DomainRuleViolationException($"'{question.Key}' did not offer that answer.");
	}

	/// <summary>
	///     A choice answer: the choice named, no wording copied, both languages read
	///     from it. A null choice is a skip, which the caller has already checked the
	///     question allows.
	/// </summary>
	private static ReportAnswer Naming(TinyId reportId,
									   Question question,
									   QuestionRevision revision,
									   QuestionChoice? choice,
									   Locale locale,
									   DateTimeOffset at)
	{
		return new ReportAnswer(reportId, question, revision, locale, at)
		{
			ChoiceId = choice?.Id,
			Choice = choice,
			TranslationMode = TranslationMode.Choice,
			TranslationSource = choice is null ? null : Reporting.TranslationSource.Choice,
		};
	}

	/// <summary>
	///     A date or time answer exactly as ADR-0072 stores it — a
	///     <c>YYYY-MM-DD</c> calendar day or an <c>HH:mm</c> wall-clock time — or a
	///     refusal. A yes/no or checkbox answer is a boolean, never text, so any text
	///     for one is refused (ADR-0130). Nothing is converted: the form's own
	///     inputs already send these, so any other shape came from somewhere else
	///     (REQ-QB-118). A blank one is a skip. Every other type passes through.
	/// </summary>
	/// <remarks>
	///     The refusal names the question, never the value: it reaches the reporter
	///     in a problem response, and a value is report content.
	/// </remarks>
	private static string? InStoredForm(Question question,
										QuestionRevision revision,
										string? value)
	{
		if (revision.Type is not (QuestionType.Date or QuestionType.Time) && !revision.IsBoolean)
		{
			return value;
		}

		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		if (revision.IsBoolean)
		{
			throw new DomainRuleViolationException($"'{question.Key}' must be answered with a boolean, never with words.");
		}

		var (stored, shape) = revision.Type == QuestionType.Date
			? (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
				"a date written YYYY-MM-DD")
			: (TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
				"a time written HH:mm");

		return stored
			? value
			: throw new DomainRuleViolationException($"'{question.Key}' must be answered with {shape}.");
	}

	/// <summary>
	///     Supplies the second language of this answer, mechanically. The reporter's
	///     own <see cref="Value" /> is never touched — this fills the language they
	///     did not answer in.
	/// </summary>
	public void SupplyAutoTranslation(string translated)
	{
		SupplyTranslation(translated, Reporting.TranslationSource.Auto);
	}

	/// <summary>
	///     Supplies or corrects the second language of this answer by hand. Unlike the
	///     automatic path, an administrator may overwrite an existing translation —
	///     including one the Worker already produced.
	/// </summary>
	public void SupplyHumanTranslation(string translated)
	{
		SupplyTranslation(translated, Reporting.TranslationSource.Human, allowOverwrite: true);
	}

	private void SupplyTranslation(string translated,
								   Reporting.TranslationSource source,
								   bool allowOverwrite = false)
	{
		if (ChoiceId is not null)
		{
			throw new DomainRuleViolationException(
				"A choice answer reads its second language from its choice. See ADR-0128.");
		}

		if (TranslationMode == TranslationMode.None)
		{
			throw new DomainRuleViolationException("This answer never has a second language. See ADR-0112.");
		}

		if (Value is null)
		{
			throw new DomainRuleViolationException("A skipped answer has nothing to translate.");
		}

		if (TranslatedValue is not null
			&& !allowOverwrite)
		{
			throw new DomainRuleViolationException("This answer already has a translation.");
		}

		if (string.IsNullOrWhiteSpace(translated))
		{
			throw new DomainRuleViolationException("A supplied translation cannot be blank.");
		}

		TranslatedValue = translated;
		TranslationSource = source;
	}

	/// <summary>Stamps this answer deleted, as part of its report's soft deletion (REQ-DOM-007).</summary>
	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}
}
