using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
/// Endpoints for the reusable choice lists a question can offer — ADR-0058.
/// </summary>
/// <remarks>
/// These rows are editable, which is the point: an administrator adds an
/// aerodrome once rather than to every question that asks for one. Editing a
/// list changes what the <i>next</i> revision offers and changes nothing about
/// any revision that already snapshotted it.
/// </remarks>
public static class OptionSetEndpoints
{
    /// <summary>Maps the admin choice-list endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The group, so the caller can see what was mapped.</returns>
    public static RouteGroupBuilder MapAdminOptionSets(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/admin/option-sets").RequireAuthorization(HpacPolicies.Administrator);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id}", ReplaceAsync);
        group.MapDelete("/{id}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(HpacSafetyDbContext database, CancellationToken cancellationToken)
    {
        var sets = await Live(database)
            .OrderBy(set => set.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(sets.Select(OptionSetView.Of).ToList());
    }

    private static async Task<IResult> CreateAsync(
        SaveOptionSetRequest request,
        HpacSafetyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return Problem("missing-key", "A choice list needs a key.", "A choice list needs a stable key that never changes.");
        }

        var key = QuestionKey.Normalize(request.Key);

        var taken = await Live(database)
            .AnyAsync(set => set.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (taken)
        {
            return Problem("duplicate-key", "That key is taken.", $"Another choice list already uses the key '{key}'.");
        }

        try
        {
            var set = OptionSet.Create(key, request.NameEn, request.NameFr, clock.GetUtcNow());

            foreach (var item in request.Items)
            {
                set.Add(item.Code, item.LabelEn, item.LabelFr);
            }

            database.OptionSets.Add(set);
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Results.Created($"/api/admin/option-sets/{set.Id.Value}", OptionSetView.Of(set));
        }
        catch (DomainRuleViolationException cause)
        {
            return Problem("option-set-rule", "That change is not allowed.", cause.Message);
        }
    }

    /// <summary>
    /// Brings a list in line with what the administrator submitted: relabels
    /// what stayed, adds what is new, removes what is gone, and arranges the
    /// result. A removal is a soft delete, so every revision that snapshotted
    /// the removed choice keeps its own copy.
    /// </summary>
    private static async Task<IResult> ReplaceAsync(
        string id,
        SaveOptionSetRequest request,
        HpacSafetyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TinyId.TryParse(id, out var setId))
        {
            return Results.NotFound();
        }

        var set = await Live(database)
            .FirstOrDefaultAsync(candidate => candidate.Id == setId, cancellationToken)
            .ConfigureAwait(false);

        if (set is null)
        {
            return Results.NotFound();
        }

        try
        {
            var at = clock.GetUtcNow();
            var wanted = request.Items.Select(item => QuestionKey.Normalize(item.Code)).ToList();

            set.Rename(request.NameEn, request.NameFr);

            foreach (var gone in set.Items.Select(item => item.Code).Where(code => !wanted.Contains(code, StringComparer.Ordinal)).ToList())
            {
                set.Remove(gone, at);
            }

            var live = set.Items.Select(item => item.Code).ToList();

            foreach (var item in request.Items)
            {
                var code = QuestionKey.Normalize(item.Code);

                if (live.Contains(code, StringComparer.Ordinal))
                {
                    set.Relabel(code, item.LabelEn, item.LabelFr);
                }
                else
                {
                    set.Add(code, item.LabelEn, item.LabelFr);
                }
            }

            set.Arrange(wanted);

            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Results.Ok(OptionSetView.Of(set));
        }
        catch (DomainRuleViolationException cause)
        {
            return Problem("option-set-rule", "That change is not allowed.", cause.Message);
        }
    }

    private static async Task<IResult> DeleteAsync(
        string id,
        HpacSafetyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!TinyId.TryParse(id, out var setId))
        {
            return Results.NotFound();
        }

        var set = await Live(database)
            .FirstOrDefaultAsync(candidate => candidate.Id == setId, cancellationToken)
            .ConfigureAwait(false);

        if (set is null)
        {
            return Results.NotFound();
        }

        set.Delete(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    /// <summary>
    /// Loads a set with its items. The navigation is the backing field, not
    /// <see cref="OptionSet.Items"/> — that property filters out removed items
    /// and orders what is left, so it is a projection EF cannot include.
    /// </summary>
    private static IQueryable<OptionSet> Live(HpacSafetyDbContext database) =>
        database.OptionSets.Include(ItemsNavigation);

    private const string ItemsNavigation = "_items";

    private static IResult Problem(string code, string title, string detail) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: $"https://hpac.ca/problems/{code}");
}
