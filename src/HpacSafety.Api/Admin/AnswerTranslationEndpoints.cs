using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     The queue of select answers waiting for their second official language.
/// </summary>
/// <remarks>
///     <para>
///         A reporter answers a picker or a type-ahead in one language, and nothing on
///         the submission path translates it (ADR-0072). The answer is stored as they
///         gave it and flagged; this is where an administrator clears that flag, by
///         typing the other language or by pressing Translate and saving what comes
///         back.
///     </para>
///     <para>
///         Everything here requires the <c>Administrator</c> policy. The queue spans
///         reports, so it shows reporter-entered text in a list rather than one report
///         at a time — which is why it is not offered to a safety officer.
///     </para>
/// </remarks>
public static class AnswerTranslationEndpoints
{
	/// <summary>Maps the answer-translation queue endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminAnswerTranslation(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/answers").RequireAuthorization(HpacPolicies.Administrator);

		group.MapGet("/awaiting-translation", AwaitingAsync);
		group.MapPut("/{id}/translation", SupplyAsync);

		return group;
	}

	private static async Task<IResult> AwaitingAsync(
		HpacSafetyDbContext database, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(database);

		var waiting = await database.ReportAnswers
			.Where(answer => answer.NeedsTranslation)
			.OrderBy(answer => answer.AnsweredAt)
			.Select(answer => new AwaitingTranslationView(
				answer.Id.ToString(),
				answer.QuestionKey,
				answer.Value!,
				answer.Locale.Code,
				answer.Locale.Counterpart.Code))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return Results.Ok(new AwaitingTranslationResponse(waiting, waiting.Count));
	}

	private static async Task<IResult> SupplyAsync(
		string id,
		SupplyTranslationRequest request,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(database);

		if (!TinyId.TryParse(id, out var answerId)) return Results.NotFound();

		var answer = await database.ReportAnswers
			.FirstOrDefaultAsync(candidate => candidate.Id == answerId, cancellationToken)
			.ConfigureAwait(false);

		if (answer is null) return Results.NotFound();

		try
		{
			answer.SupplyTranslation(request.Value);
		}
		catch (DomainRuleViolationException cause)
		{
			return Results.Problem(
				title: "That translation was not accepted.",
				detail: cause.Message,
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/answer-translation");
		}

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}
}

/// <summary>The answers waiting for a second language.</summary>
/// <param name="Answers">The queue, oldest first.</param>
/// <param name="Waiting">How many are waiting, which the page shows as a count.</param>
public sealed record AwaitingTranslationResponse(IReadOnlyList<AwaitingTranslationView> Answers, int Waiting);

/// <summary>One answer waiting for its second language.</summary>
/// <param name="Id">The answer to supply a translation for.</param>
/// <param name="QuestionKey">Which question was answered.</param>
/// <param name="Value">The words the reporter gave, which are never changed.</param>
/// <param name="Locale">The language they gave them in.</param>
/// <param name="Into">The language an administrator is being asked to supply.</param>
public sealed record AwaitingTranslationView(
	string Id,
	string QuestionKey,
	string Value,
	string Locale,
	string Into);

/// <summary>An administrator's wording for an answer's second language.</summary>
/// <param name="Value">The other language, typed or accepted from Translate.</param>
public sealed record SupplyTranslationRequest(string Value);
