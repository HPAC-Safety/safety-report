
namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// One option supplied when a revision is created or revised. A revision is
/// born with its complete ordered option set — options are never added to, or
/// reordered on, a revision that already exists; changing the set at all
/// means creating a new revision. See <see cref="QuestionRevision"/>.
/// </summary>
/// <param name="Code">The invariant code stored against an answer. Never display text.</param>
/// <param name="LabelEn">The English wording.</param>
/// <param name="LabelFr">The French wording.</param>
public sealed record QuestionOptionInput(string Code, string LabelEn, string LabelFr);
