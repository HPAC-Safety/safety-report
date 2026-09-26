namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     One choice a single-select, multi-select, or type-ahead question offers.
///     Owned by the <see cref="Question" />, not by any revision of it, and
///     editable in place — see ADR-0095.
/// </summary>
/// <remarks>
///     <para>
///         Editing a choice never revises or forks its question. An answer names its
///         choice by <see cref="Id" /> and reads its wording from here (ADR-0128), so
///         a choice any answer names is stamped <see cref="Deleted" /> when removed,
///         never erased, and a fork copies every row — removed ones included — onto
///         the replacement as new rows.
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
		LabelEnSource = labelEn is null ? null : LabelSource.Human;
		LabelFrSource = labelFr is null ? null : LabelSource.Human;
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

	/// <summary>How <see cref="LabelEn" /> was produced; null while there is none. See ADR-0129.</summary>
	public LabelSource? LabelEnSource { get; private set; }

	/// <summary>How <see cref="LabelFr" /> was produced; null while there is none. See ADR-0129.</summary>
	public LabelSource? LabelFrSource { get; private set; }

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
	///     When a reporter added this value. Null for a choice an Administrator
	///     wrote, and for one added before this was recorded.
	/// </summary>
	public DateTimeOffset? CreatedAt { get; private init; }

	/// <summary>
	///     True while a Safety Officer or Administrator has yet to review this
	///     type-ahead value: set when a reporter adds it, and again when a reporter
	///     types it after it was removed. See ADR-0129.
	/// </summary>
	public bool NeedsReview { get; private set; }

	/// <summary>When this value was last reviewed: approved, corrected, or removed.</summary>
	public DateTimeOffset? ReviewedAt { get; private set; }

	/// <summary>
	///     Who last reviewed it, as the subject of their validated token — opaque,
	///     and joined to nothing (ADR-0065).
	/// </summary>
	public string? ReviewedBy { get; private set; }

	/// <summary>
	///     The picker option that replaced this one, when an Administrator replaced
	///     it rather than fixing its wording. A retired choice keeps every answer
	///     given under it; a condition naming it follows this link (ADR-0128).
	/// </summary>
	public TinyId? ReplacedByChoiceId { get; private set; }

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

	/// <summary>
	///     This choice's wording in the language other than <paramref name="locale" />,
	///     or null while it has only one language. What a choice answer shows as its
	///     second language (ADR-0112, ADR-0128).
	/// </summary>
	public string? OtherLabel(Locale locale)
	{
		return LabelEn is not null && LabelFr is not null ? Label(locale.Counterpart) : null;
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
												Locale locale,
												DateTimeOffset? at)
	{
		var label = NotBlank(typed);

		return new QuestionChoice(
			questionId, code, displayOrder,
			locale == Locale.FrCa ? null : label,
			locale == Locale.FrCa ? label : null,
			true, locale, null)
		{
			CreatedAt = at,
			NeedsReview = true,
		};
	}

	/// <summary>
	///     This row, removal and marks included, as a choice of another question — the
	///     replacement a fork creates. Its replaced-by link names a row of this
	///     question, so the fork re-points it at the copy (<see cref="RelinkReplacement" />).
	/// </summary>
	internal QuestionChoice CopyTo(TinyId questionId)
	{
		return new QuestionChoice(questionId, Code, DisplayOrder, LabelEn, LabelFr, AddedByReporter, ReporterLocale, Deleted)
		{
			LabelEnSource = LabelEnSource,
			LabelFrSource = LabelFrSource,
			CreatedAt = CreatedAt,
			NeedsReview = NeedsReview,
			ReviewedAt = ReviewedAt,
			ReviewedBy = ReviewedBy,
		};
	}

	/// <summary>
	///     Supplies the one language this choice is missing, mechanically — the
	///     Worker translating a value a reporter typed (ADR-0129). The wording the
	///     reporter typed is never touched, and a choice that already has both
	///     languages is left as it is: a person got there first.
	/// </summary>
	/// <returns>Whether a language was supplied.</returns>
	public bool SupplyAutoTranslation(string translated)
	{
		if (string.IsNullOrWhiteSpace(translated))
		{
			throw new DomainRuleViolationException("A supplied translation cannot be blank.");
		}

		// A choice always has at least one language, so the one missing, if any,
		// is the one to fill.
		if (LabelEn is null)
		{
			LabelEn = translated;
			LabelEnSource = LabelSource.Auto;
			return true;
		}

		if (LabelFr is null)
		{
			LabelFr = translated;
			LabelFrSource = LabelSource.Auto;
			return true;
		}

		return false;
	}

	/// <summary>A reporter used this value again after it was removed: a reviewer should see it is still in use (ADR-0129).</summary>
	internal void FlagForReview()
	{
		NeedsReview = true;
	}

	/// <summary>Records a review — an approval, a correction, or a removal — and clears the flag.</summary>
	internal void MarkReviewed(string reviewer,
							   DateTimeOffset at)
	{
		if (string.IsNullOrWhiteSpace(reviewer))
		{
			throw new DomainRuleViolationException("A review needs the reviewer's token subject.");
		}

		NeedsReview = false;
		ReviewedAt = at;
		ReviewedBy = reviewer;
	}

	/// <summary>Points this copy's replaced-by link at the copy of the choice that replaced the original.</summary>
	internal void RelinkReplacement(TinyId? replacedBy)
	{
		ReplacedByChoiceId = replacedBy;
	}

	/// <summary>Retires this choice in favour of <paramref name="replacement" /> (ADR-0128).</summary>
	internal void ReplaceWith(QuestionChoice replacement,
							  DateTimeOffset at)
	{
		Deleted = at;
		ReplacedByChoiceId = replacement.Id;
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

		// A language a person rewrites is theirs; one left as it was keeps how it
		// was produced, so resaving the editor never passes a machine
		// translation off as written.
		LabelEnSource = en is null ? null : en == LabelEn ? LabelEnSource : LabelSource.Human;
		LabelFrSource = fr is null ? null : fr == LabelFr ? LabelFrSource : LabelSource.Human;
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
		ReplacedByChoiceId = null;
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
