using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Authorization;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     The member must hold at least this role <em>now</em>: resolved from the
///     Super Administrator, the role assignments (and Development's own sources)
///     on every request, never from the role a token was issued with, so a
///     revoked member loses access on their next request (ADR-0199, REQ-AUTH-021).
/// </summary>
/// <param name="Minimum">The lowest role that passes.</param>
public sealed record RoleRequirement(MemberRole Minimum) : IAuthorizationRequirement;

/// <summary>Evaluates a <see cref="RoleRequirement" /> through <see cref="IRoleResolver" />.</summary>
public sealed class RoleRequirementHandler(IRoleResolver roles) : AuthorizationHandler<RoleRequirement>
{
	/// <inheritdoc />
	protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
														 RoleRequirement requirement)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(requirement);

		var cancellation = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
		var role = await roles.Resolve(MemberRoles.SubjectOf(context.User), cancellation).ConfigureAwait(false);

		if (role >= requirement.Minimum)
		{
			context.Succeed(requirement);
		}
	}
}
