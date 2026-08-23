namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// One labelled field of a report, on its way into or out of the deterministic
/// scrub.
/// </summary>
/// <param name="Kind">How the scrub must handle this field.</param>
/// <param name="Label">
/// The field's label in the reporter's own language. Labels come from the
/// question bank, which is data, so the scrub never matches on label text — a
/// rule keyed on the literal string "Where:" would stop working the moment an
/// administrator reworded the question or a report arrived in French.
/// </param>
/// <param name="Value">What the reporter wrote. May be absent.</param>
public sealed record ScrubField(ScrubFieldKind Kind, string Label, string? Value);
