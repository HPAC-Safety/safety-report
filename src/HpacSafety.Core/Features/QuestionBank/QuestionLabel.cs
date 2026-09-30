namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     The one rule about a question label's closing colon: a label is stored
///     without it, and the interface adds it in the reader's locale, <c>Label:</c>
///     in English and <c>Label :</c> in French (ADR-0181).
/// </summary>
public static class QuestionLabel
{
	/// <summary>
	///     Whether the label, ignoring trailing whitespace, ends in a colon. A
	///     no-break or narrow no-break space before it (French typography) counts as
	///     whitespace.
	/// </summary>
	public static bool EndsWithColon(string label)
	{
		ArgumentNullException.ThrowIfNull(label);

		return label.TrimEnd().EndsWith(':');
	}

	/// <summary>
	///     The label without a trailing <c>:</c> or <c> :</c> (any space kind), and
	///     without the space before it. A label with no trailing colon is returned
	///     unchanged.
	/// </summary>
	public static string WithoutTrailingColon(string label)
	{
		ArgumentNullException.ThrowIfNull(label);

		return EndsWithColon(label) ? label.TrimEnd().TrimEnd(':').TrimEnd() : label;
	}
}
