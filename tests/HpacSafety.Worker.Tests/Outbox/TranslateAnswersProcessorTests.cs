using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     ADR-0080 against a real database: every answer with a value — narrative
///     included, not just select-shaped ones — gets mechanically translated, the
///     source stays untouched, and a skipped answer is never called into the
///     provider.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class TranslateAnswersProcessorTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenTranslateAnswersOutboxType_WhenAsked_ThenHandlesTranslateAnswers()
	{
		// Given
		var processor = new TranslateAnswersProcessor(null!, new StubTranslator());

		// Then
		processor.HandlesType.ShouldBe(OutboxMessageType.TranslateAnswers);
	}

	[Fact]
	public async Task GivenNarrativeAndSelectAnswers_WhenProcessed_ThenBothGetAnAutoTranslation()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var province = Province();
		var narrative = Narrative();
		context.Questions.AddRange(province, narrative);
		await context.SaveChangesAsync();

		var report = new Report(Locale.EnCa, At);
		report.Answer(province, "Alberta", At);
		report.Answer(narrative, "Wind picked up on final; the pilot walked away.", At);
		context.Reports.Add(report);
		await context.SaveChangesAsync();

		var translator = new StubTranslator();
		var processor = new TranslateAnswersProcessor(context, translator);
		var message = new OutboxMessage(report.Id, OutboxMessageType.TranslateAnswers, report.Id.Value, At);

		// When
		await processor.Process(message, CancellationToken.None);
		await context.SaveChangesAsync();

		// Then
		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var answers = await reader.ReportAnswers.Where(a => a.ReportId == report.Id).ToListAsync();

		answers.ShouldAllBe(answer => answer.TranslatedValue != null);
		answers.ShouldAllBe(answer => answer.TranslationSource == TranslationSource.Auto);

		var provinceAnswer = answers.Single(a => a.QuestionKey == "province");
		provinceAnswer.Value.ShouldBe("Alberta");
		provinceAnswer.Locale.ShouldBe(Locale.EnCa);
		provinceAnswer.TranslatedValue.ShouldBe("[fr-CA] Alberta");

		var narrativeAnswer = answers.Single(a => a.QuestionKey == "narrative");
		narrativeAnswer.Value.ShouldBe("Wind picked up on final; the pilot walked away.");
		narrativeAnswer.TranslatedValue.ShouldBe("[fr-CA] Wind picked up on final; the pilot walked away.");
	}

	[Fact]
	public async Task GivenASkippedAnswer_WhenProcessed_ThenItIsNeverSentToTheTranslator()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var narrative = Narrative();
		context.Questions.Add(narrative);
		await context.SaveChangesAsync();

		var report = new Report(Locale.EnCa, At);
		report.Answer(narrative, value: null, At);
		context.Reports.Add(report);
		await context.SaveChangesAsync();

		var translator = new StubTranslator();
		var processor = new TranslateAnswersProcessor(context, translator);
		var message = new OutboxMessage(report.Id, OutboxMessageType.TranslateAnswers, report.Id.Value, At);

		// When
		await processor.Process(message, CancellationToken.None);
		await context.SaveChangesAsync();

		// Then
		translator.Calls.ShouldBeEmpty();

		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var answer = await reader.ReportAnswers.SingleAsync(a => a.ReportId == report.Id);
		answer.Value.ShouldBeNull();
		answer.TranslatedValue.ShouldBeNull();
		answer.TranslationSource.ShouldBeNull();
	}

	[Fact]
	public async Task GivenAnAlreadyTranslatedAnswer_WhenProcessedAgain_ThenItIsNotRetranslated()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var narrative = Narrative();
		context.Questions.Add(narrative);
		await context.SaveChangesAsync();

		var report = new Report(Locale.EnCa, At);
		var answer = report.Answer(narrative, "Already handled by an administrator.", At);
		answer.SupplyHumanTranslation("Déjà traité par un administrateur.");
		context.Reports.Add(report);
		await context.SaveChangesAsync();

		var translator = new StubTranslator();
		var processor = new TranslateAnswersProcessor(context, translator);
		var message = new OutboxMessage(report.Id, OutboxMessageType.TranslateAnswers, report.Id.Value, At);

		// When
		await processor.Process(message, CancellationToken.None);
		await context.SaveChangesAsync();

		// Then
		translator.Calls.ShouldBeEmpty();

		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var stored = await reader.ReportAnswers.SingleAsync(a => a.ReportId == report.Id);
		stored.TranslatedValue.ShouldBe("Déjà traité par un administrateur.");
		stored.TranslationSource.ShouldBe(TranslationSource.Human);
	}

	private static Question Province()
	{
		return Question.Create(
			"province", QuestionType.SingleSelect, "Province", "Province", At, isActive: true, displayOrder: 1,
			options: [new QuestionOptionInput("alberta", "Alberta", "Alberta")]);
	}

	private static Question Narrative()
	{
		return Question.Create(
			"narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isActive: true);
	}
}
