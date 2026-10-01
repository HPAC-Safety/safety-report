namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>What <see cref="QuestionGrouping.UngroupChildren" /> did.</summary>
/// <param name="Ungrouped">Each child as it is live afterwards: itself, revised, or its replacement.</param>
/// <param name="Replacements">The new questions that replaced an answered child; the caller adds them to the bank.</param>
/// <param name="Moved">How many other questions shifted to make room, each by a new revision.</param>
public sealed record UngroupResult(IReadOnlyList<Question> Ungrouped,
								   IReadOnlyList<Question> Replacements,
								   int Moved);
