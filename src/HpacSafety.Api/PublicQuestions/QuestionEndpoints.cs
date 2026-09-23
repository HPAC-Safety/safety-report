using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.PublicQuestions;

/// <summary>
///     The reporter-facing read side of the question bank: today's live,
///     active question set, ordered, bilingual, with grouping and conditional
///     dependencies resolved so the client can render the form without a
///     second round trip.
/// </summary>
/// <remarks>
///     No authentication — this is public content, distinct from the
///     member-gated submission endpoint
///     (<see href="https://github.com/HPAC-Safety/safety-report/issues/14">#14</see>)
///     and the Administrator-only authoring endpoints in
///     <see cref="HpacSafety.Api.Admin.QuestionEndpoints" />, whose
///     <see cref="HpacSafety.Api.Admin.QuestionEndpoints.LiveQuestions" />
///     this reuses rather than re-querying the same tables a second way.
/// </remarks>
public static class QuestionEndpoints
{
	/// <summary>Maps the public question endpoint.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapPublicQuestions(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/v1/questions");

		group.MapGet("/", List);

		return group;
	}

	/// <summary>
	///     Today's form: the latest live revision per stable key, active ones
	///     only, in form order. A soft-deleted or deactivated latest revision
	///     is omitted outright — it is never replaced by an older active
	///     revision, because a question's history before its current revision
	///     is not a thing the public form has ever asked.
	/// </summary>
	private static async Task<IResult> List(HpacSafetyDbContext database, CancellationToken cancellationToken)
	{
		var questions = await HpacSafety.Api.Admin.QuestionEndpoints.LiveQuestions(database)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var live = questions
			.Where(question => question.IsActive)
			.OrderBy(question => question.DisplayOrder)
			.ThenBy(question => question.Key, StringComparer.Ordinal)
			.ToList();

		var childrenByGroup = live
			.Where(question => question.GroupedUnderQuestionId is not null)
			.ToLookup(question => question.GroupedUnderQuestionId!.Value);

		return Results.Ok(
			live
				.Where(question => question.GroupedUnderQuestionId is null)
				.Select(question => ToView(question, childrenByGroup))
				.ToList());
	}

	private static PublicQuestionView ToView(Question question, ILookup<TinyId, Question> childrenByGroup)
	{
		var children = question.Type == QuestionType.Group
			? childrenByGroup[question.Id]
				.OrderBy(child => child.DisplayOrder)
				.ThenBy(child => child.Key, StringComparer.Ordinal)
				.Select(child => ToView(child, childrenByGroup))
				.ToList()
			: [];

		return PublicQuestionView.Of(question, children);
	}
}
