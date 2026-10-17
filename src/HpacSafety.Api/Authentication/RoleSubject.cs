using HpacSafety.Core.Features.RoleAssignments;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     Reads the email a member signed in with out of a token subject, and
///     normalises one. Only this API's own issuers embed an email in a subject
///     (<c>member:&lt;email&gt;</c> for the interim issuer, <c>dev:&lt;username&gt;</c>
///     for Development); a real provider's subject is opaque and yields none
///     (#911). The result is for a lookup only: it never reaches a log (CON-IF-011).
/// </summary>
public static class RoleSubject
{
	private const string InterimPrefix = "member:";
	private const string DevelopmentPrefix = "dev:";

	/// <summary>An email or username in the form every lookup compares: trimmed and lower-cased.</summary>
	public static string Normalise(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		return RoleAssignment.AsciiLower(value.Trim());
	}

	/// <summary>
	///     The normalised sign-in name inside a subject, or <c>null</c> when the
	///     subject carries none. Each prefix counts only under the issuer that
	///     mints it, so a real provider's subject can never pose as either.
	/// </summary>
	/// <param name="subject">The token subject.</param>
	/// <param name="development">Whether <c>dev:</c> subjects are this host's own (Development).</param>
	/// <param name="interim">Whether <c>member:</c> subjects are this host's own (interim issuer).</param>
	public static string? SignInNameOf(string? subject,
									   bool development,
									   bool interim)
	{
		if (string.IsNullOrWhiteSpace(subject))
		{
			return null;
		}

		if (development && subject.StartsWith(DevelopmentPrefix, StringComparison.Ordinal))
		{
			return NameAfter(subject, DevelopmentPrefix);
		}

		if (interim && subject.StartsWith(InterimPrefix, StringComparison.Ordinal))
		{
			return NameAfter(subject, InterimPrefix);
		}

		return null;
	}

	private static string? NameAfter(string subject,
									 string prefix)
	{
		var name = Normalise(subject[prefix.Length..]);

		// Only ASCII can match a stored email (RoleAssignment.Grant refuses the rest).
		return name.Length == 0 || name.Any(character => character > '\u007F') ? null : name;
	}

	/// <summary>Whether the subject is a Development one, whose name may be a fixed account.</summary>
	public static bool IsDevelopmentSubject(string? subject)
	{
		return subject is not null && subject.StartsWith(DevelopmentPrefix, StringComparison.Ordinal);
	}
}
