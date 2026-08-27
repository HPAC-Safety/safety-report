
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// One choice on a select-style question revision, in both official languages.
/// The <see cref="Code"/> is invariant and never changes — it is what every
/// historical answer points at, so a rename is impossible: a relabel requires a
/// new revision, the same as any other wording change.
/// </summary>
public class QuestionRevisionOption
{
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private QuestionRevisionOption()
    {
    }
#pragma warning restore CS8618

    private QuestionRevisionOption(TinyId questionRevisionId, string code, int displayOrder, string labelEn, string labelFr)
    {
        Id = TinyId.New();
        QuestionRevisionId = questionRevisionId;
        Code = QuestionKey.Normalize(code);
        DisplayOrder = displayOrder;
        LabelEn = NotBlank(labelEn);
        LabelFr = NotBlank(labelFr);
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The revision this option belongs to.</summary>
    public TinyId QuestionRevisionId { get; private init; }

    /// <summary>The invariant code stored against an answer. Never display text.</summary>
    public string Code { get; private init; }

    /// <summary>
    /// Where this option sits among its siblings. Fixed at creation: the
    /// complete ordered option set belongs to the revision it was born with,
    /// and reordering options means creating a new revision with a new list,
    /// not moving one in place.
    /// </summary>
    public int DisplayOrder { get; private init; }

    /// <summary>When this option was deleted along with its revision, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>The English wording.</summary>
    public string LabelEn { get; private init; }

    /// <summary>The French wording.</summary>
    public string LabelFr { get; private init; }

    /// <summary>This option's wording in one locale.</summary>
    public string Label(Locale locale) => locale == Locale.FrCa ? LabelFr : LabelEn;

    internal static QuestionRevisionOption Create(
        TinyId questionRevisionId, string code, int displayOrder, string labelEn, string labelFr) =>
        new(questionRevisionId, code, displayOrder, labelEn, labelFr);

    private static string NotBlank(string label) =>
        string.IsNullOrWhiteSpace(label)
            ? throw new DomainRuleViolationException("A question option needs a label in both official languages.")
            : label;
}
