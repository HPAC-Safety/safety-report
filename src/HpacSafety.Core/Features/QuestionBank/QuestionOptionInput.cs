namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     One choice in the complete ordered list an Administrator saves for a
///     question. See <see cref="Question.ReplaceChoices" /> and ADR-0095.
/// </summary>
/// <param name="Code">The invariant code the choice is recorded under. Never display text.</param>
/// <param name="LabelEn">
///     The English wording. Required on a choice an Administrator writes; may stay
///     null on a reporter-added choice typed in French until someone supplies it.
/// </param>
/// <param name="LabelFr">The French wording, under the same rule as <paramref name="LabelEn" />.</param>
public sealed record QuestionOptionInput(string Code, string? LabelEn, string? LabelFr);
