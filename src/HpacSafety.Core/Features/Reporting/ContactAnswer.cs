using System.Text.RegularExpressions;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The written forms of an email and a phone answer (ADR-0137). The form holds
///     an email address to the same rule, in <c>src/web/src/lib/emailAddress.ts</c>.
/// </summary>
/// <remarks>
///     Only the phone number's E.164 shape is checked here, because <c>Core</c>
///     takes no runtime package. Whether the number is valid for its country is
///     the API's check, with libphonenumber.
/// </remarks>
public static partial class ContactAnswer
{
	/// <summary>The longest email address a mail system will carry (RFC 5321's path limit, less its brackets).</summary>
	public const int EmailMaxLength = 254;

	/// <summary>
	///     Whether <paramref name="value" /> is one email address: a non-empty local
	///     part and a domain joined by one <c>@</c>, no whitespace, and a domain of at
	///     least two non-empty dot-separated labels whose last is at least two
	///     characters long.
	/// </summary>
	public static bool IsEmailAddress(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return value.Length <= EmailMaxLength && EmailAddress().IsMatch(value);
	}

	/// <summary>
	///     Whether <paramref name="value" /> is written in E.164: <c>+</c>, then up to
	///     fifteen digits, the first not zero, and nothing else.
	/// </summary>
	public static bool IsE164(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return E164().IsMatch(value);
	}

	[GeneratedRegex(@"^[^\s@]+@(?:[^\s@.]+\.)+[^\s@.]{2,}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
	private static partial Regex EmailAddress();

	[GeneratedRegex(@"^\+[1-9][0-9]{1,14}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
	private static partial Regex E164();
}
