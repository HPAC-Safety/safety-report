using System.Globalization;
using System.Text;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     A question's stable identity in text: lowercase, underscore-separated,
///     invariant. Keys are generated once and never change, because answers and
///     exports refer to them.
/// </summary>
public static class QuestionKey
{
	/// <summary>The key of the one question this system will not run without.</summary>
	public const string ConsentPublish = "consent_publish";

	/// <summary>
	///     Normalizes a candidate key. Rejects an empty result rather than inventing
	///     one, because a key nobody chose is a key nobody can find again.
	/// </summary>
	/// <remarks>
	///     Accents are folded rather than dropped, so a choice a reporter typed in
	///     French keeps its letters: "Élévation" becomes <c>elevation</c>, not
	///     <c>l_vation</c>. A candidate that was already a key is unchanged.
	/// </remarks>
	public static string Normalize(string candidate)
	{
		ArgumentNullException.ThrowIfNull(candidate);

		var builder = new StringBuilder(candidate.Length);
		foreach (var character in Folded(candidate.Trim().ToLowerInvariant()))
		{
			if (char.IsAsciiLetterOrDigit(character))
			{
				builder.Append(character);
			}
			else if (builder.Length > 0
					 && builder[^1] != '_')
			{
				builder.Append('_');
			}
		}

		var key = builder.ToString().Trim('_');

		return key.Length == 0
			? throw new DomainRuleViolationException($"'{candidate}' does not reduce to a usable question key.")
			: key;
	}

	/// <summary>Decomposes accented letters and keeps only their base letter.</summary>
	private static string Folded(string text)
	{
		var builder = new StringBuilder(text.Length);
		foreach (var character in text.Replace("œ", "oe", StringComparison.Ordinal).Replace("æ", "ae", StringComparison.Ordinal)
					 .Normalize(NormalizationForm.FormD))
		{
			if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
			{
				builder.Append(character);
			}
		}

		return builder.ToString();
	}
}
