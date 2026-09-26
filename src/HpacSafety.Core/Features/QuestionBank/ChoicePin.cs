namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     Where a <see cref="QuestionChoice" /> is listed relative to the alphabetical
///     rest. Stored as an invariant code, like every other domain enum. See ADR-0136.
/// </summary>
/// <remarks>
///     A list shows its pinned-first choices, then its unpinned ones, then its
///     pinned-last ones; each group is sorted alphabetically in the reader's
///     language by the browser, which alone knows that language.
/// </remarks>
public enum ChoicePin
{
	/// <summary>Not pinned: listed alphabetically among the other unpinned choices. The default.</summary>
	None = 0,

	/// <summary>Pinned to the top, above every unpinned choice — "Canada" in a list of countries.</summary>
	First = 1,

	/// <summary>Pinned to the bottom, below every unpinned choice — "Other", "Unknown", "None of the above".</summary>
	Last = 2,
}

/// <summary>Where a <see cref="ChoicePin" /> puts its choice in a list.</summary>
public static class ChoicePinOrder
{
	/// <summary>The group a choice is listed in: 0 pinned first, 1 unpinned, 2 pinned last (ADR-0136).</summary>
	public static int Group(this ChoicePin pin)
	{
		return pin switch
		{
			ChoicePin.First => 0,
			ChoicePin.None => 1,
			_ => 2,
		};
	}
}
