using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     <see cref="Worker" />'s own claim loop, end to end, with a real
///     <see cref="IOutboxMessageProcessor" /> registered — <see cref="WorkerTests" />
///     deliberately registers none, so it never exercises the per-processor
///     <c>foreach</c> that actually drains outbox work. This is that path.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class WorkerOutboxLoopTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenARegisteredProcessor_WhenAMatchingMessageIsDue_ThenTheWorkerLoopClaimsAndRunsIt()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabaseAsync();

		await using (var seed = WorkerPostgresFixture.ContextFor(connectionString))
		{
			var narrative = Question.Create(
				"narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isActive: true);
			seed.Questions.Add(narrative);
			await seed.SaveChangesAsync();

			var report = new Report(Locale.EnCa, At);
			report.Answer(narrative, "Wind picked up on final.", At);
			seed.Reports.Add(report);
			seed.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.TranslateAnswers, report.Id.Value, At));
			await seed.SaveChangesAsync();
		}

		var services = new ServiceCollection();
		services.AddDbContext<HpacSafetyDbContext>(options => options.UseNpgsql(connectionString));
		services.AddScoped<ITranslator, StubTranslator>();
		services.AddScoped<IOutboxMessageProcessor, TranslateAnswersProcessor>();

		var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
		var logger = new FakeLogger<Worker>();
		var worker = new Worker(scopeFactory, TimeProvider.System, logger);

		// When
		await worker.StartAsync(CancellationToken.None);

		var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
		string? translated = null;
		while (translated is null && DateTimeOffset.UtcNow < deadline)
		{
			await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
			translated = await reader.ReportAnswers.Select(a => a.TranslatedValue).FirstOrDefaultAsync();
			if (translated is null)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(50));
			}
		}

		await worker.StopAsync(CancellationToken.None);

		// Then
		translated.ShouldBe("[fr-CA] Wind picked up on final.");

		await using var final = WorkerPostgresFixture.ContextFor(connectionString);
		var message = await final.OutboxMessages.SingleAsync();
		message.ProcessedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenNoDueMessage_WhenTheIdlePollIsCancelled_ThenTheLoopStopsWithoutThrowing()
	{
		// Given — no processor registered, so the loop always idles.
		var connectionString = await postgres.CreateMigratedDatabaseAsync();
		var services = new ServiceCollection();
		services.AddDbContext<HpacSafetyDbContext>(options => options.UseNpgsql(connectionString));

		var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
		var worker = new Worker(scopeFactory, TimeProvider.System, new FakeLogger<Worker>());

		// When
		await worker.StartAsync(CancellationToken.None);
		var stop = async () => await worker.StopAsync(CancellationToken.None);

		// Then — cancelling mid-idle-delay must not surface as an unhandled
		// exception; the loop catches it and breaks.
		await stop.ShouldNotThrowAsync();
	}

	/// <summary>
	///     The idle delay's other exit: nothing due, nobody stops the Worker, and
	///     five real seconds pass — the delay completes on its own rather than by
	///     cancellation, and the loop goes back around to poll again rather than
	///     exiting. This is slow by design: it is the only way to observe that
	///     branch without giving the loop a fake clock, which nothing else in this
	///     codebase uses yet.
	/// </summary>
	[Fact]
	public async Task GivenNoDueMessage_WhenTheIdleIntervalElapsesOnItsOwn_ThenTheLoopPollsAgainWithoutStopping()
	{
		// Given — no processor registered, so the loop always idles.
		var connectionString = await postgres.CreateMigratedDatabaseAsync();
		var services = new ServiceCollection();
		services.AddDbContext<HpacSafetyDbContext>(options => options.UseNpgsql(connectionString));

		var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
		var worker = new Worker(scopeFactory, TimeProvider.System, new FakeLogger<Worker>());

		// When — outlive the 5-second idle interval without ever cancelling, so
		// this Task.Delay returns normally instead of throwing.
		await worker.StartAsync(CancellationToken.None);
		await Task.Delay(TimeSpan.FromSeconds(6));
		var stop = async () => await worker.StopAsync(CancellationToken.None);

		// Then — the loop is still alive and stops cleanly afterward.
		await stop.ShouldNotThrowAsync();
	}
}
