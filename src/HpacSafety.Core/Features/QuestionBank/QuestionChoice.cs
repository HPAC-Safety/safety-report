namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     One choice a single-select, multi-select, or type-ahead question offers.
///     Owned by the <see cref="Question" />, not by any revision of it, and
///     editable in place — see ADR-0095.
/// </summary>
/// <remarks>
///     <para>
///         Editing a choice never revises or forks its question: an answer stores the
///         reporter's own words (ADR-0072), so nothing an answer records depends on
///         this row staying as it was. A removed choice is stamped
///         <see cref="Deleted" />, never erased, and a fork copies every row —
///         removed ones included — onto the replacement.
///     </para>
///     <para>
///         A choice an Administrator writes has both official languages. A choice a
///         reporter typed into a type-ahead has only the language they typed it in
///         until an Administrator supplies the other, and is offered in the one it
///         has meanwhile (<see cref="Label" />).
///     </para>
/// </remarks>
public class QuestionChoice
{
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. It exists for
	// the ORM and for nothing else.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private QuestionChoice()
	{
	}
#pragma warning restore CS8618

	private QuestionChoice(
		TinyId questionId,
		string code,
		int displayOrder,
		string? labelEn,
		string? labelFr,
		bool addedByReporter,
		Locale? reporterLocale,
		DateTimeOffset? deleted)
	{
		Id = TinyId.New();
		QuestionId = questionId;
		Code = QuestionKey.Normalize(code);
		DisplayOrder = displayOrder;
		LabelEn = labelEn;
		LabelFr = labelFr;
		AddedByReporter = addedByReporter;
		ReporterLocale = reporterLocale;
		Deleted = deleted;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The question this choice belongs to.</summary>
	public TinyId QuestionId { get; private init; }

	/// <summary>
	///     The invariant code a conditional question names (ADR-0074). Derived from
	///     the choice's first wording and never changed — a reword is a label change,
	///     not a recode.
	/// </summary>
	public string Code { get; private init; }

	/// <summary>Where this choice sits among the question's choices.</summary>
	public int DisplayOrder { get; private set; }

	/// <summary>The English wording. Null only on a reporter-added choice typed in French.</summary>
	public string? LabelEn { get; private set; }

	/// <summary>The French wording. Null only on a reporter-added choice typed in English.</summary>
	public string? LabelFr { get; private set; }

	/// <summary>
	///     True when a reporter typed this choice into a type-ahead rather than an
	///     Administrator writing it. A curation mark, not a warning. See ADR-0063.
	/// </summary>
	public bool AddedByReporter { get; private init; }

	/// <summary>The language a reporter typed this choice in. Null for a choice an Administrator wrote.</summary>
	public Locale? ReporterLocale { get; private init; }

	/// <summary>When this choice was removed from the question, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>
	///     True while one language is missing — a reporter-added choice waiting for
	///     an Administrator to supply the other wording.
	/// </summary>
	public bool NeedsTranslation => LabelEn is null || LabelFr is null;

	/// <summary>
	///     This choice's wording in one locale, or in the other one when this locale
	///     has none yet. A one-language choice is offered in the language it has
	///     rather than not at all (ADR-0095).
	/// </summary>
	public string Label(Locale locale)
	{
		return (locale == Locale.FrCa ? LabelFr ?? LabelEn : LabelEn ?? LabelFr)!;
	}

	internal static QuestionChoice Written(TinyId questionId,
										   string code,
										   int displayOrder,
										   string labelEn,
										   string labelFr)
	{
		return new QuestionChoice(
			questionId, code, displayOrder, NotBlank(labelEn), NotBlank(labelFr), false, null, null);
	}

	internal static QuestionChoice FromReporter(TinyId questionId,
												string code,
												int displayOrder,
												string typed,
												Locale locale)
	{
		var label = NotBlank(typed);

		return new QuestionChoice(
			questionId, code, displayOrder,
			locale == Locale.FrCa ? null : label,
			locale == Locale.FrCa ? label : null,
			true, locale, null);
	}

	/// <summary>This row, removal and marks included, as a choice of another question — the replacement a fork creates.</summary>
	internal QuestionChoice CopyTo(TinyId questionId)
	{
		return new QuestionChoice(questionId, Code, DisplayOrder, LabelEn, LabelFr, AddedByReporter, ReporterLocale, Deleted);
	}

	/// <summary>
	///     Replaces the wording. A choice an Administrator wrote keeps both
	///     languages; a reporter-added one may keep one missing until someone
	///     supplies it, but never both.
	/// </summary>
	internal void Relabel(string? labelEn,
						  string? labelFr)
	{
		var en = Blank(labelEn) ? null : labelEn;
		var fr = Blank(labelFr) ? null : labelFr;

		if (!AddedByReporter)
		{
			en = NotBlank(en);
			fr = NotBlank(fr);
		}
		else if (en is null
				 && fr is null)
		{
			throw new DomainRuleViolationException("A choice needs wording in at least one official language.");
		}

		LabelEn = en;
		LabelFr = fr;
	}

	internal void MoveTo(int displayOrder)
	{
		DisplayOrder = displayOrder;
	}

	/// <summary>Removes this choice. Only a live choice is ever removed — see <see cref="Question.ReplaceChoices" />.</summary>
	internal void Delete(DateTimeOffset at)
	{
		Deleted = at;
	}

	/// <summary>
	///     Brings a removed choice back because an Administrator wrote it again. A
	///     code is unique on its question, removed rows included, so this row is
	///     the one to revive rather than a rival. A reporter never reaches this.
	/// </summary>
	internal void Restore(int displayOrder,
						  string? labelEn,
						  string? labelFr)
	{
		Relabel(labelEn, labelFr);
		Deleted = null;
		DisplayOrder = displayOrder;
	}

	private static bool Blank(string? label)
	{
		return string.IsNullOrWhiteSpace(label);
	}

	private static string NotBlank(string? label)
	{
		return Blank(label)
			? throw new DomainRuleViolationException("A choice needs wording in both official languages.")
			: label!;
	}
}
