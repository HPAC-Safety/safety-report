using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
/// Authoring endpoints for the question bank. The form is data, so this is how
/// it changes — ADR-0016, and there is no deploy involved.
/// </summary>
/// <remarks>
/// Every write goes through the <see cref="Question"/> aggregate, which is what
/// makes an edit a new revision rather than an update. Nothing here patches a
/// <c>question_revisions</c> row, and nothing may: a report answered a specific
/// revision and has to keep rendering exactly what it was asked.
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

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id}", ReviseAsync);
        group.MapPost("/order", ReorderAsync);
        group.MapDelete("/{id}", DeleteAsync);

        return group;
    }

    /// <summary>
    /// Every live question with its current revision, in form order. Ties in
    /// sort order break by stable key, which is what makes the order
    /// deterministic rather than whatever PostgreSQL returned.
    /// </summary>
    private static async Task<IResult> ListAsync(HpacSafetyDbContext database, CancellationToken cancellationToken)
    {
        var questions = await LiveQuestions(database).ToListAsync(cancellationToken).ConfigureAwait(false);
        var sets = await LiveSetsAsync(database, cancellationToken).ConfigureAwait(false);

        return Results.Ok(
            questions
                .OrderBy(question => question.DisplayOrder)
                .ThenBy(question => question.Key, StringComparer.Ordinal)
                .Select(question => QuestionView.Of(question, SetFor(question, sets)))
                .ToList());
    }

    private static async Task<IResult> CreateAsync(
        SaveQuestionRequest request,
        HpacSafetyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!EnumCode.TryParse<QuestionType>(request.Type, out var type))
        {
            return UnknownType(request.Type);
        }

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return Problem("missing-key", "A question needs a key.", "A question needs a stable key that never changes.");
        }

        var at = clock.GetUtcNow();
        var questions = await LiveQuestions(database).ToListAsync(cancellationToken).ConfigureAwait(false);
        var key = QuestionKey.Normalize(request.Key);

        if (questions.Exists(question => question.Key == key))
        {
            return Problem("duplicate-key", "That key is taken.", $"Another question already uses the key '{key}'.");
        }

        return await Save(
            async () =>
            {
                var dependsOn = ResolvedDependency(request, questions, childId: null);
                var options = await OptionsForAsync(request, database, type, cancellationToken).ConfigureAwait(false);

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
                    NextDisplayOrder(questions),
                    request.SectionKey,
                    dependsOn,
                    ParsedOptionSet(request),
                    options);

                database.Questions.Add(question);
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var sets = await LiveSetsAsync(database, cancellationToken).ConfigureAwait(false);

                return Results.Created(
                    $"/api/admin/questions/{question.Id.Value}", QuestionView.Of(question, SetFor(question, sets)));
            }).ConfigureAwait(false);
    }

    /// <summary>Saves an edit as a new revision. The previous one is left exactly as it was.</summary>
    private static async Task<IResult> ReviseAsync(
        string id,
        SaveQuestionRequest request,
        HpacSafetyDbContext database,
        TimeProvider clock,
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
        var question = questions.Find(candidate => candidate.Id == questionId);

        if (question is null)
        {
            return Results.NotFound();
        }

        return await Save(
            async () =>
            {
                var dependsOn = ResolvedDependency(request, questions, question.Id);
                var options = await OptionsForAsync(request, database, type, cancellationToken).ConfigureAwait(false);

                question.Revise(
                    type,
                    request.LabelEn,
                    request.LabelFr,
                    request.IsPrivate,
                    request.IsActive,
                    question.DisplayOrder,
                    request.SectionKey,
                    clock.GetUtcNow(),
                    request.HelpTextEn,
                    request.HelpTextFr,
                    request.PlaceholderEn,
                    request.PlaceholderFr,
                    request.IsRequired,
                    dependsOn,
                    ParsedOptionSet(request),
                    options);

                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var sets = await LiveSetsAsync(database, cancellationToken).ConfigureAwait(false);

                return Results.Ok(QuestionView.Of(question, SetFor(question, sets)));
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Rearranges the form. Every moved question gets a new revision, and all of
    /// them are written in one <c>SaveChangesAsync</c> — a half-applied reorder
    /// would leave two questions claiming the same position.
    /// </summary>
    private static async Task<IResult> ReorderAsync(
        ReorderQuestionsRequest request,
        HpacSafetyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var questions = await LiveQuestions(database).ToListAsync(cancellationToken).ConfigureAwait(false);
        var ordered = new List<Question>(request.QuestionIdsInOrder.Count);

        foreach (var candidate in request.QuestionIdsInOrder)
        {
            if (!TinyId.TryParse(candidate, out var id) || questions.Find(question => question.Id == id) is not { } question)
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

        var at = clock.GetUtcNow();

        for (var position = 0; position < ordered.Count; position++)
        {
            if (ordered[position].DisplayOrder != position)
            {
                ordered[position].Reorder(position, at);
            }
        }

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var sets = await LiveSetsAsync(database, cancellationToken).ConfigureAwait(false);

        return Results.Ok(
            ordered.Select(question => QuestionView.Of(question, SetFor(question, sets))).ToList());
    }

    /// <summary>
    /// Retires a question. Always a soft delete — answers already given to it
    /// belong to a real report and are never removed with it.
    /// </summary>
    private static async Task<IResult> DeleteAsync(
        string id,
        HpacSafetyDbContext database,
        TimeProvider clock,
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

        return await Save(
            async () =>
            {
                question.Delete(clock.GetUtcNow());
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return Results.NoContent();
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Every live shared choice list, by id. An autocomplete renders the live
    /// list rather than its snapshot (ADR-0063), so the screen needs them.
    /// </summary>
    private static async Task<Dictionary<TinyId, OptionSet>> LiveSetsAsync(
        HpacSafetyDbContext database, CancellationToken cancellationToken)
    {
        var sets = await database.OptionSets
            .Include("_items")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sets.ToDictionary(set => set.Id);
    }

    /// <summary>The shared list a question's current revision names, if it names a live one.</summary>
    private static OptionSet? SetFor(Question question, Dictionary<TinyId, OptionSet> sets) =>
        question.CurrentRevision.OptionSetId is { } id && sets.TryGetValue(id, out var set) ? set : null;

    private static IQueryable<Question> LiveQuestions(HpacSafetyDbContext database) =>
        database.Questions
            .Include(question => question.Revisions)
            .ThenInclude(revision => revision.Options);

    private static int NextDisplayOrder(List<Question> questions) =>
        questions.Count == 0 ? 0 : questions.Max(question => question.DisplayOrder) + 1;

    private static TinyId? ParsedOptionSet(SaveQuestionRequest request) =>
        TinyId.TryParse(request.OptionSetId, out var optionSetId) ? optionSetId : null;

    /// <summary>
    /// Resolves the parent question, checking the part of the rule that needs
    /// to see the rest of the bank: the parent exists, is live, is a yes/no
    /// question, and does not lead back here. See ADR-0060.
    /// </summary>
    private static TinyId? ResolvedDependency(
        SaveQuestionRequest request, List<Question> questions, TinyId? childId)
    {
        if (!TinyId.TryParse(request.DependsOnQuestionId, out var parentId))
        {
            return null;
        }

        QuestionDependencies.EnsureDependencyAllowed(questions, childId, parentId);

        return parentId;
    }

    /// <summary>
    /// The complete option set the new revision is born with. When a shared set
    /// is named, its live items are <b>copied</b> — the revision answers from
    /// that copy forever, whatever later happens to the set. See ADR-0058.
    /// </summary>
    private static async Task<IReadOnlyList<QuestionOptionInput>> OptionsForAsync(
        SaveQuestionRequest request,
        HpacSafetyDbContext database,
        QuestionType type,
        CancellationToken cancellationToken)
    {
        if (ParsedOptionSet(request) is { } optionSetId)
        {
            var set = await database.OptionSets
                .Include("_items")
                .FirstOrDefaultAsync(candidate => candidate.Id == optionSetId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new DomainRuleViolationException("That choice list no longer exists.");

            return set.AsRevisionOptions();
        }

        return type is QuestionType.YesNo || request.Options is null
            ? []
            : [.. request.Options.Select(option => new QuestionOptionInput(option.Code, option.LabelEn, option.LabelFr))];
    }

    /// <summary>
    /// Turns a broken domain rule into a 400 rather than a 500. The message is
    /// authored form-definition text, never report content or reporter input.
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

    private static IResult UnknownType(string type) =>
        Problem("unknown-type", "That is not a question type.", $"'{type}' does not name a question type.");

    private static IResult Problem(string code, string title, string detail) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: $"https://hpac.ca/problems/{code}");
}
