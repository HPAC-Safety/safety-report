using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>Checks that a clean database mirrors the current Typeform question set.</summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SeededQuestionBankTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset At = new(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_a_clean_database_When_live_questions_are_read_Then_the_latest_revisions_are_in_form_order()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        var keys = await context.Questions
            .Where(question => question.IsActive
                && !context.Questions.Any(candidate =>
                    candidate.Key == question.Key
                    && candidate.IsActive
                    && candidate.Revision > question.Revision))
            .OrderBy(question => question.SortOrder)
            .Select(question => question.Key)
            .ToListAsync();

        keys.ShouldBe(QuestionBankSeed.Questions.Select(question => question.Key).ToList());
    }

    [Fact]
    public async Task Given_a_clean_database_When_questions_are_loaded_Then_each_revision_is_complete_and_bilingual()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        var questions = await LoadedQuestionsAsync(context);

        foreach (var question in questions)
        {
            question.Revision.ShouldBe(1);
            question.IsActive.ShouldBeTrue($"'{question.Key}' is seeded active.");
            question.LabelEn.ShouldNotBeNullOrWhiteSpace();
            question.LabelFr.ShouldNotBeNullOrWhiteSpace();
            question.Options.ShouldAllBe(option =>
                !string.IsNullOrWhiteSpace(option.LabelEn) && !string.IsNullOrWhiteSpace(option.LabelFr));
        }
    }

    [Fact]
    public async Task Given_a_clean_database_When_question_privacy_is_loaded_Then_it_matches_the_seed()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var stored = await context.Questions.ToDictionaryAsync(question => question.Key);

        foreach (var expected in QuestionBankSeed.Questions)
        {
            stored[expected.Key].IsPrivate.ShouldBe(expected.IsPrivate);
        }
    }

    [Fact]
    public async Task Given_a_choice_question_When_it_is_loaded_Then_options_carry_both_languages_in_order()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        var province = (await LoadedQuestionsAsync(context)).Single(question => question.Key == "province");
        var options = province.Options.OrderBy(option => option.SortOrder).ToList();

        options.Select(option => option.Code).ShouldBe(
            QuestionBankSeed.Questions.Single(question => question.Key == "province").Options.Select(option => option.Code));
        options[0].LabelEn.ShouldBe("Newfoundland and Labrador");
        options[0].LabelFr.ShouldBe("Terre-Neuve-et-Labrador");
    }

    [Fact]
    public async Task Given_a_clean_database_When_publication_consent_is_loaded_Then_it_is_the_only_required_question()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        var questions = await LoadedQuestionsAsync(context);
        var consent = questions.Single(question => question.Key == QuestionKey.ConsentPublish);

        consent.IsSystem.ShouldBeTrue();
        consent.IsRequired.ShouldBeTrue();
        consent.IsPrivate.ShouldBeTrue();
        consent.Type.ShouldBe(QuestionType.YesNo);
        questions.Where(question => question.Key != QuestionKey.ConsentPublish)
            .ShouldAllBe(question => !question.IsSystem && !question.IsRequired);
    }

    [Fact]
    public async Task Given_an_answer_When_it_is_saved_Then_it_references_the_exact_question_revision()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var ratings = (await LoadedQuestionsAsync(context)).Single(question => question.Key == "pilot_ratings");
        var report = new Report(Locale.EnCa, At);

        report.Answer(ratings, ["p3", "paragliding_instructor"], At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        await using var reader = PostgresFixture.ContextFor(connectionString);
        var answer = await reader.ReportAnswers.SingleAsync(item => item.ReportId == report.Id);
        answer.QuestionId.ShouldBe(ratings.Id);
        answer.QuestionKey.ShouldBe(ratings.Key);
        answer.SelectedOptionCodes.ShouldBe(["p3", "paragliding_instructor"]);
    }

    [Fact]
    public async Task Given_an_existing_question_When_wording_changes_Then_a_new_complete_row_is_added_and_the_old_row_is_unchanged()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var original = (await LoadedQuestionsAsync(context)).Single(question => question.Key == "damage");
        var originalLabel = original.LabelEn;
        var revision = original.Revise(
            original.Type,
            "Damage to aircraft or property:",
            "Dommages à l'aéronef ou aux biens :",
            At,
            original.IsPrivate,
            original.SortOrder,
            isActive: true,
            original.HelpEn,
            original.HelpFr,
            original.SectionKey);

        context.Questions.Add(revision);
        await context.SaveChangesAsync();

        await using var reader = PostgresFixture.ContextFor(connectionString);
        var stored = await reader.Questions
            .Where(question => question.Key == "damage")
            .OrderBy(question => question.Revision)
            .ToListAsync();
        stored.Count.ShouldBe(2);
        stored[0].LabelEn.ShouldBe(originalLabel);
        stored[1].Revision.ShouldBe(2);
        stored[1].SupersedesQuestionId.ShouldBe(stored[0].Id);
        stored[1].LabelEn.ShouldBe("Damage to aircraft or property:");
    }

    private static Task<List<Question>> LoadedQuestionsAsync(HpacSafetyDbContext context) =>
        context.Questions
            .Include(question => question.Options)
            .OrderBy(question => question.SortOrder)
            .ToListAsync();
}
