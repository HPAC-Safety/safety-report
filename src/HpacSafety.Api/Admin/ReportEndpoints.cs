using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     The one admin report endpoint that exists so far: soft deletion. The review
///     queue and its approve/reject/publish/edit-summary endpoints are issue #25 —
///     not built yet.
/// </summary>
public static class ReportEndpoints
{
	/// <summary>Maps the admin report endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminReports(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/reports").RequireAuthorization(HpacPolicies.Reviewer);

		group.MapDelete("/{id}", DeleteAsync);

		return group;
	}

	/// <summary>
	///     Soft-deletes a report and everything it owns — answers, files, summary,
	///     and pending outbox work — with one shared timestamp, in one transaction.
	///     Irreversible: there is no restore endpoint (REQ-DOM-007).
	/// </summary>
	private static async Task<IResult> DeleteAsync(
		string id, HpacSafetyDbContext database, TimeProvider clock, HttpContext context, CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(id, out var reportId))
		{
			return Results.NotFound();
		}

		var report = await database.Reports
			.Include(candidate => candidate.Answers)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken)
			.ConfigureAwait(false);

		if (report is null)
		{
			return Results.NotFound();
		}

		var at = clock.GetUtcNow();

		report.SoftDelete(at);

		var pendingOutbox = await database.OutboxMessages
			.Where(message => message.AggregateId == reportId)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		foreach (var message in pendingOutbox)
		{
			message.Delete(at);
		}

		// A validated token should always carry a subject, but the /me endpoint
		// already treats that as unguaranteed rather than assumed — a role claim
		// alone does not prove a subject claim exists. Same stance here.
		var subject = MemberRoles.SubjectOf(context.User) ?? "(unknown)";
		database.AuditLog.Add(new AuditLogEntry(subject, AuditAction.DeletedReport, "Report", reportId, at));

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}
}
