using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>Failures back off exponentially and move aside after a poison
/// threshold rather than retrying forever.</summary>
public class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_failed_message_When_the_failure_is_recorded_Then_the_next_attempt_is_delayed()
    {
        // Given
        var message = new OutboxMessage(Guid.NewGuid(), "summarize", "{}", Now);

        // When
        message.RecordFailure("timeout", Now);

        // Then
        message.Attempts.ShouldBe(1);
        message.NextAttemptAt.ShouldBeGreaterThan(Now);
        message.IsPoisoned.ShouldBeFalse();
    }

    [Fact]
    public void Given_repeated_failures_When_the_threshold_is_crossed_Then_the_message_is_set_aside()
    {
        // Given
        var message = new OutboxMessage(Guid.NewGuid(), "summarize", "{}", Now);

        // When
        for (var attempt = 0; attempt < OutboxMessage.PoisonThreshold; attempt++)
        {
            message.RecordFailure("timeout", Now);
        }

        // Then — set aside for a human rather than retried forever
        message.IsPoisoned.ShouldBeTrue();
        message.LastError.ShouldBe("timeout");
    }

    [Fact]
    public void Given_a_message_When_it_is_processed_Then_the_last_error_is_cleared()
    {
        // Given
        var message = new OutboxMessage(Guid.NewGuid(), "summarize", "{}", Now);
        message.RecordFailure("timeout", Now);

        // When
        message.MarkProcessed(Now.AddMinutes(1));

        // Then
        message.IsProcessed.ShouldBeTrue();
        message.LastError.ShouldBeNull();
    }

    [Fact]
    public void Given_successive_attempts_When_the_backoff_is_calculated_Then_it_grows()
    {
        // Given / When
        var first = OutboxMessage.BackoffFor(1);
        var third = OutboxMessage.BackoffFor(3);

        // Then
        third.ShouldBeGreaterThan(first);
    }
}
