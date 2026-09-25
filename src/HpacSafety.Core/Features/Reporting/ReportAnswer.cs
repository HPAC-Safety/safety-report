using System.Globalization;
using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     One answer to one question, as it was asked, stored as one string — or, for a
///     yes/no or checkbox question, as one boolean.
/// </summary>
/// <remarks>
///     <para>
///         The value is the words the reporter saw — the label they picked, the text
///         they typed, or an ISO 8601 date or time. A yes/no or checkbox answer holds
///         no words at all: it is <see cref="BooleanValue" />, and only the interface
///         turns it into Yes / Oui or No / Non (ADR-0130). It is not
///         an option code and it resolves through nothing, so relabelling or removing a
///         choice tomorrow cannot change what this answer says (ADR-0072).
///     </para>
///     <para>
///         Every answer is recorded in the reporter's language, and is immutable once
///         written — nothing on the submission path, or anywhere else, ever
///         overwrites <see cref="Value" /> or <see cref="Locale" />. Whether it has a
///         second language at all is decided when it is recorded
///         (<see cref="TranslationMode" />, ADR-0112): a select answer copies its
///         choice's other label then (<see cref="TranslationSource.Choice" />);
///         free text marked for translation is filled later, off the submission
///         path, mechanically by the Worker (<see cref="TranslationSource.Auto" />) or
///         by an administrator (<see cref="TranslationSource.Human" />); and anything
///         else never has one. See ADR-0080.
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
		Value is not null && TranslatedValue is null && TranslationMode == TranslationMode.Machine;

	/// <summary>
	///     The second language as a reader should see it: null for an answer that
	///     never has one, even if an older row stored one before ADR-0112.
	/// </summary>
	public string? DisplayedTranslation => TranslationMode == TranslationMode.None ? null : TranslatedValue;

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
		return locale == Locale ? Value : DisplayedTranslation ?? Value;
	}

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

		// A type-ahead is the one option type a reporter may answer with words the
		// question does not offer: the submission records them as a new choice
		// (ADR-0063), so they are validated as present, not as offered. Every
		// other select is checked against the question's live choices, which
		// belong to the question rather than to any revision (ADR-0095).
		if (value is not null
			&& revision.ExpectsOptions
			&& !revision.TakesReporterAdditions
			&& !question.Offers(value, locale))
		{
			throw new DomainRuleViolationException($"'{question.Key}' did not offer that answer.");
		}

		var (mode, other) = SecondLanguageOf(question, revision, value, locale);

		return new ReportAnswer(reportId, question, revision, locale, at)
		{
			Value = value,
			TranslationMode = mode,
			TranslatedValue = other,
			TranslationSource = other is null ? null : Reporting.TranslationSource.Choice,
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
			throw new DomainRuleViolationException($"'{question.Key}' must be answered with a boolean, not text.");
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
	///     How this answer gets its second language, and — for a value naming a
	///     choice written in both languages — that choice's other label, copied now.
	///     A lookup, never a translation provider, so the submission path stays
	///     provider-free. See ADR-0112.
	/// </summary>
	private static (TranslationMode Mode, string? FromChoice) SecondLanguageOf(
		Question question,
		QuestionRevision revision,
		string? value,
		Locale locale)
	{
		if (!revision.StoresLocalizedValue)
		{
			return (revision.IsTranslatable ? TranslationMode.Machine : TranslationMode.None, null);
		}

		if (value is null)
		{
			return (TranslationMode.Choice, null);
		}

		// A choice with both languages supplies the other one. A value naming no
		// such choice — a type-ahead value the reporter typed, or a choice that
		// still has one language — is left for the Worker.
		var other = question.OtherLabelOf(value, locale);

		return other is null
			? (TranslationMode.Machine, null)
			: (TranslationMode.Choice, other);
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
