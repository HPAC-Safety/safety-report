using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.RoleAssignments;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Admin;

/// <summary>
///     The manage-roles endpoints: an Administrator lists, adds, and removes
///     (revokes) role assignments (ADR-0199, REQ-AUTH-012 to REQ-AUTH-020).
/// </summary>
/// <remarks>
///     No email reaches a log (CON-IF-011). The duplicate check runs before the
///     insert, and the insert itself is <c>ON CONFLICT DO NOTHING</c>, so a racing
///     duplicate is a refusal and never a database error whose detail echoes the
///     email.
/// </remarks>
public static class RoleAssignmentEndpoints
{
	private const string TargetType = "RoleAssignment";
	private const string InvalidType = "https://hpac.ca/problems/invalid-role-assignment";

	/// <summary>Maps the role-assignment endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminRoleAssignments(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/role-assignments").RequireAuthorization(HpacPolicies.Administrator);

		group.MapGet("", List);
		group.MapPost("", Assign);
		group.MapPost("{id}/revoke", Revoke);

		return group;
	}

	private static async Task<IResult> List(
		HttpContext context,
		RoleResolverMode mode,
		HpacSafetyDbContext database,
		IOptions<HpacAuthenticationOptions> authentication,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken)
	{
		var caller = RoleSubject.SignInNameOf(MemberRoles.SubjectOf(context.User), mode.IncludeDevelopmentSources, mode.InterimIssuerEnabled);
		var configured = authentication.Value.SuperAdmin;
		var superAdministrator = string.IsNullOrWhiteSpace(configured)
			? null
			: new SuperAdministratorView(RoleSubject.Normalise(configured));

		try
		{
			var rows = await database.ActiveRoleAssignments
				.AsNoTracking()
				.OrderBy(assignment => assignment.GrantedAt)
				.ThenBy(assignment => assignment.Email)
				.ToListAsync(cancellationToken)
				.ConfigureAwait(false);

			return Results.Ok(new RoleAssignmentListResponse(
				superAdministrator,
				[.. rows.Select(row => new RoleAssignmentView(row.Id.Value, row.Email, EnumCode.Of(row.Role), row.GrantedAt, caller == row.Email))],
				AssignmentsUnavailable: false));
		}
		catch (Exception cause) when (cause is not OperationCanceledException)
		{
			// The Super Administrator is still listed (REQ-AUTH-018). Only the
			// exception's type is logged: its message can echo a value.
			RoleAssignmentLog.Unreadable(loggerFactory.CreateLogger(nameof(RoleAssignmentEndpoints)), cause.GetType().Name);

			return Results.Ok(new RoleAssignmentListResponse(superAdministrator, null, AssignmentsUnavailable: true));
		}
	}

	private static async Task<IResult> Assign(
		[FromBody] AssignRoleRequest request,
		HttpContext context,
		HpacSafetyDbContext database,
		IOptions<HpacAuthenticationOptions> authentication,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!EnumCode.TryParse<MemberRole>(request.Role, out var role)
			|| role is not (MemberRole.SafetyOfficer or MemberRole.Administrator))
		{
			return Invalid("invalid-role", "Choose Safety Officer or Administrator.");
		}

		var actor = MemberRoles.SubjectOf(context.User) ?? "(unknown)";
		var now = clock.GetUtcNow();

		RoleAssignment assignment;

		try
		{
			assignment = RoleAssignment.Grant(request.Email ?? string.Empty, role, actor, now);
		}
		catch (DomainRuleViolationException)
		{
			return Invalid("invalid-email", "Enter an email address.");
		}

		var superAdministrator = authentication.Value.SuperAdmin;

		if (!string.IsNullOrWhiteSpace(superAdministrator)
			&& RoleSubject.Normalise(superAdministrator) == assignment.Email)
		{
			return Invalid("super-administrator", "That email address is the Super Administrator, who is always an Administrator.");
		}

		var email = assignment.Email;

		if (await database.ActiveRoleAssignments.AsNoTracking()
				.AnyAsync(active => active.Email == email, cancellationToken).ConfigureAwait(false))
		{
			return Invalid("duplicate", "That email address already has a role assignment. Remove it first to change its role.");
		}

		var roleCode = EnumCode.Of(assignment.Role);
		var id = assignment.Id.Value;

		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		// ON CONFLICT DO NOTHING: a racing duplicate inserts nothing, rather than
		// raising a unique violation whose detail names the email (CON-IF-011).
		var inserted = await database.Database.ExecuteSqlInterpolatedAsync(
			$"""
			 INSERT INTO role_assignments (id, email, role, granted_at, granted_by_subject)
			 VALUES ({id}, {email}, {roleCode}, {assignment.GrantedAt}, {actor})
			 ON CONFLICT (email) WHERE revoked_at IS NULL DO NOTHING
			 """,
			cancellationToken).ConfigureAwait(false);

		if (inserted == 0)
		{
			return Invalid("duplicate", "That email address already has a role assignment. Remove it first to change its role.");
		}

		database.AuditLog.Add(new AuditLogEntry(actor, AuditAction.AssignedRole, TargetType, assignment.Id, now));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

		return Results.Json(
			new RoleAssignmentView(id, email, roleCode, assignment.GrantedAt, IsSelf: false),
			statusCode: StatusCodes.Status201Created);
	}

	private static async Task<IResult> Revoke(
		string id,
		HttpContext context,
		HpacSafetyDbContext database,
		RoleResolverMode mode,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(id, out var assignmentId))
		{
			return NotFound();
		}

		var target = await database.ActiveRoleAssignments
			.AsNoTracking()
			.Where(active => active.Id == assignmentId)
			.Select(active => active.Email)
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (target is null)
		{
			return NotFound();
		}

		var actor = MemberRoles.SubjectOf(context.User) ?? "(unknown)";

		if (RoleSubject.SignInNameOf(actor, mode.IncludeDevelopmentSources, mode.InterimIssuerEnabled) == target)
		{
			return Invalid("self", "You cannot remove your own role assignment.");
		}

		var now = clock.GetUtcNow();

		await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		// Guarded on revoked_at IS NULL: the one update the database allows, and
		// a concurrent second revoke changes nothing instead of failing.
		var revoked = await database.RoleAssignments
			.Where(assignment => assignment.Id == assignmentId && assignment.RevokedAt == null)
			.ExecuteUpdateAsync(
				set => set
					.SetProperty(assignment => assignment.RevokedAt, now)
					.SetProperty(assignment => assignment.RevokedBySubject, actor),
				cancellationToken)
			.ConfigureAwait(false);

		if (revoked == 0)
		{
			return NotFound();
		}

		database.AuditLog.Add(new AuditLogEntry(actor, AuditAction.RevokedRole, TargetType, assignmentId, now));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}

	/// <summary>
	///     A 400 whose <c>code</c> extension is a stable code the browser localizes:
	///     <c>invalid-email</c>, <c>invalid-role</c>, <c>duplicate</c>,
	///     <c>super-administrator</c>, or <c>self</c>. The detail is a
	///     plain-English fallback and never carries the email.
	/// </summary>
	private static IResult Invalid(string code,
								   string detail)
	{
		return Results.Problem(
			title: "That role assignment is not valid.",
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: InvalidType,
			extensions: new Dictionary<string, object?> { ["code"] = code });
	}

	private static IResult NotFound()
	{
		return Results.Problem(
			title: "No such role assignment.",
			statusCode: StatusCodes.Status404NotFound,
			type: "https://hpac.ca/problems/role-assignment-not-found",
			extensions: new Dictionary<string, object?> { ["code"] = "not-found" });
	}
}

internal static partial class RoleAssignmentLog
{
	[LoggerMessage(Level = LogLevel.Warning, Message = "Role assignments could not be read ({ExceptionType}).")]
	public static partial void Unreadable(ILogger logger,
										  string exceptionType);
}

/// <summary>The body of a request to assign a role.</summary>
/// <param name="Email">The email address, as typed.</param>
/// <param name="Role">The role code: <c>safety_officer</c> or <c>administrator</c>.</param>
public sealed record AssignRoleRequest(string? Email, string? Role);

/// <summary>One active role assignment.</summary>
/// <param name="Id">The assignment's id.</param>
/// <param name="Email">The normalised email.</param>
/// <param name="Role">The role code.</param>
/// <param name="GrantedAt">When it was granted.</param>
/// <param name="IsSelf">True on the caller's own row, which cannot be removed.</param>
public sealed record RoleAssignmentView(string Id, string Email, string Role, DateTimeOffset GrantedAt, bool IsSelf);

/// <summary>The permanent Super Administrator.</summary>
/// <param name="Email">The configured email, normalised.</param>
public sealed record SuperAdministratorView(string Email);

/// <summary>The manage-roles list.</summary>
/// <param name="SuperAdministrator">The Super Administrator, or null where none is configured.</param>
/// <param name="Assignments">The active assignments, or null when they cannot be read.</param>
/// <param name="AssignmentsUnavailable">True when they cannot be read.</param>
public sealed record RoleAssignmentListResponse(
	SuperAdministratorView? SuperAdministrator,
	IReadOnlyList<RoleAssignmentView>? Assignments,
	bool AssignmentsUnavailable);
