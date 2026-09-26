using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     Authoring endpoints for the question bank. The form is data, so this is how
///     it changes — ADR-0016, and there is no deploy involved.
/// </summary>
/// <remarks>
///     Every write goes through the <see cref="Question" /> aggregate, which is what
///     makes an edit a new revision rather than an update. Nothing here patches a
///     <c>question_revisions</c> row, and nothing may: a report answered a specific
///     revision and has to keep rendering exactly what it was asked. A question's
///     choices are the exception — they belong to the question and are edited in
///     place (ADR-0095).
/// </remarks>
public static class QuestionEndpoints
{
	/// <summary>Maps the admin question endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminQuestions(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/questions").RequireAuthorization(HpacPolicies.Administrator);

		group.MapGet("/", List);
		group.MapPost("/", Create);
		group.MapPut("/{id}", Revise);
		group.MapPost("/order", Reorder);
		group.MapDelete("/{id}", Delete);
		group.MapDelete("/{id}/revisions/{revisionId}", DeleteRevision);

		return group;
	}

	/// <summary>
	///     Every live question with its current revision, in form order. Ties in
	///     sort order break by stable key, which is what makes the order
	///     deterministic rather than whatever PostgreSQL returned.
	/// </summary>
	private static async Task<IResult> List(HpacSafetyDbContext database,
											CancellationToken cancellationToken)
	{
		var questions = await LiveQuestions(database).ToListAsync(cancellationToken).ConfigureAwait(false);
		var bank = await WithRetiredParents(database, questions, cancellationToken).ConfigureAwait(false);
		var answered = await AnsweredQuestionIds(database, cancellationToken).ConfigureAwait(false);

		return Results.Ok(
			questions
				.OrderBy(question => question.DisplayOrder)
				.ThenBy(question => question.Key, StringComparer.Ordinal)
				.Select(question => QuestionView.Of(question, answered.Contains(question.Id), bank))
				.ToList());
	}

	private static async Task<IResult> Create(
		SaveQuestionRequest request,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!EnumCode.TryParse<QuestionType>(request.Type, out var type))
		{
			return UnknownType(request.Type);
		}

		var at = clock.GetUtcNow();
		var questions = await LiveQuestions(database).ToListAsync(cancellationToken).ConfigureAwait(false);
		var bank = await WithRetiredParents(database, questions, cancellationToken).ConfigureAwait(false);
		string key;

		if (string.IsNullOrWhiteSpace(request.Key))
		{
			// An administrator never sees or chooses a key: it is derived from
			// the English wording, and never reuses one a retired question
			// still holds, so a new question cannot join another's history.
			key = await DerivedKey(request.LabelEn, database, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// Only an imported Typeform draft still carries a key of its own. A
			// retired question's key is refused too: a new question never joins
			// another question's history.
			key = QuestionKey.Normalize(request.Key);
			var taken = await database.Questions
				.IgnoreQueryFilters()
				.AnyAsync(question => question.Key == key, cancellationToken)
				.ConfigureAwait(false);

			if (taken)
			{
				return Problem("duplicate-key", "That key is taken.", $"Another question already uses the key '{key}'.");
			}
		}

		return await Save(async () =>
		{
			var dependsOn = ResolvedDependency(request, bank, null);
			var groupedUnderQuestionId = ResolvedGrouping(request, questions, null);
			var displayOrder = NextDisplayOrder(questions);
			var choiceParentId = ResolvedChoiceParent(request, type, questions, null, displayOrder);
			var options = OptionsFor(request, type, null, choiceParentId);

			var question = Question.Create(
				key,
				type,
				request.LabelEn,
				request.LabelFr,
				at,
				request.HelpTextEn,
				request.HelpTextFr,
				request.PlaceholderEn,
				request.PlaceholderFr,
				QuestionRole.None,
				request.IsRequired,
				request.IsPrivate,
				request.IsActive,
				displayOrder,
				dependsOn.ParentId,
				dependsOn.ChoiceId,
				groupedUnderQuestionId,
				options,
				request.IsTranslatable,
				request.AllowFutureDates,
				choiceParentId);

			ChoiceDependencies.EnsureLinksAllowed(questions, question);

			database.Questions.Add(question);
			Audit(database, context, AuditAction.CreatedQuestion, question.Id, at);
			await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			return Results.Created($"/api/admin/questions/{question.Id.Value}", QuestionView.Of(question, bank: bank));
		}).ConfigureAwait(false);
	}

	/// <summary>
	///     Saves an edit. A change to a revision field is a new revision — or, once
	///     answered, a replacement question (ADR-0071) — and the previous one is left
	///     exactly as it was. The choices are applied in place either way, so a
	///     choices-only edit keeps the question and its revision (ADR-0095).
	/// </summary>
	private static async Task<IResult> Revise(
		string id,
		SaveQuestionRequest request,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!TinyId.TryParse(id, out var questionId))
		{
			return Results.NotFound();
		}

		if (!EnumCode.TryParse<QuestionType>(request.Type, out var type))
		{
			return UnknownType(request.Type);
		}

		var questions = await LiveQuestions(database).ToListAsync(cancellationToken).ConfigureAwait(false);
		var bank = await WithRetiredParents(database, questions, cancellationToken).ConfigureAwait(false);
		var question = questions.Find(candidate => candidate.Id == questionId);

		if (question is null)
		{
			return Results.NotFound();
		}

		var wasActive = question.IsActive;
		var at = clock.GetUtcNow();

		return await Save(async () =>
		{
			var dependsOn = ResolvedDependency(request, bank, question);
			var groupedUnderQuestionId = ResolvedGrouping(request, questions, question.Id);
			var choiceParentId = ResolvedChoiceParent(request, type, questions, question, question.DisplayOrder);
			var options = OptionsFor(request, type, question, choiceParentId);

			ChoiceDependencies.EnsureParentKeepsType(questions, question, type);

			if (options is not null)
			{
				QuestionDependencies.EnsureChoicesRemovable(bank, question, [.. options.Select(option => option.Code)]);
				ChoiceDependencies.EnsureParentChoicesRemovable(questions, question, [.. options.Select(option => option.Code)]);
			}

			// Outside the revision (ADR-0145): set in place, and carried by a fork.
			question.DependChoicesOn(choiceParentId);

			var hasBeenAnswered = await HasBeenAnswered(database, question.Id, cancellationToken)
				.ConfigureAwait(false);

			// Revises while nothing has answered it, and otherwise retires
			// this question and returns its replacement (ADR-0071) — unless
			// only the choices changed, which never does either (ADR-0095).
			var live = question.ApplyEdit(
				hasBeenAnswered,
				type,
				request.LabelEn,
				request.LabelFr,
				request.IsPrivate,
				request.IsActive,
				question.DisplayOrder,
				at,
				request.HelpTextEn,
				request.HelpTextFr,
				request.PlaceholderEn,
				request.PlaceholderFr,
				request.IsRequired,
				dependsOn.ParentId,
				dependsOn.ChoiceId,
				groupedUnderQuestionId,
				options,
				request.IsTranslatable,
				request.AllowFutureDates);

			var forked = !ReferenceEquals(live, question);

			if (forked)
			{
				database.Questions.Add(live);
			}

			// A replaced, merged, or forked parent choice passes its links on, and a
			// forked parent its dependents, without revising them (ADR-0145).
			List<Question> touched = [.. questions, .. forked ? [live] : Array.Empty<Question>()];
			ChoiceDependencies.EnsureLinksAllowed(touched, live);
			ChoiceDependencies.Follow(touched, live);

			var action = wasActive && !request.IsActive ? AuditAction.DeactivatedQuestion : AuditAction.RevisedQuestion;
			Audit(database, context, action, live.Id, at, forked ? "forked" : null);
			await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			// The replacement is new, so nothing has answered it yet.
			return Results.Ok(QuestionView.Of(live, hasBeenAnswered && !forked, bank));
		}).ConfigureAwait(false);
	}

	/// <summary>
	///     Rearranges the form. Every moved question gets a new revision, and all of
	///     them are written in one <c>SaveChangesAsync</c> — a half-applied reorder
	///     would leave two questions claiming the same position.
	/// </summary>
	private static async Task<IResult> Reorder(
		ReorderQuestionsRequest request,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		var questions = await LiveQuestions(database).ToListAsync(cancellationToken).ConfigureAwait(false);
		var ordered = new List<Question>(request.QuestionIdsInOrder.Count);

		foreach (var candidate in request.QuestionIdsInOrder)
		{
			if (!TinyId.TryParse(candidate, out var id)
				|| questions.Find(question => question.Id == id) is not { } question)
			{
				return Problem(
					"unknown-question",
					"That question no longer exists.",
					"The form changed while it was being rearranged. Reload and try again.");
			}

			ordered.Add(question);
		}

		if (ordered.Count != questions.Count)
		{
			return Problem(
				"incomplete-order",
				"Every question has to be listed.",
				"A partial arrangement would leave the questions it omits in an arbitrary position.");
		}

		try
		{
			// A parent is answered first, so it stays above every question whose
			// choices depend on it (ADR-0145).
			ChoiceDependencies.EnsureOrder(ordered);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem("question-rule", "That change is not allowed.", cause.Message);
		}

		var at = clock.GetUtcNow();
		var moved = 0;

		for (var position = 0; position < ordered.Count; position++)
		{
			if (ordered[position].DisplayOrder != position)
			{
				ordered[position].Reorder(position, at);
				moved++;
			}
		}

		if (moved > 0)
		{
			Audit(database, context, AuditAction.ReorderedQuestions, TinyId.New(), at, $"moved={moved}", "QuestionOrder");
		}

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		var bank = await WithRetiredParents(database, ordered, cancellationToken).ConfigureAwait(false);
		return Results.Ok(ordered.Select(question => QuestionView.Of(question, bank: bank)).ToList());
	}

	/// <summary>
	///     Retires a question. Always a soft delete — answers already given to it
	///     belong to a real report and are never removed with it.
	/// </summary>
	private static async Task<IResult> Delete(
		string id,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(id, out var questionId))
		{
			return Results.NotFound();
		}

		var question = await LiveQuestions(database)
			.FirstOrDefaultAsync(candidate => candidate.Id == questionId, cancellationToken)
			.ConfigureAwait(false);

		if (question is null)
		{
			return Results.NotFound();
		}

		// Counts answers on deleted reports too: a deleted report is still a
		// record of what somebody was asked (REQ-QB-030, REQ-QB-031).
		var hasBeenAnswered = await HasBeenAnswered(database, question.Id, cancellationToken)
			.ConfigureAwait(false);

		return await Save(async () =>
		{
			var at = clock.GetUtcNow();
			question.Delete(hasBeenAnswered, at);
			Audit(database, context, AuditAction.DeletedQuestion, question.Id, at);
			await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			return Results.NoContent();
		}).ConfigureAwait(false);
	}

	/// <summary>
	///     Deletes one historical revision out of a live question's chain —
	///     distinct from <see cref="Delete" />, which retires the whole question.
	/// </summary>
	private static async Task<IResult> DeleteRevision(
		string id,
		string revisionId,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(id, out var questionId)
			|| !TinyId.TryParse(revisionId, out var parsedRevisionId))
		{
			return Results.NotFound();
		}

		var question = await LiveQuestions(database)
			.FirstOrDefaultAsync(candidate => candidate.Id == questionId, cancellationToken)
			.ConfigureAwait(false);

		if (question is null
			|| question.Revisions.All(revision => revision.Id != parsedRevisionId))
		{
			return Results.NotFound();
		}

		// Counts answers on deleted reports too: a deleted report is still a
		// record of what somebody was asked (REQ-DOM-008).
		var hasBeenAnswered = await HasBeenAnsweredRevision(database, parsedRevisionId, cancellationToken)
			.ConfigureAwait(false);

		return await Save(async () =>
		{
			var at = clock.GetUtcNow();
			question.DeleteRevision(parsedRevisionId, hasBeenAnswered, at);
			Audit(database, context, AuditAction.DeletedQuestionRevision, parsedRevisionId, at, targetType: "QuestionRevision");
			await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			return Results.NoContent();
		}).ConfigureAwait(false);
	}

	/// <summary>
	///     Every question that any answer references, so the list can mark which
	///     ones an edit would replace rather than revise.
	/// </summary>
	private static async Task<HashSet<TinyId>> AnsweredQuestionIds(
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		return
		[
			.. await database.ReportAnswers
				.IgnoreQueryFilters()
				.Select(answer => answer.QuestionId)
				.Distinct()
				.ToListAsync(cancellationToken)
				.ConfigureAwait(false),
		];
	}

	/// <summary>
	///     Whether any answer anywhere references this question, which is what
	///     decides between revising it and replacing it (ADR-0071).
	/// </summary>
	/// <remarks>
	///     Query filters are ignored deliberately: an answer on a soft-deleted
	///     report is still a record of what somebody was asked, so it forces the
	///     fork exactly as a live one does.
	/// </remarks>
	private static Task<bool> HasBeenAnswered(
		HpacSafetyDbContext database,
		TinyId questionId,
		CancellationToken cancellationToken)
	{
		return database.ReportAnswers
			.IgnoreQueryFilters()
			.AnyAsync(answer => answer.QuestionId == questionId, cancellationToken);
	}

	/// <summary>
	///     Whether any answer references this one revision, which is what decides
	///     whether it may be deleted out of its question's history (REQ-DOM-008).
	/// </summary>
	/// <remarks>
	///     Query filters are ignored deliberately, the same as
	///     <see cref="HasBeenAnswered" />: an answer on a soft-deleted report is
	///     still a record of what somebody was asked.
	/// </remarks>
	private static Task<bool> HasBeenAnsweredRevision(
		HpacSafetyDbContext database,
		TinyId revisionId,
		CancellationToken cancellationToken)
	{
		return database.ReportAnswers
			.IgnoreQueryFilters()
			.AnyAsync(answer => answer.QuestionRevisionId == revisionId, cancellationToken);
	}

	/// <summary>
	///     A key from the English wording, suffixed <c>_2</c>, <c>_3</c>, … past any
	///     key a question already holds, retired questions included. Wording that
	///     reduces to nothing falls back to <c>question</c>.
	/// </summary>
	private static async Task<string> DerivedKey(
		string labelEn,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		var stem = Stem(labelEn);

		var taken = await database.Questions
			.IgnoreQueryFilters()
			.Where(question => question.Key == stem || question.Key.StartsWith(stem + "_"))
			.Select(question => question.Key)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var key = stem;
		for (var suffix = 2; taken.Contains(key, StringComparer.Ordinal); suffix++)
		{
			key = $"{stem}_{suffix}";
		}

		return key;
	}

	/// <summary>The wording normalized and cut to a readable length, or <c>question</c>.</summary>
	private static string Stem(string labelEn)
	{
		const int length = 60;

		try
		{
			var normalized = QuestionKey.Normalize(labelEn ?? string.Empty);
			return normalized.Length <= length ? normalized : normalized[..length].TrimEnd('_');
		}
		catch (DomainRuleViolationException)
		{
			return "question";
		}
	}

	/// <summary>Every live question with its revisions and every choice, removed ones included. Also used by the Typeform export.</summary>
	internal static IQueryable<Question> LiveQuestions(HpacSafetyDbContext database)
	{
		return database.Questions
			.Include(question => question.Revisions)
			.Include(question => question.AllChoices);
	}

	/// <summary>
	///     <paramref name="live" />, and every retired question a live one's condition
	///     still names: the parents that forked after the condition was saved. A
	///     condition follows its parent's fork on read, and resolving it needs the
	///     retired parent's key and choices (ADR-0132).
	/// </summary>
	internal static async Task<List<Question>> WithRetiredParents(HpacSafetyDbContext database,
																  List<Question> live,
																  CancellationToken cancellationToken)
	{
		var liveIds = live.Select(question => question.Id).ToHashSet();
		var named = live
			.Select(question => question.DependsOnQuestionId)
			.OfType<TinyId>()
			.Where(id => !liveIds.Contains(id))
			.Distinct()
			.ToList();

		if (named.Count == 0)
		{
			return live;
		}

		var retired = await database.Questions
			.IgnoreQueryFilters()
			.Include(question => question.Revisions)
			.Include(question => question.AllChoices)
			.Where(question => named.Contains(question.Id))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return [.. live, .. retired];
	}

	private static int NextDisplayOrder(List<Question> questions)
	{
		return questions.Count == 0 ? 0 : questions.Max(question => question.DisplayOrder) + 1;
	}

	/// <summary>
	///     Resolves the parent question and required option, checking the part of
	///     the rule that needs to see the rest of the bank: the parent exists, is
	///     live, is a yes/no or single-select question, currently offers the
	///     required option when it needs one, and does not lead back here. See
	///     ADR-0060, ADR-0074.
	/// </summary>
	private static (TinyId? ParentId, TinyId? ChoiceId) ResolvedDependency(
		SaveQuestionRequest request,
		List<Question> questions,
		Question? child)
	{
		if (!TinyId.TryParse(request.DependsOnQuestionId, out var parentId))
		{
			return (null, null);
		}

		TinyId? choiceId = null;

		if (!string.IsNullOrWhiteSpace(request.DependsOnChoiceId))
		{
			choiceId = TinyId.TryParse(request.DependsOnChoiceId, out var parsed)
				? parsed
				: throw new DomainRuleViolationException("That required option is not one the parent question offers.");
		}

		QuestionDependencies.EnsureDependencyAllowed(questions, child?.Id, parentId, choiceId);

		// The screen shows a condition as the parent and choice that stand for it
		// today: the replacement of a replaced option (ADR-0128), and the live
		// question that replaced a forked parent (ADR-0132). Saving it back is not
		// a change of condition, so what is stored is kept — otherwise an
		// untouched condition would revise, or fork, the question.
		if (child?.DependsOnQuestionId is { } storedParent
			&& QuestionDependencies.ParentToday(questions, storedParent) is { } today
			&& today.Id == parentId
			&& (choiceId is null
				? child.DependsOnChoiceId is null
				: QuestionDependencies.RequiredChoiceToday(questions, child.CurrentRevision) is { } required
				  && required == today.CurrentChoice(choiceId.Value)))
		{
			return (storedParent, child.DependsOnChoiceId);
		}

		return (parentId, choiceId);
	}

	/// <summary>
	///     The question whose answer decides which of this one's choices are offered,
	///     checking what needs the rest of the bank: it is another live single-select
	///     or type-ahead, depends on nothing itself, is nobody's child when this one
	///     is somebody's parent, and comes first on the form (ADR-0145). Null, for a
	///     type that takes no choices, clears it — a retype must not leave one behind.
	/// </summary>
	private static TinyId? ResolvedChoiceParent(SaveQuestionRequest request,
												QuestionType type,
												List<Question> questions,
												Question? child,
												int displayOrder)
	{
		if (string.IsNullOrWhiteSpace(request.ChoicesDependOnQuestionId))
		{
			return null;
		}

		if (!TinyId.TryParse(request.ChoicesDependOnQuestionId, out var parentId))
		{
			throw new DomainRuleViolationException("That question no longer exists, so no question's choices can depend on it.");
		}

		ChoiceDependencies.EnsureDependencyAllowed(questions, child?.Id, request.LabelEn, type, displayOrder, parentId);
		return parentId;
	}

	/// <summary>
	///     Resolves the group question this one renders under, checking the
	///     part of the rule that needs to see the rest of the bank: the group
	///     exists, is live, is currently a group question, and does not lead
	///     back here. See ADR-0076.
	/// </summary>
	private static TinyId? ResolvedGrouping(SaveQuestionRequest request,
											List<Question> questions,
											TinyId? childId)
	{
		if (!TinyId.TryParse(request.GroupedUnderQuestionId, out var groupId))
		{
			return null;
		}

		QuestionGrouping.EnsureGroupingAllowed(questions, childId, groupId);

		return groupId;
	}

	/// <summary>
	///     The complete ordered list of the question's choices, each under the code
	///     it already has or one derived from its wording. Empty for a type that
	///     takes no choices, which clears any a retyped question still had; null
	///     when the request sends none, which leaves the choices as they are.
	/// </summary>
	private static IReadOnlyList<QuestionOptionInput>? OptionsFor(SaveQuestionRequest request,
																  QuestionType type,
																  Question? existing,
																  TinyId? choiceParentId)
	{
		if (type is not (QuestionType.SingleSelect or QuestionType.MultiSelect or QuestionType.Autocomplete))
		{
			return [];
		}

		if (request.Options is null)
		{
			return null;
		}

		var options = choiceParentId is null ? request.Options : CodedUnderParents(request.Options, existing);

		return [.. OptionInput.Resolve(options).Select(pair => new QuestionOptionInput(pair.Code, pair.Option.LabelEn, pair.Option.LabelFr, pair.Option.Replace, pair.Option.ResolvedPin, pair.Option.ResolvedParentChoiceId))];
	}

	/// <summary>
	///     Gives each new choice of a dependent question a code of its own. The same
	///     wording is entered once under each parent choice it applies to — "Other"
	///     under every make — so a new choice whose wording reduces to a code the
	///     question already holds takes the next free <c>_2</c>, <c>_3</c>, … instead
	///     of reviving or relabelling that choice (ADR-0145). Wording repeated under
	///     one parent choice is still refused, by the question.
	/// </summary>
	private static List<OptionInput> CodedUnderParents(IReadOnlyList<OptionInput> options,
														Question? existing)
	{
		var taken = options
			.Where(option => !string.IsNullOrWhiteSpace(option.Code))
			.Select(option => QuestionKey.Normalize(option.Code!))
			.ToHashSet(StringComparer.Ordinal);

		return
		[
			.. options.Select(option =>
			{
				if (!string.IsNullOrWhiteSpace(option.Code))
				{
					return option;
				}

				var stem = option.ResolvedCode;
				var code = existing?.UnusedCode(stem, taken) ?? stem;

				for (var suffix = 2; existing is null && taken.Contains(code); suffix++)
				{
					code = $"{stem}_{suffix}";
				}

				taken.Add(code);
				return option with { Code = code };
			}),
		];
	}

	/// <summary>
	///     Turns a broken domain rule into a 400 rather than a 500. The message is
	///     authored form-definition text, never report content or reporter input.
	/// </summary>
	private static async Task<IResult> Save(Func<Task<IResult>> write)
	{
		try
		{
			return await write().ConfigureAwait(false);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem("question-rule", "That change is not allowed.", cause.Message);
		}
	}

	private static IResult UnknownType(string type)
	{
		return Problem("unknown-type", "That is not a question type.", $"'{type}' does not name a question type.");
	}

	/// <summary>
	///     Queues one audit row on the same <see cref="HpacSafetyDbContext" /> the
	///     caller is about to call <c>SaveChangesAsync</c> on, so it commits in the
	///     same transaction as the change it describes — a failed audit write rolls
	///     the change back too (ADR-0092).
	/// </summary>
	private static void Audit(
		HpacSafetyDbContext database,
		HttpContext context,
		AuditAction action,
		TinyId targetId,
		DateTimeOffset at,
		string? detail = null,
		string targetType = "Question")
	{
		var subject = MemberRoles.SubjectOf(context.User) ?? "(unknown)";
		database.AuditLog.Add(new AuditLogEntry(subject, action, targetType, targetId, at, detail));
	}

	private static IResult Problem(string code,
								   string title,
								   string detail)
	{
		return Results.Problem(
			title: title,
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: $"https://hpac.ca/problems/{code}");
	}
}
