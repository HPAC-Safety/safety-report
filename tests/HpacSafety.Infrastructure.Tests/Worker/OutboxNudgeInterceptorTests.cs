using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Tests.Persistence;
using HpacSafety.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Worker;

/// <summary>
///     <see cref="OutboxNudgeInterceptor" /> against a real
///     <see cref="HpacSafetyDbContext" />, exercising both the synchronous
///     <c>SaveChanges</c> path (nothing in this codebase calls it today, but
///     the interceptor covers it for correctness) and confirming a save that
///     queues nothing never nudges (ADR-0123).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class OutboxNudgeInterceptorTests(PostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenASynchronousSaveQueuesOutboxWork_WhenSaveChangesRuns_ThenTheWorkerIsNudgedOnce()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var nudge = new CountingNudge();
		var interceptor = new OutboxNudgeInterceptor(nudge, new FakeLogger<OutboxNudgeInterceptor>());

		await using var context = ContextWith(connectionString, interceptor);
		var narrative = Question.Create("nudge-sync", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isActive: true);
		context.Questions.Add(narrative);
		context.SaveChanges();

		var report = new Report(Locale.EnCa, At);
		report.Answer(narrative, "Synthetic.", At);
		context.Reports.Add(report);
		context.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At));

		// When — the synchronous overload, not SaveChangesAsync.
		context.SaveChanges();

		// Then
		nudge.Calls.ShouldBe(1);
	}

	[Fact]
	public async Task GivenASynchronousSaveQueuesNothing_WhenSaveChangesRuns_ThenTheWorkerIsNotNudged()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var nudge = new CountingNudge();
		var interceptor = new OutboxNudgeInterceptor(nudge, new FakeLogger<OutboxNudgeInterceptor>());

		await using var context = ContextWith(connectionString, interceptor);

		// When — a save that adds nothing outbox-shaped.
		context.Questions.Add(Question.Create("nudge-sync-none", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isActive: true));
		context.SaveChanges();

		// Then
		nudge.Calls.ShouldBe(0);
	}

	private static HpacSafetyDbContext ContextWith(string connectionString,
													OutboxNudgeInterceptor interceptor)
	{
		var options = new DbContextOptionsBuilder<HpacSafetyDbContext>()
			.UseNpgsql(connectionString)
			.AddInterceptors(interceptor)
			.Options;

		return new HpacSafetyDbContext(options);
	}

	private sealed class CountingNudge : IWorkerNudge
	{
		public int Calls { get; private set; }

		public Task NudgeAsync(CancellationToken cancellationToken)
		{
			Calls++;
			return Task.CompletedTask;
		}
	}
}
