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

        // When
        var keys = await context.Questions.OrderBy(q => q.DisplayOrder).Select(q => q.Key).ToListAsync();

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

        // Then — a question cannot be asked without wording in every locale, so
        // a half-translated seed would leave a form that renders nothing.
        foreach (var question in questions)
        {
            question.IsActive.ShouldBeTrue($"'{question.Key}' is seeded active.");
            question.CurrentVersion.IsFullyTranslated.ShouldBeTrue($"'{question.Key}' has wording in every locale.");
            question.CurrentVersion.VersionNumber.ShouldBe(1);
        }
    }

    [Fact]
    public async Task Given_a_clean_database_When_question_privacy_is_loaded_Then_it_matches_the_seed_contract()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When
        var stored = await context.Questions.ToDictionaryAsync(question => question.Key);

        // Then
        foreach (var expected in QuestionBankSeed.Questions)
        {
            stored[expected.Key].IsPrivate.ShouldBe(
                expected.IsPrivate,
                $"the privacy classification of '{expected.Key}' must survive the legacy-schema migration");
        }
    }

    [Fact]
    public async Task Given_a_clean_database_When_the_French_wording_is_read_Then_it_is_marked_as_machine_translated_and_unreviewed()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When
        var french = await context.QuestionTranslations.Where(t => t.Locale == Locale.FrCa).ToListAsync();
        var english = await context.QuestionTranslations.Where(t => t.Locale == Locale.EnCa).ToListAsync();

        // Then — nobody has reviewed the French, and the rows say so rather
        // than implying a human signed off on it.
        french.Count.ShouldBe(QuestionBankSeed.Questions.Count);
        french.ShouldAllBe(t => t.IsMachineTranslated && !t.IsSource);
        english.ShouldAllBe(t => !t.IsMachineTranslated && t.IsSource);
    }

    [Fact]
    public async Task Given_a_clean_database_When_a_reviewer_looks_for_wording_nobody_has_read_Then_every_unreviewed_row_is_findable()
    {
        // Given — the admin UI in #49 works through this list. A flag that
        // cannot be queried is a comment, and a comment is not a work queue.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When
        var unreviewed = await context.QuestionTranslations
            .Where(t => t.IsMachineTranslated)
            .Select(t => t.Locale)
            .Distinct()
            .ToListAsync();

        var unreviewedOptions = await context.QuestionOptionTranslations
            .CountAsync(t => t.IsMachineTranslated);

        // Then — all of it is French, and all of the French is there.
        unreviewed.ShouldBe([Locale.FrCa]);
        unreviewedOptions.ShouldBe(QuestionBankSeed.Questions.Sum(q => q.Options.Count));
    }

    [Fact]
    public async Task Given_seeded_wording_nobody_has_read_When_a_reviewer_rewrites_it_Then_it_stops_being_marked_unreviewed()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var province = (await LoadedQuestionsAsync(context)).Single(q => q.Key == "province");
        var french = province.CurrentVersion.Translation(Locale.FrCa)!;

        // When
        french.ReviseByHand("Province de l'événement :", french.HelpText, french.Placeholder, At);
        await context.SaveChangesAsync();

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var reread = await reader.QuestionTranslations.SingleAsync(t => t.Id == french.Id);
        reread.IsMachineTranslated.ShouldBeFalse();
        reread.Label.ShouldBe("Province de l'événement :");
        reread.IsSource.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_a_clean_database_When_a_question_with_choices_is_loaded_Then_its_options_carry_both_languages_in_order()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When
        var province = (await LoadedQuestionsAsync(context)).Single(q => q.Key == "province");
        var options = province.CurrentVersion.Options.OrderBy(o => o.DisplayOrder).ToList();

        // Then
        options.Select(o => o.Code).ShouldBe(QuestionBankSeed.Questions.Single(q => q.Key == "province").Options.Select(o => o.Code).ToList());
        options[0].Translation(Locale.EnCa)!.Label.ShouldBe("Newfoundland and Labrador");
        options[0].Translation(Locale.FrCa)!.Label.ShouldBe("Terre-Neuve-et-Labrador");
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
        consent.CurrentVersion.IsRequired.ShouldBeTrue();
        Should.Throw<DomainRuleViolationException>(() => consent.Delete(At));
    }

    [Fact]
    public async Task Given_the_seeded_form_When_a_report_answers_the_questions_that_carry_roles_Then_the_answers_project_onto_the_report()
    {
        // Given — the seeded option codes are only useful if the projection in
        // Report can actually read them.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var questions = (await LoadedQuestionsAsync(context)).ToDictionary(q => q.Key, StringComparer.Ordinal);
        var report = new Report(Locale.EnCa, At);

        // When
        report.Answer(questions[QuestionKey.ConsentPublish], ["yes"], At);
        report.Answer(questions["occurrence_date"], "2026-07-14", At);
        report.Answer(questions["province"], ["alberta"], At);
        report.Answer(questions["pilot_injury"], ["serious"], At);
        report.Answer(questions["passenger_injury"], ["none"], At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var stored = await reader.Reports.SingleAsync(r => r.Id == report.Id);
        stored.ConsentPublish.ShouldBe(true);
        stored.OccurredOn.ShouldBe(new DateOnly(2026, 7, 14));
        stored.Province.ShouldBe(Province.Alberta);
        stored.PilotInjury.ShouldBe(InjurySeverity.Serious);
        stored.PassengerInjury.ShouldBe(InjurySeverity.None);
        stored.InvolvesSeriousInjury.ShouldBeTrue();
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

    private static async Task<List<Question>> LoadedQuestionsAsync(HpacSafetyDbContext context) =>
        await context.Questions
            .Include(q => q.Versions).ThenInclude(v => v.Translations)
            .Include(q => q.Versions).ThenInclude(v => v.Options).ThenInclude(o => o.Translations)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync();
}
