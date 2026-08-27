using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// A clean database asks exactly the Typeform question set — in both languages,
/// active, and answerable. See ADR-0016 and ADR-0020.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SeededQuestionBankTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset At = new(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_a_clean_database_When_the_question_bank_is_read_Then_it_holds_every_seeded_question_in_order()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When — DisplayOrder now lives on the revision, so ordering by it
        // happens after the aggregate is materialized, not in SQL.
        var keys = (await LoadedQuestionsAsync(context)).Select(q => q.Key).ToList();

        // Then
        keys.ShouldBe(QuestionBankSeed.Questions.Select(q => q.Key).ToList());
    }

    [Fact]
    public async Task Given_a_clean_database_When_a_seeded_question_is_loaded_Then_it_is_active_and_worded_in_both_languages()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When
        var questions = await LoadedQuestionsAsync(context);

        // Then — a question is born complete in both official languages
        foreach (var question in questions)
        {
            question.IsActive.ShouldBeTrue($"'{question.Key}' is seeded active.");
            question.CurrentRevision.LabelEn.ShouldNotBeNullOrWhiteSpace($"'{question.Key}' has English wording.");
            question.CurrentRevision.LabelFr.ShouldNotBeNullOrWhiteSpace($"'{question.Key}' has French wording.");
            question.CurrentRevision.RevisionNumber.ShouldBe(1);
        }
    }

    [Fact]
    public async Task Given_a_clean_database_When_question_privacy_is_loaded_Then_it_matches_the_seed_contract()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When — IsPrivate reads through to CurrentRevision, so Revisions must be loaded.
        var stored = (await LoadedQuestionsAsync(context)).ToDictionary(question => question.Key, StringComparer.Ordinal);

        // Then
        foreach (var expected in QuestionBankSeed.Questions)
        {
            stored[expected.Key].IsPrivate.ShouldBe(
                expected.IsPrivate,
                $"the privacy classification of '{expected.Key}' must survive the migration");
        }
    }

    [Fact]
    public async Task Given_a_clean_database_When_a_question_with_choices_is_loaded_Then_its_options_carry_both_languages_in_order()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When
        var province = (await LoadedQuestionsAsync(context)).Single(q => q.Key == "province");
        var options = province.CurrentRevision.Options.OrderBy(o => o.DisplayOrder).ToList();

        // Then
        options.Select(o => o.Code).ShouldBe(QuestionBankSeed.Questions.Single(q => q.Key == "province").Options.Select(o => o.Code).ToList());
        options[0].LabelEn.ShouldBe("Newfoundland and Labrador");
        options[0].LabelFr.ShouldBe("Terre-Neuve-et-Labrador");
    }

    [Fact]
    public async Task Given_a_clean_database_When_publication_consent_is_looked_up_Then_it_is_the_system_question_and_cannot_be_deleted()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When
        var consent = (await LoadedQuestionsAsync(context)).Single(q => q.Key == QuestionKey.ConsentPublish);

        // Then
        consent.IsSystem.ShouldBeTrue();
        consent.Role.ShouldBe(QuestionRole.ConsentPublish);
        consent.CurrentRevision.IsRequired.ShouldBeTrue();
        Should.Throw<DomainRuleViolationException>(() => consent.Delete(At));
    }

    [Fact]
    public async Task Given_the_seeded_form_When_a_report_answers_publication_consent_Then_the_answer_projects_onto_the_report()
    {
        // Given — consent is the only answer a report reads by name.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var questions = (await LoadedQuestionsAsync(context)).ToDictionary(q => q.Key, StringComparer.Ordinal);
        var report = new Report(Locale.EnCa, At);

        // When
        report.Answer(questions[QuestionKey.ConsentPublish], ["yes"], At);
        report.Answer(questions["province"], ["alberta"], At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var stored = await reader.Reports.SingleAsync(r => r.Id == report.Id);
        stored.ConsentPublish.ShouldBe(true);

        var provinceAnswer = await reader.ReportAnswers.SingleAsync(a => a.ReportId == report.Id && a.QuestionKey == "province");
        provinceAnswer.SelectedOptionCodes.ShouldBe(["alberta"]);
    }

    [Fact]
    public async Task Given_the_seeded_form_When_a_multi_select_answer_is_saved_Then_every_chosen_code_survives_the_round_trip()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var ratings = (await LoadedQuestionsAsync(context)).Single(q => q.Key == "pilot_ratings");
        var report = new Report(Locale.EnCa, At);

        // When
        report.Answer(ratings, ["p3", "paragliding_instructor"], At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var answer = await reader.ReportAnswers.SingleAsync(a => a.ReportId == report.Id);
        answer.SelectedOptionCodes.ShouldBe(["p3", "paragliding_instructor"]);
        answer.IsPrivate.ShouldBeTrue();
    }

    // DisplayOrder now lives on QuestionRevision, so it cannot be translated
    // into a SQL ORDER BY against the questions table — the aggregate is
    // ordered after materialization instead.
    private static async Task<List<Question>> LoadedQuestionsAsync(HpacSafetyDbContext context)
    {
        var questions = await context.Questions
            .Include(q => q.Revisions).ThenInclude(v => v.Options)
            .ToListAsync();
        return questions.OrderBy(q => q.DisplayOrder).ToList();
    }
}
