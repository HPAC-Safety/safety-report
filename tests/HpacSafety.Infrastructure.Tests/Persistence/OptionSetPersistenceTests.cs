using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// Shared choice lists, and the property the whole design exists for: a
/// revision built from one keeps its own copy whatever later happens to the
/// list. See ADR-0058.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class OptionSetPersistenceTests(PostgresFixture postgres)
{
    private const string Items = "_items";
    private static readonly DateTimeOffset At = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_a_shared_list_When_it_is_saved_Then_it_round_trips_with_tiny_id_keys()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var key = UniqueKey("aerodromes");

        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            var set = OptionSet.Create(key, "Aerodromes", "Aérodromes", At);
            set.Add("golden", "Golden", "Golden");
            set.Add("lumby", "Lumby", "Lumby");

            context.OptionSets.Add(set);
            await context.SaveChangesAsync();
        }

        // When
        await using var reading = PostgresFixture.ContextFor(connectionString);
        var loaded = await reading.OptionSets.Include(Items).SingleAsync(set => set.Key == key);

        // Then
        loaded.Id.Value.Length.ShouldBe(11);
        loaded.Items.Select(item => item.Code).ShouldBe(["golden", "lumby"]);
        loaded.Items.ShouldAllBe(item => item.Id.Value.Length == 11);
    }

    [Fact]
    public async Task Given_a_removed_item_When_the_list_is_read_back_Then_the_row_is_retained_and_filtered_out()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var key = UniqueKey("provinces");

        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            var set = OptionSet.Create(key, "Provinces", "Provinces", At);
            set.Add("alberta", "Alberta", "Alberta");
            set.Add("yukon", "Yukon", "Yukon");

            context.OptionSets.Add(set);
            await context.SaveChangesAsync();

            set.Remove("yukon", At.AddHours(1));
            await context.SaveChangesAsync();
        }

        // When
        await using var reading = PostgresFixture.ContextFor(connectionString);
        var loaded = await reading.OptionSets.Include(Items).SingleAsync(set => set.Key == key);

        // Then — filtered from the live list, but never physically deleted
        loaded.Items.Select(item => item.Code).ShouldBe(["alberta"]);

        var stored = await reading.OptionSetItems
            .IgnoreQueryFilters()
            .CountAsync(item => item.OptionSetId == loaded.Id);

        stored.ShouldBe(2);
    }

    [Fact]
    public async Task Given_a_revision_built_from_a_list_When_the_list_is_deleted_Then_the_snapshot_survives_intact()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var questionKey = UniqueKey("launch_site");

        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            var set = OptionSet.Create(UniqueKey("sites"), "Sites", "Sites", At);
            set.Add("golden", "Golden", "Golden");
            set.Add("lumby", "Lumby", "Lumby");

            context.OptionSets.Add(set);
            await context.SaveChangesAsync();

            var question = Question.Create(
                questionKey,
                QuestionType.Autocomplete,
                "Where did you launch from?",
                "D'où avez-vous décollé ?",
                At,
                isActive: true,
                optionSetId: set.Id,
                options: set.AsRevisionOptions());

            context.Questions.Add(question);
            await context.SaveChangesAsync();

            // When the whole list is retired afterwards
            set.Delete(At.AddHours(1));
            await context.SaveChangesAsync();
        }

        // Then the revision still offers exactly what it snapshotted
        await using var reading = PostgresFixture.ContextFor(connectionString);
        var loaded = await reading.Questions
            .Include(question => question.Revisions)
            .ThenInclude(revision => revision.Options)
            .SingleAsync(question => question.Key == questionKey);

        var options = loaded.CurrentRevision.Options.OrderBy(option => option.DisplayOrder).ToList();

        options.Select(option => option.Code).ShouldBe(["golden", "lumby"]);
        options.ShouldAllBe(option => option.SourceItemId != null);
        loaded.CurrentRevision.OptionSetId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Given_a_conditional_question_When_it_is_read_back_Then_it_names_the_parent_question()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var childKey = UniqueKey("injury_detail");

        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            var parent = Question.Create(
                UniqueKey("were_you_injured"), QuestionType.YesNo, "Were you injured?", "Avez-vous été blessé ?",
                At, isActive: true);

            context.Questions.Add(parent);
            await context.SaveChangesAsync();

            var child = Question.Create(
                childKey, QuestionType.LongText, "What was the injury?", "Quelle était la blessure ?",
                At, isActive: true, dependsOnQuestionId: parent.Id);

            context.Questions.Add(child);
            await context.SaveChangesAsync();
        }

        // When
        await using var reading = PostgresFixture.ContextFor(connectionString);
        var loaded = await reading.Questions
            .Include(question => question.Revisions)
            .SingleAsync(question => question.Key == childKey);

        // Then
        loaded.DependsOnQuestionId.ShouldNotBeNull();

        var parentExists = await reading.Questions.AnyAsync(question => question.Id == loaded.DependsOnQuestionId);
        parentExists.ShouldBeTrue();
    }

    private static string UniqueKey(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..24];
}
