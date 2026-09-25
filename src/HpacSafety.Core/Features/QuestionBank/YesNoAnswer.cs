namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     The words a yes/no or checkbox answer is written in: <c>yes</c>/<c>no</c> in
///     English and <c>oui</c>/<c>non</c> in French, each stored in the reporter's own
///     language (ADR-0127).
/// </summary>
/// <remarks>
///     A French answer given before ADR-0127 is stored as <c>yes</c> or <c>no</c> and
///     is never rewritten, so every reader goes through <see cref="IsYes" /> and
///     <see cref="IsNo" />, which accept all four words. Comparing a boolean answer to
///     <c>"yes"</c> alone is a bug.
/// </remarks>
public static class YesNoAnswer
{
	/// <summary>The word for yes in <paramref name="locale" />.</summary>
	public static string Yes(Locale locale)
	{
		return locale == Locale.FrCa ? "oui" : "yes";
	}

	/// <summary>The word for no in <paramref name="locale" />.</summary>
	public static string No(Locale locale)
	{
		return locale == Locale.FrCa ? "non" : "no";
	}

	/// <summary>Whether <paramref name="value" /> is yes or no written in <paramref name="locale" />.</summary>
	public static bool IsWordIn(string value,
								Locale locale)
	{
		return string.Equals(value, Yes(locale), StringComparison.Ordinal)
			   || string.Equals(value, No(locale), StringComparison.Ordinal);
	}

	/// <summary>Whether a stored answer means yes, in either language.</summary>
	public static bool IsYes(string? value)
	{
		return value is "yes" or "oui";
	}

	/// <summary>Whether a stored answer means no, in either language.</summary>
	public static bool IsNo(string? value)
	{
		return value is "no" or "non";
	}

	/// <summary>
	///     The same answer in the other official language — a fixed pair, never a
	///     translation — or null for anything that is not yes or no.
	/// </summary>
	public static string? Counterpart(string? value)
	{
		return value switch
		{
			"yes" => "oui",
			"oui" => "yes",
			"no" => "non",
			"non" => "no",
			_ => null,
		};
	}
}
