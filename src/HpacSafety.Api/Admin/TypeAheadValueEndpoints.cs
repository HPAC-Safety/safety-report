using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     The type-ahead values waiting for review, and the review itself: a Safety
///     Officer or an Administrator approves a value, corrects its wording in place
///     for every answer that names it, merges it into another, or removes it
///     (ADR-0129). Every write is
///     audited in the same transaction as the change.
/// </summary>
public static class TypeAheadValueEndpoints
{
	/// <summary>Maps the type-ahead review endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminTypeAheadValues(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/type-ahead-values").RequireAuthorization(HpacPolicies.Reviewer);

		group.MapGet("/awaiting-review", Awaiting);
		group.MapPost("/{id}/approval", Approve);
		group.MapPut("/{id}", Correct);
		group.MapDelete("/{id}", Remove);
		group.MapPost("/{id}/merge", Merge);

		return group;
	}

	/// <summary>
	///     Every value flagged for review on a live type-ahead question, oldest first,
	///     with how many live answers name it.
	/// </summary>
	private static async Task<IResult> Awaiting(
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(database);

		var questions = await QuestionEndpoints.LiveQuestions(database)
			.AsNoTracking()
			.Where(question => question.Deleted == null && question.AllChoices.Any(choice => choice.NeedsReview))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var flagged = questions
			.SelectMany(question => question.AllChoices.Where(choice => choice.NeedsReview)
				.Select(choice => (Question: question, Choice: choice)))
			.ToList();

		// An answer naming a value merged into a flagged one names it too (ADR-0129).
		var readAs = questions
			.SelectMany(question => question.AllChoices)
			.ToDictionary(choice => choice.Id, choice => choice.MergedIntoChoiceId ?? choice.Id);
		var ids = readAs.Keys.Select(id => (TinyId?)id).ToList();
		var named = await database.ReportAnswers
			.AsNoTracking()
			.Where(answer => ids.Contains(answer.ChoiceId))
			.GroupBy(answer => answer.ChoiceId)
			.Select(group => new { ChoiceId = group.Key, Count = group.Count() })
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);
		var answerCounts = named
			.GroupBy(entry => readAs[entry.ChoiceId!.Value])
			.ToDictionary(group => group.Key, group => group.Sum(entry => entry.Count));

		var values = flagged
			.OrderBy(entry => entry.Choice.CreatedAt ?? DateTimeOffset.MinValue)
			.ThenBy(entry => entry.Question.Key, StringComparer.Ordinal)
			.Select(entry => new TypeAheadValueView(
				entry.Choice.Id.Value,
				entry.Question.Id.Value,
				entry.Question.CurrentRevision.LabelEn,
				entry.Question.CurrentRevision.LabelFr,
				entry.Choice.LabelEn,
				entry.Choice.LabelFr,
				entry.Choice.ReporterLocale?.Code,
				entry.Choice.Deleted is not null,
				answerCounts.GetValueOrDefault(entry.Choice.Id),
				entry.Choice.CreatedAt,
				[
					.. entry.Question.Choices
						.Where(target => target.Id != entry.Choice.Id)
						.Select(target => new TypeAheadMergeTarget(target.Id.Value, target.LabelEn, target.LabelFr, EnumCode.Of(target.Pin))),
				]))
			.ToList();

		return Results.Ok(new TypeAheadValuesResponse(values, values.Count));
	}

	private static Task<IResult> Approve(
		string id,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		return Review(id, database, clock, context, AuditAction.ApprovedTypeAheadValue,
			(question, choiceId, reviewer, at) => question.ApproveValue(choiceId, reviewer, at), cancellationToken);
	}

	private static Task<IResult> Correct(
		string id,
		CorrectTypeAheadValueRequest request,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Review(id, database, clock, context, AuditAction.CorrectedTypeAheadValue,
			(question, choiceId, reviewer, at) => question.CorrectValue(choiceId, request.LabelEn, request.LabelFr, reviewer, at), cancellationToken);
	}

	private static Task<IResult> Merge(
		string id,
		MergeTypeAheadValueRequest request,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!TinyId.TryParse(request.IntoId, out var targetId))
		{
			return Task.FromResult(Results.Problem(
				title: "That review was not accepted.",
				detail: "Name the value to merge into.",
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/type-ahead-review"));
		}

		return Review(id, database, clock, context, AuditAction.MergedTypeAheadValue,
			(question, choiceId, reviewer, at) => question.MergeValue(choiceId, targetId, reviewer, at), cancellationToken);
	}

	private static Task<IResult> Remove(
		string id,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		return Review(id, database, clock, context, AuditAction.RemovedTypeAheadValue,
			(question, choiceId, reviewer, at) => question.RemoveValue(choiceId, reviewer, at), cancellationToken);
	}

	/// <summary>
	///     Loads the live question that owns the value, applies one review action,
	///     and audits it in the same save. Not found for an unknown value or one
	///     on a retired question; a refusal names the rule, never report content.
	/// </summary>
	private static async Task<IResult> Review(
		string id,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		AuditAction action,
		Action<Question, TinyId, string, DateTimeOffset> apply,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(clock);
		ArgumentNullException.ThrowIfNull(context);

		if (!TinyId.TryParse(id, out var choiceId))
		{
			return Results.NotFound();
		}

		var question = await QuestionEndpoints.LiveQuestions(database)
			.Where(candidate => candidate.Deleted == null && candidate.AllChoices.Any(choice => choice.Id == choiceId))
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (question is null)
		{
			return Results.NotFound();
		}

		var reviewer = MemberRoles.SubjectOf(context.User) ?? "(unknown)";
		var at = clock.GetUtcNow();

		try
		{
			apply(question, choiceId, reviewer, at);
		}
		catch (DomainRuleViolationException cause)
		{
			return Results.Problem(
				title: "That review was not accepted.",
				detail: cause.Message,
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/type-ahead-review");
		}

		database.AuditLog.Add(new AuditLogEntry(reviewer, action, "QuestionChoice", choiceId, at, question.Id.Value));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}
}

/// <summary>The type-ahead values waiting for review.</summary>
/// <param name="Values">Each one, oldest first.</param>
/// <param name="Count">How many.</param>
public sealed record TypeAheadValuesResponse(IReadOnlyList<TypeAheadValueView> Values, int Count);

/// <summary>One type-ahead value waiting for review.</summary>
/// <param name="Id">The value's identifier.</param>
/// <param name="QuestionId">The type-ahead question it belongs to.</param>
/// <param name="QuestionLabelEn">That question's English wording.</param>
/// <param name="QuestionLabelFr">That question's French wording.</param>
/// <param name="LabelEn">The value's English wording, or null while it has none.</param>
/// <param name="LabelFr">The value's French wording, or null while it has none.</param>
/// <param name="TypedIn">The language a reporter typed it in, or null for a value an Administrator wrote.</param>
/// <param name="IsRemoved">True for a removed value a reporter typed again.</param>
/// <param name="AnswerCount">How many live answers name it.</param>
/// <param name="AddedAt">When a reporter added it, when that was recorded.</param>
/// <param name="MergeTargets">The question's other live values, any of which this one may be merged into.</param>
public sealed record TypeAheadValueView(
	string Id,
	string QuestionId,
	string QuestionLabelEn,
	string QuestionLabelFr,
	string? LabelEn,
	string? LabelFr,
	string? TypedIn,
	bool IsRemoved,
	int AnswerCount,
	DateTimeOffset? AddedAt,
	IReadOnlyList<TypeAheadMergeTarget> MergeTargets);

/// <summary>A live value of the same question a flagged value may be merged into.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="LabelEn">Its English wording, or null while it has none.</param>
/// <param name="LabelFr">Its French wording, or null while it has none.</param>
/// <param name="Pin">
///     <c>first</c>, <c>last</c>, or <c>none</c>, so the page lists the targets as
///     the form lists the choices (ADR-0136).
/// </param>
public sealed record TypeAheadMergeTarget(string Id, string? LabelEn, string? LabelFr, string Pin);

/// <summary>A reviewer's merge of one type-ahead value into another of the same question.</summary>
/// <param name="IntoId">The value it is merged into: every answer naming the merged value reads this one.</param>
public sealed record MergeTypeAheadValueRequest(string? IntoId);

/// <summary>A reviewer's correction of a type-ahead value's wording, in place.</summary>
/// <param name="LabelEn">The English wording; blank leaves a reporter-added value without one.</param>
/// <param name="LabelFr">The French wording; blank leaves a reporter-added value without one.</param>
public sealed record CorrectTypeAheadValueRequest(string? LabelEn, string? LabelFr);
