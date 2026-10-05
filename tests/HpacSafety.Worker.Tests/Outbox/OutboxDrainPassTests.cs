using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Observability;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     <see cref="OutboxDrainPass" /> is what a Lambda invocation drains
///     through instead of <see cref="Worker" />'s polling loop (ADR-0123): one
///     or more due messages, until nothing is left or the time budget given to
///     it runs out. See <c>.spec/features/README.md</c> and issue #443.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class OutboxDrainPassTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenSeveralDueMessages_WhenDrainingUntilIdle_ThenEveryOneIsProcessedInOnePass()
	{
		// Given — three due comments to translate, no time pressure at all.
		var connectionString = await postgres.CreateMigratedDatabase();
		var reportId = await SeedReportAsync(connectionString);

		await using (var seed = WorkerPostgresFixture.ContextFor(connectionString))
		{
			for (var i = 0; i < 3; i++)
			{
				seed.OutboxMessages.Add(new OutboxMessage(reportId, OutboxMessageType.TranslateAnswers, reportId.Value, At));
			}

			await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		var services = new ServiceCollection();
		services.AddDbContext<HpacSafetyDbContext>(options => options.UseNpgsql(connectionString));
		services.AddScoped<ITranslator, StubTranslator>();
		services.AddScoped<HpacSafety.Worker.Outbox.IOutboxMessageProcessor, TranslateAnswersProcessor>();
		services.AddSingleton<IMetricsPublisher, NoOpMetricsPublisher>();
		var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

		// When — an ample, never-shrinking time budget, as a Lambda invocation
		// would pass its context's remaining time.
		await OutboxDrainPass.DrainUntilIdleOrOutOfTime(
			scopeFactory,
			TimeProvider.System,
			() => TimeSpan.FromMinutes(15),
			TimeSpan.FromSeconds(30),
			CancellationToken.None);

		// Then
		await using var check = WorkerPostgresFixture.ContextFor(connectionString);
		var processed = await check.OutboxMessages.CountAsync(message => message.ProcessedAt != null, cancellationToken: TestContext.Current.CancellationToken);
		processed.ShouldBe(3);
	}

	[Fact]
	public async Task GivenADueMessage_WhenTheTimeBudgetIsAlreadySpent_ThenNothingIsClaimed()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var reportId = await SeedReportAsync(connectionString);

		await using (var seed = WorkerPostgresFixture.ContextFor(connectionString))
		{
			seed.OutboxMessages.Add(new OutboxMessage(reportId, OutboxMessageType.TranslateAnswers, reportId.Value, At));
			await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		var services = new ServiceCollection();
		services.AddDbContext<HpacSafetyDbContext>(options => options.UseNpgsql(connectionString));
		services.AddScoped<ITranslator, StubTranslator>();
		services.AddScoped<HpacSafety.Worker.Outbox.IOutboxMessageProcessor, TranslateAnswersProcessor>();
		var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

		// When — remaining time is already inside the safety margin, as a
		// nearly-out-of-time invocation would report.
		await OutboxDrainPass.DrainUntilIdleOrOutOfTime(
			scopeFactory,
			TimeProvider.System,
			() => TimeSpan.FromSeconds(5),
			TimeSpan.FromSeconds(30),
			CancellationToken.None);

		// Then — the due message is left for the next invocation.
		await using var check = WorkerPostgresFixture.ContextFor(connectionString);
		var message = await check.OutboxMessages.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
		message.ProcessedAt.ShouldBeNull();
	}

	private static async Task<TinyId> SeedReportAsync(string connectionString)
	{
		await using var seed = WorkerPostgresFixture.ContextFor(connectionString);
		var narrative = Question.Create(
			"narrative-drain", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isActive: true);
		seed.Questions.Add(narrative);
		await seed.SaveChangesAsync();

		var report = new Report(Locale.EnCa, At);
		report.Answer(narrative, "Wind picked up on final.", At);
		seed.Reports.Add(report);
		await seed.SaveChangesAsync();

		return report.Id;
	}
}
