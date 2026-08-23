using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// ADR-0002's guarantee, against a real database: a report and its outbox row
/// are one write. There is no "save, then notify", because that loses a report
/// whenever the process dies between the two — and a lost safety report is not
/// recoverable from anywhere.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class OutboxAtomicityTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset At = new(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_a_report_and_its_outbox_message_When_they_are_saved_in_one_call_Then_both_rows_are_present()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = await SubmittedReportAsync(context);
        context.Reports.Add(report);
        context.OutboxMessages.Add(SummarizationRequestFor(report));

        // When
        await context.SaveChangesAsync();

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        (await reader.Reports.CountAsync(r => r.Id == report.Id)).ShouldBe(1);
        (await reader.OutboxMessages.CountAsync(m => m.AggregateId == report.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Given_a_report_and_its_outbox_message_When_the_transaction_is_rolled_back_Then_neither_row_is_present()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = await SubmittedReportAsync(context);

        // When
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Reports.Add(report);
            context.OutboxMessages.Add(SummarizationRequestFor(report));
            await context.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        (await reader.Reports.CountAsync(r => r.Id == report.Id)).ShouldBe(0);
        (await reader.OutboxMessages.CountAsync(m => m.AggregateId == report.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_report_and_its_outbox_message_When_the_write_fails_part_way_Then_neither_row_is_present()
    {
        // Given — an answer pointing at a question revision that is not there.
        // The database refuses it, and the report and the outbox row have to go
        // with it rather than being left behind without their trigger.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = await SubmittedReportAsync(context);
        var orphan = OrphanedQuestion();
        report.Answer(orphan, "A gust on final; the pilot walked away.", At);

        context.Reports.Add(report);
        context.OutboxMessages.Add(SummarizationRequestFor(report));

        // When
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        (await reader.Reports.CountAsync(r => r.Id == report.Id)).ShouldBe(0);
        (await reader.OutboxMessages.CountAsync(m => m.AggregateId == report.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Given_an_outbox_message_When_it_is_read_back_Then_it_is_due_and_has_never_been_attempted()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = await SubmittedReportAsync(context);
        context.Reports.Add(report);
        context.OutboxMessages.Add(SummarizationRequestFor(report));
        await context.SaveChangesAsync();

        // When
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var message = await reader.OutboxMessages.SingleAsync(m => m.AggregateId == report.Id);

        // Then
        message.IsProcessed.ShouldBeFalse();
        message.IsPoisoned.ShouldBeFalse();
        message.Attempts.ShouldBe(0);
        message.NextAttemptAt.ShouldBe(message.OccurredAt);
        message.Type.ShouldBe("report.submitted");
    }

    private static OutboxMessage SummarizationRequestFor(Report report) =>
        new(report.Id, "report.submitted", $$"""{"reportId":"{{report.Id}}"}""", At);

    private static async Task<Report> SubmittedReportAsync(HpacSafetyDbContext context)
    {
        var report = new Report(Locale.EnCa, At);
        var consent = await QuestionAsync(context, QuestionKey.ConsentPublish);
        report.Answer(consent, ["yes"], At);
        report.EnsureReadyForSubmission();
        return report;
    }

    private static Task<Question> QuestionAsync(HpacSafetyDbContext context, string key) =>
        context.Questions.SingleAsync(q => q.Key == key && q.IsActive);

    /// <summary>
    /// A question the database has never seen, so an answer to it cannot be
    /// stored. Built in memory only.
    /// </summary>
    private static Question OrphanedQuestion() =>
        Question.Create("never_asked", QuestionType.LongText, "Never asked", "Jamais demandée", At);
}
