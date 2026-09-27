using System.Text.RegularExpressions;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     When two choice wordings are the same wording: after trimming, collapsing
///     each run of whitespace to one space, and ignoring case. A dependent
///     question offers each wording once, and a reporter's typed words are
///     matched this way (ADR-0151).
/// </summary>
public static partial class ChoiceWording
{
	/// <summary>The form two wordings are compared in, or null for no wording.</summary>
	public static string? Normalize(string? wording)
	{
		return string.IsNullOrWhiteSpace(wording)
			? null
			: Whitespace().Replace(wording.Trim(), " ").ToUpperInvariant();
	}

	/// <summary>Whether <paramref name="choice" /> reads as <paramref name="words" /> in either language.</summary>
	public static bool ReadsAs(QuestionChoice choice,
							   string? words)
	{
		ArgumentNullException.ThrowIfNull(choice);

		var normalized = Normalize(words);
		return normalized is not null
			   && (Normalize(choice.LabelEn) == normalized || Normalize(choice.LabelFr) == normalized);
	}

	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();
}
