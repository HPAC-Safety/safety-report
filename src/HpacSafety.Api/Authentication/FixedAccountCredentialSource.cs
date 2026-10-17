using HpacSafety.Core.Features.Moderation;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     The three fixed development accounts from ADR-0066 — one per role, the
///     password matching the username because it is memorable, obviously
///     synthetic, and nothing here is a credential.
/// </summary>
public sealed class FixedAccountCredentialSource : IDevelopmentCredentialSource
{
	private static readonly (string User, string Password, MemberRole Role)[] Accounts =
	[
		("admin", "admin", MemberRole.Administrator),
		("officer", "officer", MemberRole.SafetyOfficer),
		("user", "user", MemberRole.User),
	];

	/// <summary>
	///     The role a fixed account holds, by username, or <c>null</c> when the
	///     name is not one. Development's role resolver uses this on every
	///     request (ADR-0199).
	/// </summary>
	public static MemberRole? RoleOf(string username)
	{
		var match = Accounts.FirstOrDefault(account => string.Equals(account.User, username, StringComparison.Ordinal));

		return match.User is null ? null : match.Role;
	}

	/// <inheritdoc />
	public Task<MemberRole?> Verify(string username,
									string password,
									CancellationToken cancellationToken)
	{
		var match = Accounts.FirstOrDefault(account =>
			string.Equals(account.User, username, StringComparison.Ordinal)
			&& string.Equals(account.Password, password, StringComparison.Ordinal));

		return Task.FromResult<MemberRole?>(match.User is null ? null : match.Role);
	}
}
