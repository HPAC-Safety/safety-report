using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     The claim/complete/fail semantics <see cref="OutboxClaimer" /> gives a
///     Worker, against a real database: only a due, unclaimed message of the
///     requested type is claimed; success marks it processed; a handler failure
///     records the error and reschedules rather than losing the message.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class OutboxClaimerTests(PostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenDueUnclaimedMessage_WhenClaimed_ThenHandlerRunsAndMessageIsMarkedProcessed()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		var message = new OutboxMessage(TinyId.New(), OutboxMessageType.TranslateAnswers, "payload", At);
		context.OutboxMessages.Add(message);
		await context.SaveChangesAsync();

		var handled = false;

		// When
		var claimed = await OutboxClaimer.ClaimNext(
			context, OutboxMessageType.TranslateAnswers, At,
			(_,
			 _) =>
			{
				handled = true;
				return Task.CompletedTask;
			},
			CancellationToken.None);

		// Then
		claimed.ShouldBeTrue();
		handled.ShouldBeTrue();

		await using var reader = PostgresFixture.ContextFor(connectionString);
		var stored = await reader.OutboxMessages.SingleAsync(m => m.Id == message.Id);
		stored.IsProcessed.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenMessageOfAnotherType_WhenClaimingADifferentType_ThenNothingIsClaimed()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		context.OutboxMessages.Add(new OutboxMessage(TinyId.New(), OutboxMessageType.SummarizeReport, "payload", At));
		await context.SaveChangesAsync();

		// When
		var claimed = await OutboxClaimer.ClaimNext(
			context, OutboxMessageType.TranslateAnswers, At,
			(_,
			 _) => throw new InvalidOperationException("Nothing of this type should have been claimed."),
			CancellationToken.None);

		// Then
		claimed.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenNoMessagesDue_WhenClaiming_ThenReturnsFalse()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);

		// When
		var claimed = await OutboxClaimer.ClaimNext(
			context, OutboxMessageType.TranslateAnswers, At,
			(_,
			 _) => Task.CompletedTask,
			CancellationToken.None);

		// Then
		claimed.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenMessageNotYetDue_WhenClaiming_ThenNothingIsClaimed()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		var future = At.AddMinutes(5);
		var message = new OutboxMessage(TinyId.New(), OutboxMessageType.TranslateAnswers, "payload", future);
		context.OutboxMessages.Add(message);
		await context.SaveChangesAsync();

		// When
		var claimed = await OutboxClaimer.ClaimNext(
			context, OutboxMessageType.TranslateAnswers, At,
			(_,
			 _) => throw new InvalidOperationException("A not-yet-due message should not have been claimed."),
			CancellationToken.None);

		// Then
		claimed.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenHandlerThrows_WhenClaimed_ThenFailureIsRecordedAndMessageStaysUnprocessed()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		var message = new OutboxMessage(TinyId.New(), OutboxMessageType.TranslateAnswers, "payload", At);
		context.OutboxMessages.Add(message);
		await context.SaveChangesAsync();

		// When
		var claimed = await OutboxClaimer.ClaimNext(
			context, OutboxMessageType.TranslateAnswers, At,
			(_,
			 _) => throw new InvalidOperationException("The translation provider is unreachable."),
			CancellationToken.None);

		// Then
		claimed.ShouldBeTrue();

		await using var reader = PostgresFixture.ContextFor(connectionString);
		var stored = await reader.OutboxMessages.SingleAsync(m => m.Id == message.Id);
		stored.IsProcessed.ShouldBeFalse();
		stored.Attempts.ShouldBe(1);
		stored.LastError.ShouldBe("The translation provider is unreachable.");
		stored.NextAttemptAt.ShouldBeGreaterThan(At);
	}
}
