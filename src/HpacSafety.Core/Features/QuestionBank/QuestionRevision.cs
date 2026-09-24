namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     A question exactly as it was asked at a point in time: its type, its complete
///     bilingual wording, its order, privacy, active state, required
///     state, system state, and whether its answers need translation. Immutable once created — rewording,
///     retyping, reordering, changing privacy, activating, or
///     deactivating produces a new revision, so a report filed last year still
///     renders the revision it was actually answering. Its choices are not part
///     of it: they belong to the <see cref="Question" /> and are edited in place
///     (ADR-0095).
/// </summary>
/// <remarks>
///     <para>
///         A revision is born complete: both official languages are supplied together,
///         atomically, by whoever authors it. There is no partially translated, pending,
///         or machine-generated state to reach a database — see product invariant #1 and
///         <c>docs/data-and-persistence.md</c>.
///     </para>
///     <para>
///         Order, privacy, active state, system state, and required state are all
///         revision fields — see
///         <c>features/question-bank-and-form/question-bank-and-form.feature</c>. None
///         of them can be mutated on an existing revision; every change, including
///         these, is a new revision row created by <see cref="Question" />.
///     </para>
/// </remarks>
public class QuestionRevision
{
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. It exists for
	// the ORM and for nothing else — domain code still has to go through the
	// constructor or factory that follows, so no caller can reach a half-built
	// aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private QuestionRevision()
	{
	}
#pragma warning restore CS8618

	private QuestionRevision(
		TinyId questionId,
		int revisionNumber,
		QuestionType type,
		string labelEn,
		string labelFr,
		string? helpTextEn,
		string? helpTextFr,
		string? placeholderEn,
		string? placeholderFr,
		bool isSystem,
		bool isRequired,
		bool isPrivate,
		bool isActive,
		int displayOrder,
		TinyId? dependsOnQuestionId,
		string? dependsOnOptionCode,
		TinyId? groupedUnderQuestionId,
		bool isTranslatable,
		DateTimeOffset at)
	{
		Id = TinyId.New();
		QuestionId = questionId;
		RevisionNumber = revisionNumber;
		Type = type;

		if (CollectsNoAnswerType(type)
			&& (isRequired || isPrivate))
		{
			throw new DomainRuleViolationException(
				$"A {type} question collects no answer and cannot be marked required or private.");
		}

		// A system question — publication or media consent — is always required
		// whenever the form asks it: silence is not consent. Every other
		// question's required state is authored by an administrator. See
		// ADR-0061 and ADR-0117.
		if (isTranslatable
			&& !CanBeTranslatable(type))
		{
			throw new DomainRuleViolationException(
				$"Only a short- or long-text question can need translation; a {type} answer never has a second language. See ADR-0112.");
		}

		IsSystem = isSystem;
		IsRequired = isSystem || isRequired;
		IsPrivate = isPrivate;
		IsTranslatable = isTranslatable;
		IsActive = isActive;
		DisplayOrder = displayOrder;
		DependsOnQuestionId = ValidatedDependency(dependsOnQuestionId, questionId, type, isSystem);
		DependsOnOptionCode = ValidatedOptionCode(dependsOnOptionCode, DependsOnQuestionId);
		GroupedUnderQuestionId = ValidatedGrouping(groupedUnderQuestionId, questionId);
		LabelEn = NotBlank(labelEn);
		LabelFr = NotBlank(labelFr);
		HelpTextEn = helpTextEn;
		HelpTextFr = helpTextFr;
		PlaceholderEn = placeholderEn;
		PlaceholderFr = placeholderFr;
		CreatedAt = at;
	}

	/// <summary>Surrogate key. Answers reference this, never the question row.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The question this is a revision of.</summary>
	public TinyId QuestionId { get; private init; }

	/// <summary>Increments by one per revision, starting at 1.</summary>
	public int RevisionNumber { get; private init; }

	/// <summary>What this revision asks for.</summary>
	public QuestionType Type { get; private init; }

	/// <summary>
	///     True only for a publication- or media-consent revision. Copied from the
	///     question at revision-creation time — every revision of the same
	///     question carries the same value, since a question's system status
	///     never changes across its history.
	/// </summary>
	public bool IsSystem { get; private init; }

	/// <summary>
	///     Whether a reporter must answer before submitting. Authored by an
	///     administrator on every ordinary question, and forced true on the two
	///     consent questions, which cannot be made optional. See
	///     ADR-0061.
	/// </summary>
	public bool IsRequired { get; private init; }

	/// <summary>
	///     Whether this answer is private redaction context rather than a fact
	///     eligible for the summary.
	/// </summary>
	public bool IsPrivate { get; private init; }

	/// <summary>
	///     Whether an answer to this revision is machine-translated into the other
	///     official language. Only ever true for short or long text; an
	///     administrator decides, and long text starts out true. See ADR-0112.
	/// </summary>
	public bool IsTranslatable { get; private init; }

	/// <summary>Whether this revision is the one the form asks.</summary>
	public bool IsActive { get; private init; }

	/// <summary>Where this revision sits on the form.</summary>
	public int DisplayOrder { get; private init; }

	/// <summary>
	///     The question this one is conditional on, if any. The form enables this
	///     question only when that question's answer satisfies the condition:
	///     "yes" for a yes/no parent, or the option named by
	///     <see cref="DependsOnOptionCode" /> for a single-select parent.
	/// </summary>
	/// <remarks>
	///     This names the stable <see cref="Question" />, not a revision of it, so
	///     rewording the parent does not break the child. Only a
	///     <see cref="QuestionType.YesNo" /> or <see cref="QuestionType.SingleSelect" />
	///     question may be a parent; that is a fact about the parent's current
	///     revision, which this row cannot see, so it is checked by
	///     <see cref="QuestionDependencies" /> rather than here. See ADR-0060,
	///     ADR-0074.
	/// </remarks>
	public TinyId? DependsOnQuestionId { get; private init; }

	/// <summary>
	///     The invariant option code a <see cref="QuestionType.SingleSelect" />
	///     parent must be answered with to enable this question. Always
	///     <c>null</c> when <see cref="DependsOnQuestionId" /> is null or names a
	///     <see cref="QuestionType.YesNo" /> parent, whose condition is the
	///     invariant "yes" instead. See ADR-0074.
	/// </summary>
	public string? DependsOnOptionCode { get; private init; }

	/// <summary>
	///     The <see cref="QuestionType.Group" /> question this revision renders
	///     together with, if any. Distinct from <see cref="DependsOnQuestionId" />:
	///     this is "display together," never "conditional on." Names the stable
	///     <see cref="Question" />, not a revision of it, for the same reason a
	///     dependency does. See ADR-0076.
	/// </summary>
	public TinyId? GroupedUnderQuestionId { get; private init; }

	/// <summary>
	///     Whether a reporter's value the question does not offer is added as a new
	///     choice at submission rather than rejected. True only for
	///     <see cref="QuestionType.Autocomplete" />. See ADR-0063, ADR-0095.
	/// </summary>
	public bool TakesReporterAdditions => Type == QuestionType.Autocomplete;

	/// <summary>The English wording.</summary>
	public string LabelEn { get; private init; }

	/// <summary>The French wording.</summary>
	public string LabelFr { get; private init; }

	/// <summary>Supporting English copy shown under the label.</summary>
	public string? HelpTextEn { get; private init; }

	/// <summary>Supporting French copy shown under the label.</summary>
	public string? HelpTextFr { get; private init; }

	/// <summary>English placeholder text, for free-text types.</summary>
	public string? PlaceholderEn { get; private init; }

	/// <summary>French placeholder text, for free-text types.</summary>
	public string? PlaceholderFr { get; private init; }

	/// <summary>When this revision was created.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>When this revision was deleted along with its question, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>
	///     True when this type answers from a fixed set of choices rather
	///     than free text.
	/// </summary>
	public bool ExpectsOptions =>
		Type is QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.YesNo
			or QuestionType.Autocomplete;

	/// <summary>
	///     True when this type is instructional or structural rather than
	///     something a reporter answers. Neither type may be required, private,
	///     system, a conditional parent, or a conditional child. See ADR-0076.
	/// </summary>
	public bool CollectsNoAnswer => CollectsNoAnswerType(Type);

	/// <summary>
	///     True when an answer to this type is stored in the reporter's language and
	///     so needs an administrator to supply the other one (ADR-0072). Yes/no is
	///     excluded although it answers from a fixed set: its stored form is the
	///     invariant <c>yes</c> or <c>no</c>, identical in both languages, which is
	///     what lets a conditional question compare it without knowing the locale
	///     (ADR-0060).
	/// </summary>
	public bool StoresLocalizedValue => ExpectsOptions && Type != QuestionType.YesNo;

	/// <summary>
	///     True when this type's answers are the reporter's own free text, the only
	///     kind an administrator may mark as needing machine translation (ADR-0112).
	/// </summary>
	public static bool CanBeTranslatable(QuestionType type)
	{
		return type is QuestionType.ShortText or QuestionType.LongText;
	}

	/// <summary>
	///     Whether a question of this type needs translation when nobody has said:
	///     long text does, and everything else does not (ADR-0112).
	/// </summary>
	public static bool TranslatableByDefault(QuestionType type)
	{
		return type == QuestionType.LongText;
	}

	/// <summary>True when this type takes at most one answer.</summary>
	public bool TakesOneAnswer =>
		Type is QuestionType.SingleSelect or QuestionType.YesNo or QuestionType.Autocomplete;

	/// <summary>
	///     The two codes a <see cref="QuestionType.YesNo" /> question accepts. Fixed,
	///     unorderable, and with no third state — a yes/no question has no default
	///     and the reporter must choose one.
	/// </summary>
	public static IReadOnlyList<string> YesNoCodes { get; } = ["yes", "no"];

	/// <summary>The wording in one locale.</summary>
	public string Label(Locale locale)
	{
		return locale == Locale.FrCa ? LabelFr : LabelEn;
	}

	/// <summary>The help text in one locale.</summary>
	public string? HelpText(Locale locale)
	{
		return locale == Locale.FrCa ? HelpTextFr : HelpTextEn;
	}

	/// <summary>
	///     Deletes this one revision out of its question's history — distinct from
	///     <see cref="Question.Delete" />, which retires the whole question. Stamps
	///     this row rather than removing it.
	///     Idempotent, and the caller (<see cref="Question" />) has already checked
	///     that no answer references it and that it is not the current revision.
	/// </summary>
	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}

	internal static QuestionRevision Create(
		TinyId questionId,
		int revisionNumber,
		QuestionType type,
		string labelEn,
		string labelFr,
		string? helpTextEn,
		string? helpTextFr,
		string? placeholderEn,
		string? placeholderFr,
		bool isSystem,
		bool isRequired,
		bool isPrivate,
		bool isActive,
		int displayOrder,
		TinyId? dependsOnQuestionId,
		string? dependsOnOptionCode,
		TinyId? groupedUnderQuestionId,
		bool isTranslatable,
		DateTimeOffset at)
	{
		return new QuestionRevision(
			questionId, revisionNumber, type, labelEn, labelFr, helpTextEn, helpTextFr, placeholderEn, placeholderFr,
			isSystem, isRequired, isPrivate, isActive, displayOrder, dependsOnQuestionId, dependsOnOptionCode,
			groupedUnderQuestionId, isTranslatable, at);
	}

	/// <summary>
	///     Checks the part of a dependency this row can see on its own: that it
	///     does not point at itself, that the question is not the
	///     a system question, and that a question collecting no
	///     answer is not made conditional. Whether the <i>parent</i> is a
	///     question type that can enable another one is a fact about a different
	///     row, so <see cref="QuestionDependencies" /> checks that. See ADR-0060,
	///     ADR-0076.
	/// </summary>
	private static TinyId? ValidatedDependency(TinyId? dependsOnQuestionId,
											   TinyId questionId,
											   QuestionType type,
											   bool isSystem)
	{
		if (dependsOnQuestionId is not { } parent)
		{
			return null;
		}

		if (parent == questionId)
		{
			throw new DomainRuleViolationException("A question cannot be conditional on itself.");
		}

		if (isSystem)
		{
			throw new DomainRuleViolationException(
				"A consent question is never conditional on another question. Publication consent is always asked, and when media consent is asked is a rule of its own.");
		}

		if (CollectsNoAnswerType(type))
		{
			throw new DomainRuleViolationException($"A {type} question collects no answer and cannot be made conditional.");
		}

		return parent;
	}

	/// <summary>
	///     Checks the part of a grouping this row can see on its own: that it
	///     does not name itself. Whether the named question is currently a
	///     <see cref="QuestionType.Group" /> is a fact about a different row, so
	///     <see cref="QuestionGrouping" /> checks that. See ADR-0076.
	/// </summary>
	private static TinyId? ValidatedGrouping(TinyId? groupedUnderQuestionId,
											 TinyId questionId)
	{
		if (groupedUnderQuestionId is not { } parent)
		{
			return null;
		}

		if (parent == questionId)
		{
			throw new DomainRuleViolationException("A question cannot be grouped under itself.");
		}

		return parent;
	}

	private static bool CollectsNoAnswerType(QuestionType type)
	{
		return type is QuestionType.Statement or QuestionType.Group;
	}

	/// <summary>
	///     Normalizes a required option code, and refuses one with no parent to
	///     attach it to. Whether the parent's type actually takes an option code
	///     (a <see cref="QuestionType.SingleSelect" /> parent does, a
	///     <see cref="QuestionType.YesNo" /> one does not) is, like the parent's
	///     type itself, a fact <see cref="QuestionDependencies" /> checks against
	///     the live bank. See ADR-0074.
	/// </summary>
	private static string? ValidatedOptionCode(string? dependsOnOptionCode,
											   TinyId? dependsOnQuestionId)
	{
		if (dependsOnOptionCode is null)
		{
			return null;
		}

		if (dependsOnQuestionId is null)
		{
			throw new DomainRuleViolationException("A required option needs a parent question to name it.");
		}

		return QuestionKey.Normalize(dependsOnOptionCode);
	}

	/// <summary>
	///     Whether the reporter's answer to <paramref name="parent" /> satisfies
	///     this revision's condition, so the question it belongs to should be
	///     shown. Always true when this revision is unconditional. See ADR-0074.
	/// </summary>
	/// <param name="parent">
	///     The question named by <see cref="DependsOnQuestionId" />, or null when
	///     this revision is unconditional or the caller has not loaded it.
	/// </param>
	/// <param name="parentAnswerValue">
	///     The reporter's answer to <paramref name="parent" /> so far, in
	///     <paramref name="locale" />, or null when they have not answered it yet.
	/// </param>
	/// <param name="locale">The locale <paramref name="parentAnswerValue" /> was given in.</param>
	public bool IsEnabledGiven(Question? parent,
							   string? parentAnswerValue,
							   Locale locale)
	{
		if (DependsOnQuestionId is null)
		{
			return true;
		}

		if (parent is null
			|| parentAnswerValue is null)
		{
			return false;
		}

		var parentRevision = parent.CurrentRevision;

		return parentRevision.Type == QuestionType.YesNo
			? string.Equals(parentAnswerValue, "yes", StringComparison.Ordinal)
			: DependsOnOptionCode is { } requiredOptionCode
			  && string.Equals(
				  parent.Choice(requiredOptionCode)?.Label(locale), parentAnswerValue, StringComparison.Ordinal);
	}

	private static string NotBlank(string label)
	{
		return string.IsNullOrWhiteSpace(label)
			? throw new DomainRuleViolationException("A question revision needs wording in both official languages.")
			: label;
	}
}
