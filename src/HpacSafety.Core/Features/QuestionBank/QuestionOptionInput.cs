namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     One choice in the complete list an Administrator saves for a question. See
///     <see cref="Question.ReplaceChoices" />, ADR-0095, and ADR-0136.
/// </summary>
/// <param name="Code">The invariant code the choice is recorded under. Never display text.</param>
/// <param name="LabelEn">
///     The English wording. Required on a choice an Administrator writes; may stay
///     null on a reporter-added choice typed in French until someone supplies it.
/// </param>
/// <param name="LabelFr">The French wording, under the same rule as <paramref name="LabelEn" />.</param>
/// <param name="Replace">
///     For a picker option this question already has: true retires that option
///     and adds a new one with this wording in its place, so earlier answers keep
///     the old option; false fixes its wording in place, for every answer
///     (ADR-0128). Ignored for a new option.
/// </param>
/// <param name="Pin">
///     Whether the choice is listed before or after the alphabetical rest, or among
///     them — the default (ADR-0136).
/// </param>
public sealed record QuestionOptionInput(
	string Code,
	string? LabelEn,
	string? LabelFr,
	bool Replace = false,
	ChoicePin Pin = ChoicePin.None);
