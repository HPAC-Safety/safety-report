using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Authentication;

/// <summary>Resolves the role a member holds right now (ADR-0199, ADR-0200).</summary>
public interface IRoleResolver
{
	/// <summary>
	///     The role for a token subject, always at least <see cref="MemberRole.User" />.
	///     Never throws for an unreadable store: that fails closed to User, except
	///     for the Super Administrator, who never depends on the store.
	/// </summary>
	Task<MemberRole> Resolve(string? subject,
							 CancellationToken cancellationToken);
}

/// <summary>
///     The one place a role is decided. In order: the Super Administrator
///     (configuration, ADR-0200); in Development only, the fixed accounts and the
///     <see cref="MembersSiteLoginOptions" /> lists (ADR-0079); the member's active
///     role assignment. The highest role wins, and email matching is
///     case-insensitive. An unreadable store resolves to User (REQ-AUTH-023).
/// </summary>
/// <remarks>
///     No email, subject, or exception message is ever logged (CON-IF-011).
/// </remarks>
public sealed partial class RoleResolver(
	HpacSafetyDbContext database,
	IOptions<HpacAuthenticationOptions> authentication,
	IOptions<MembersSiteLoginOptions> membersSite,
	RoleResolverMode mode,
	ILogger<RoleResolver> logger) : IRoleResolver
{
	/// <inheritdoc />
	public async Task<MemberRole> Resolve(string? subject,
										  CancellationToken cancellationToken)
	{
		var name = RoleSubject.SignInNameOf(subject, mode.IncludeDevelopmentSources, mode.InterimIssuerEnabled);

		if (name is null)
		{
			return MemberRole.User;
		}

		if (IsSuperAdministrator(name))
		{
			return MemberRole.Administrator;
		}

		var role = MemberRole.User;

		if (mode.IncludeDevelopmentSources)
		{
			role = Max(role, DevelopmentRole(subject, name));
		}

		try
		{
			var assigned = await database.ActiveRoleAssignments
				.AsNoTracking()
				.Where(assignment => assignment.Email == name)
				.Select(assignment => (MemberRole?)assignment.Role)
				.FirstOrDefaultAsync(cancellationToken)
				.ConfigureAwait(false);

			if (assigned is not null)
			{
				role = Max(role, assigned.Value);
			}
		}
		catch (Exception cause) when (cause is not OperationCanceledException)
		{
			// Fails closed to whatever the other sources gave (User outside
			// Development). Logs the exception's type only: its message can echo
			// a value (CON-IF-011).
			LogUnreadable(logger, cause.GetType().Name);
		}

		return role;
	}

	private bool IsSuperAdministrator(string name)
	{
		var configured = authentication.Value.SuperAdmin;

		return !string.IsNullOrWhiteSpace(configured) && RoleSubject.Normalise(configured) == name;
	}

	private MemberRole DevelopmentRole(string? subject,
									   string name)
	{
		var role = MemberRole.User;

		if (RoleSubject.IsDevelopmentSubject(subject))
		{
			// Fixed account names are case-sensitive and not emails.
			var rawName = subject!["dev:".Length..];
			role = Max(role, FixedAccountCredentialSource.RoleOf(rawName) ?? MemberRole.User);
		}

		var lists = membersSite.Value;

		if (lists.AdministratorEmails.Any(email => RoleSubject.Normalise(email) == name))
		{
			role = Max(role, MemberRole.Administrator);
		}
		else if (lists.SafetyOfficerEmails.Any(email => RoleSubject.Normalise(email) == name))
		{
			role = Max(role, MemberRole.SafetyOfficer);
		}

		return role;
	}

	private static MemberRole Max(MemberRole a,
								  MemberRole b)
	{
		return a >= b ? a : b;
	}

	[LoggerMessage(Level = LogLevel.Warning, Message = "Role assignments could not be read ({ExceptionType}); resolving the member as a User.")]
	private static partial void LogUnreadable(ILogger logger,
											  string exceptionType);
}

/// <summary>Which issuer's subjects this host knows how to read, and whether Development's role sources apply.</summary>
/// <param name="IncludeDevelopmentSources">True only in Development: its subjects, fixed accounts, and lists.</param>
/// <param name="InterimIssuerEnabled">True only where the temporary interim issuer is enabled.</param>
public sealed record RoleResolverMode(bool IncludeDevelopmentSources, bool InterimIssuerEnabled);
