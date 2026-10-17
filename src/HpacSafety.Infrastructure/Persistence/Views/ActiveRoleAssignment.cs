using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;

namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     A row of the <c>active_role_assignments</c> view: a role assignment not yet
///     revoked (ADR-0199). The role resolver and the manage-roles list read it.
///     Read-only; the revoker and the granter are not exposed.
/// </summary>
public sealed class ActiveRoleAssignment
{
	/// <summary>The assignment's key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The normalised email.</summary>
	public string Email { get; private init; } = string.Empty;

	/// <summary>Safety Officer or Administrator.</summary>
	public MemberRole Role { get; private init; }

	/// <summary>When it was granted.</summary>
	public DateTimeOffset GrantedAt { get; private init; }
}
