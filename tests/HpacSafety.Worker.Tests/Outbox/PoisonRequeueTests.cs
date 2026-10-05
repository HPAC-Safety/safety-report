using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     The operator requeue tool the NAT-outage runbook needs (issue #467):
///     invoking the Worker Lambda with a <c>{"requeue":"poison"}</c> payload
///     is handled by <see cref="PoisonRequeue" /> once <c>Program.cs</c> has
///     parsed the payload — these scenarios exercise that logic against a
///     real database (.spec/features/domain-and-lifecycle, REQ-DOM-016/017).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class PoisonRequeueTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenAPoisonedMessage_WhenRequeued_ThenItsPoisonStateIsClearedAndItBecomesClaimableImmediately()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var reportId = await SeedReportAsync(connectionString);
		var poisonedAt = At.AddMinutes(10);

		var messageId = TinyId.New();
		await using (var seed = WorkerPostgresFixture.ContextFor(connectionString))
		{
			var message = new OutboxMessage(reportId, OutboxMessageType.SummarizeReport, reportId.Value, At);
			for (var attempt = 0; attempt < OutboxMessage.PoisonThreshold; attempt++)
			{
				message.RecordFailure("synthetic failure", poisonedAt);
			}

			message.IsPoisoned.ShouldBeTrue();
			messageId = message.Id;
			seed.OutboxMessages.Add(message);
			await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		var logger = new FakeLogger<PoisonRequeueTests>();

		// When
		await using var db = WorkerPostgresFixture.ContextFor(connectionString);
		var result = await PoisonRequeue.Requeue(db, poisonedAt.AddMinutes(5), from: null, to: null, logger, CancellationToken.None);

		// Then
		result.RequeuedCount.ShouldBe(1);
		result.RequeuedIds.ShouldBe([messageId.ToString()]);

		await using var check = WorkerPostgresFixture.ContextFor(connectionString);
		var requeued = await check.OutboxMessages.SingleAsync(m => m.Id == messageId, cancellationToken: TestContext.Current.CancellationToken);
		requeued.IsPoisoned.ShouldBeFalse();
		requeued.Attempts.ShouldBe(0);
		requeued.NextAttemptAt.ShouldBe(poisonedAt.AddMinutes(5));
	}

	[Fact]
	public async Task GivenMessagesPoisonedBeforeAndWithinAWindow_WhenRequeuedWithThatWindow_ThenOnlyTheOneWithinItIsRequeued()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var reportId = await SeedReportAsync(connectionString);

		var before = new OutboxMessage(reportId, OutboxMessageType.SummarizeReport, reportId.Value, At);
		var within = new OutboxMessage(reportId, OutboxMessageType.TranslateAnswers, reportId.Value, At);

		var beforePoisonedAt = At.AddHours(-2);
		var windowFrom = At.AddHours(-1);
		var windowTo = At;
		var withinPoisonedAt = At.AddMinutes(-30);

		await using (var seed = WorkerPostgresFixture.ContextFor(connectionString))
		{
			for (var attempt = 0; attempt < OutboxMessage.PoisonThreshold; attempt++)
			{
				before.RecordFailure("synthetic failure", beforePoisonedAt);
				within.RecordFailure("synthetic failure", withinPoisonedAt);
			}

			seed.OutboxMessages.AddRange(before, within);
			await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		var logger = new FakeLogger<PoisonRequeueTests>();

		// When
		await using var db = WorkerPostgresFixture.ContextFor(connectionString);
		var result = await PoisonRequeue.Requeue(db, At, windowFrom, windowTo, logger, CancellationToken.None);

		// Then
		result.RequeuedCount.ShouldBe(1);
		result.RequeuedIds.ShouldBe([within.Id.ToString()]);

		await using var check = WorkerPostgresFixture.ContextFor(connectionString);
		(await check.OutboxMessages.SingleAsync(m => m.Id == before.Id, cancellationToken: TestContext.Current.CancellationToken)).IsPoisoned.ShouldBeTrue();
		(await check.OutboxMessages.SingleAsync(m => m.Id == within.Id, cancellationToken: TestContext.Current.CancellationToken)).IsPoisoned.ShouldBeFalse();
	}

	[Fact]
	public void GivenAPayload_ThenOnlyTheLiteralPoisonValueIsRecognized()
	{
		PoisonRequeue.IsPoisonRequeue(new PoisonRequeue.Request { Requeue = "poison" }).ShouldBeTrue();
		PoisonRequeue.IsPoisonRequeue(new PoisonRequeue.Request { Requeue = "POISON" }).ShouldBeTrue();
		PoisonRequeue.IsPoisonRequeue(new PoisonRequeue.Request { Requeue = "something-else" }).ShouldBeFalse();
		PoisonRequeue.IsPoisonRequeue(request: null).ShouldBeFalse();
	}

	private static async Task<TinyId> SeedReportAsync(string connectionString)
	{
		await using var seed = WorkerPostgresFixture.ContextFor(connectionString);
		var narrative = Question.Create(
			"narrative-requeue", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isActive: true);
		seed.Questions.Add(narrative);
		await seed.SaveChangesAsync();

		var report = new Report(Locale.EnCa, At);
		report.Answer(narrative, "Wind picked up on final.", At);
		seed.Reports.Add(report);
		await seed.SaveChangesAsync();

		return report.Id;
	}
}
