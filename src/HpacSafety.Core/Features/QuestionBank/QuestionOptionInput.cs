
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
/// <param name="SourceItemId">
/// The <see cref="OptionSetItem"/> this option was copied from, when the
/// revision was built from a shared <see cref="OptionSet"/>. Provenance only:
/// it lets the authoring UI say where a snapshot came from and offer to
/// refresh it. Nothing consults it to render or validate an answer — the
/// snapshot on the revision is the whole truth. See ADR-0058.
/// </param>
public sealed record QuestionOptionInput(string Code, string LabelEn, string LabelFr, TinyId? SourceItemId = null);
