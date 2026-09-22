using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     ADR-0002's guarantee, against a real database: a report and its outbox row
///     are one write. There is no "save, then notify", because that loses a report
///     whenever the process dies between the two — and a lost safety report is not
///     recoverable from anywhere.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class OutboxAtomicityTests(PostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenReportAndOutboxMessage_WhenTheyAreSavedInOneCall_ThenBothRowsArePresent()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await SubmittedReport(context);
		context.Reports.Add(report);
		context.OutboxMessages.Add(SummarizationRequestFor(report));

		// When
		await context.SaveChangesAsync();

		// Then
		await using var reader = PostgresFixture.ContextFor(connectionString);
		(await reader.Reports.SingleAsync(r => r.Id == report.Id)).Id.ShouldBe(report.Id);
		(await reader.ReportAnswers.SingleAsync(a => a.ReportId == report.Id)).Value.ShouldBe("yes");
		(await reader.OutboxMessages.CountAsync(m => m.AggregateId == report.Id)).ShouldBe(1);
	}

	[Fact]
	public async Task GivenReportAndOutboxMessage_WhenTransactionIsRolledBack_ThenNeitherRowIsPresent()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await SubmittedReport(context);

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
	public async Task GivenReportAndOutboxMessage_WhenWriteFailsPartWay_ThenNeitherRowIsPresent()
	{
		// Given — an answer pointing at a question version that is not there.
		// The database refuses it, and the report and the outbox row have to go
		// with it rather than being left behind without their trigger.
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await SubmittedReport(context);
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
	public async Task GivenOutboxMessage_WhenReadBack_ThenDueAndHasNeverBeenAttempted()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await SubmittedReport(context);
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
		message.Type.ShouldBe(OutboxMessageType.SummarizeReport);
	}

	private static OutboxMessage SummarizationRequestFor(Report report)
	{
		return new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, $$"""{"reportId":"{{report.Id}}"}""", At);
	}

	private static async Task<Report> SubmittedReport(HpacSafetyDbContext context)
	{
		var report = new Report(Locale.EnCa, At);
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
		context.Questions.Add(consent);
		await context.SaveChangesAsync();

		report.Answer(consent, ["yes"], At);
		report.EnsureReadyForSubmission();
		return report;
	}

	/// <summary>
	///     A question the database has never seen, so an answer to it cannot be
	///     stored. Built in memory only.
	/// </summary>
	private static Question OrphanedQuestion()
	{
		return Question.Create("never_asked", QuestionType.LongText, "Never asked", "Jamais demandé", At);
	}
}
