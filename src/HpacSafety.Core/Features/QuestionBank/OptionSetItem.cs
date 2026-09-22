namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     One choice in a reusable <see cref="OptionSet" />, in both official languages.
/// </summary>
/// <remarks>
///     Unlike <see cref="QuestionRevisionOption" />, this row is editable — it is the
///     working list, not the record of what a reporter was shown. A revision built
///     from this set copies it; that copy is what answers point at, and it never
///     changes again. See ADR-0058.
/// </remarks>
public class OptionSetItem
{
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. It exists for
	// the ORM and for nothing else.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private OptionSetItem()
	{
	}
#pragma warning restore CS8618

	private OptionSetItem(
		TinyId optionSetId, string code, int displayOrder, string labelEn, string labelFr,
		bool addedByReporter, bool needsTranslation)
	{
		Id = TinyId.New();
		OptionSetId = optionSetId;
		Code = QuestionKey.Normalize(code);
		DisplayOrder = displayOrder;
		LabelEn = NotBlank(labelEn);
		LabelFr = NotBlank(labelFr);
		AddedByReporter = addedByReporter;
		NeedsTranslation = needsTranslation;
	}

	/// <summary>Surrogate key. A revision's snapshot records this as its source.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The set this choice belongs to.</summary>
	public TinyId OptionSetId { get; private init; }

	/// <summary>
	///     The invariant code stored against an answer. Never display text, and
	///     never changed — a relabel is a label change, not a recode.
	/// </summary>
	public string Code { get; private init; }

	/// <summary>Where this choice sits in the set. Changed by <see cref="OptionSet.Arrange" />.</summary>
	public int DisplayOrder { get; private set; }

	/// <summary>The English wording.</summary>
	public string LabelEn { get; private set; }

	/// <summary>The French wording.</summary>
	public string LabelFr { get; private set; }

	/// <summary>
	///     True when a reporter typed this choice into a type-ahead rather than an
	///     administrator authoring it.
	/// </summary>
	/// <remarks>
	///     It is a curation flag, not a warning: an administrator uses it to find
	///     the entries nobody has reviewed yet, to rename "mount 7" to "Mount 7",
	///     to merge a duplicate, or to remove something that should not have been
	///     added. See ADR-0063.
	/// </remarks>
	public bool AddedByReporter { get; private init; }

	/// <summary>
	///     True when one of the two labels is not a translation but a copy of the
	///     other, waiting for an administrator to supply the real wording.
	/// </summary>
	/// <remarks>
	///     A reporter types one language, and nothing on the submission path
	///     translates it (ADR-0072). Rather than leave a choice half-blank — which
	///     would show the other half of the membership an empty option — the typed
	///     words stand in for both languages and this flag says so, exactly as the
	///     development translator's copied output is labelled a stand-in rather
	///     than passed off as a translation.
	/// </remarks>
	public bool NeedsTranslation { get; private set; }

	/// <summary>When this choice was removed from the set, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>This choice's wording in one locale.</summary>
	public string Label(Locale locale)
	{
		return locale == Locale.FrCa ? LabelFr : LabelEn;
	}

	internal static OptionSetItem Create(
		TinyId optionSetId, string code, int displayOrder, string labelEn, string labelFr,
		bool addedByReporter = false, bool needsTranslation = false)
	{
		return new OptionSetItem(optionSetId, code, displayOrder, labelEn, labelFr, addedByReporter, needsTranslation);
	}

	/// <summary>
	///     Replaces both labels with an administrator's own wording, which settles
	///     any stand-in language a reporter's entry was carrying.
	/// </summary>
	internal void Relabel(string labelEn, string labelFr)
	{
		LabelEn = NotBlank(labelEn);
		LabelFr = NotBlank(labelFr);
		NeedsTranslation = false;
	}

	internal void MoveTo(int displayOrder)
	{
		DisplayOrder = displayOrder;
	}

	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}

	/// <summary>
	///     Brings a removed choice back rather than creating a second row claiming
	///     the same code — a code is unique in its set, and history already points
	///     at this row.
	/// </summary>
	internal void Restore(int displayOrder, string labelEn, string labelFr)
	{
		Deleted = null;
		DisplayOrder = displayOrder;
		Relabel(labelEn, labelFr);
	}

	private static string NotBlank(string label)
	{
		return string.IsNullOrWhiteSpace(label)
			? throw new DomainRuleViolationException("An option needs a label in both official languages.")
			: label;
	}
}
