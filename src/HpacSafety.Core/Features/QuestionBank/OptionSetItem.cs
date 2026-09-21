namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// One choice in a reusable <see cref="OptionSet"/>, in both official languages.
/// </summary>
/// <remarks>
/// Unlike <see cref="QuestionRevisionOption"/>, this row is editable — it is the
/// working list, not the record of what a reporter was shown. A revision built
/// from this set copies it; that copy is what answers point at, and it never
/// changes again. See ADR-0058.
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
        TinyId optionSetId, string code, int displayOrder, string labelEn, string labelFr, bool addedByReporter)
    {
        Id = TinyId.New();
        OptionSetId = optionSetId;
        Code = QuestionKey.Normalize(code);
        DisplayOrder = displayOrder;
        LabelEn = NotBlank(labelEn);
        LabelFr = NotBlank(labelFr);
        AddedByReporter = addedByReporter;
    }

    /// <summary>Surrogate key. A revision's snapshot records this as its source.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The set this choice belongs to.</summary>
    public TinyId OptionSetId { get; private init; }

    /// <summary>
    /// The invariant code stored against an answer. Never display text, and
    /// never changed — a relabel is a label change, not a recode.
    /// </summary>
    public string Code { get; private init; }

    /// <summary>Where this choice sits in the set. Changed by <see cref="OptionSet.Arrange"/>.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>The English wording.</summary>
    public string LabelEn { get; private set; }

    /// <summary>The French wording.</summary>
    public string LabelFr { get; private set; }

    /// <summary>
    /// True when a reporter typed this choice into a type-ahead rather than an
    /// administrator authoring it.
    /// </summary>
    /// <remarks>
    /// It is a curation flag, not a warning: an administrator uses it to find
    /// the entries nobody has reviewed yet, to rename "mount 7" to "Mount 7",
    /// to merge a duplicate, or to remove something that should not have been
    /// added. The French on a reporter-added item was machine-drafted at
    /// submission, so it is the wording most worth a second look. See ADR-0063.
    /// </remarks>
    public bool AddedByReporter { get; private init; }

    /// <summary>When this choice was removed from the set, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>This choice's wording in one locale.</summary>
    public string Label(Locale locale) => locale == Locale.FrCa ? LabelFr : LabelEn;

    internal static OptionSetItem Create(
        TinyId optionSetId, string code, int displayOrder, string labelEn, string labelFr,
        bool addedByReporter = false) =>
        new(optionSetId, code, displayOrder, labelEn, labelFr, addedByReporter);

    internal void Relabel(string labelEn, string labelFr)
    {
        LabelEn = NotBlank(labelEn);
        LabelFr = NotBlank(labelFr);
    }

    internal void MoveTo(int displayOrder) => DisplayOrder = displayOrder;

    internal void Delete(DateTimeOffset at) => Deleted ??= at;

    /// <summary>
    /// Brings a removed choice back rather than creating a second row claiming
    /// the same code — a code is unique in its set, and history already points
    /// at this row.
    /// </summary>
    internal void Restore(int displayOrder, string labelEn, string labelFr)
    {
        Deleted = null;
        DisplayOrder = displayOrder;
        Relabel(labelEn, labelFr);
    }

    private static string NotBlank(string label) =>
        string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("An option needs a label in both official languages.")
            : label;
}
