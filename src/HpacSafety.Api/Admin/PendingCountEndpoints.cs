using HpacSafety.Api.Authentication;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     How much admin work is waiting, for the Admin menu's badges (REQ-MOD-084).
///     Counts only — no report or answer content — so reading it is not audited.
/// </summary>
public static class PendingCountEndpoints
{
	/// <summary>Maps the pending-counts endpoint.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The route, so the caller can see what was mapped.</returns>
	public static RouteHandlerBuilder MapAdminPendingCounts(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		return app.MapGet("/api/admin/counts", Counts).RequireAuthorization(HpacPolicies.Reviewer);
	}

	/// <summary>
	///     The reports needing action for any reviewer, and the answers awaiting
	///     translation only for someone the Administrator policy admits — the queue
	///     itself is theirs alone (REQ-MOD-085).
	/// </summary>
	private static async Task<IResult> Counts(
		HpacSafetyDbContext database,
		IAuthorizationService authorization,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		var counts = await database.AdminPendingCounts
			.AsNoTracking()
			.SingleAsync(cancellationToken)
			.ConfigureAwait(false);

		var isAdministrator = (await authorization
			.AuthorizeAsync(context.User, HpacPolicies.Administrator)
			.ConfigureAwait(false)).Succeeded;

		return Results.Ok(new PendingCountsResponse(
			counts.ReportsNeedingAction,
			isAdministrator ? counts.AnswersAwaitingTranslation : null));
	}
}

/// <summary>How much admin work is waiting.</summary>
/// <param name="ReportsNeedingAction">Live reports the Needs action filter would list.</param>
/// <param name="AnswersAwaitingTranslation">Answers waiting for a second language; null unless the caller is an Administrator.</param>
public sealed record PendingCountsResponse(int ReportsNeedingAction, int? AnswersAwaitingTranslation);
