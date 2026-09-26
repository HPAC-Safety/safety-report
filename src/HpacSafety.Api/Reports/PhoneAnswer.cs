using HpacSafety.Core.Features.Reporting;
using PhoneNumbers;

namespace HpacSafety.Api.Reports;

/// <summary>
///     Whether a phone answer names a real number (ADR-0137): written in E.164, and
///     valid for the country its calling code names by Google's libphonenumber
///     metadata — the rules the form's libphonenumber-js applies to the country the
///     reporter chose.
/// </summary>
public static class PhoneAnswer
{
	private static readonly PhoneNumberUtil Numbers = PhoneNumberUtil.GetInstance();

	/// <summary>Whether <paramref name="value" /> is an E.164 number valid for its country.</summary>
	public static bool IsValid(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		if (!ContactAnswer.IsE164(value))
		{
			return false;
		}

		try
		{
			return Numbers.IsValidNumber(Numbers.Parse(value, null));
		}
		catch (NumberParseException)
		{
			return false;
		}
	}
}
