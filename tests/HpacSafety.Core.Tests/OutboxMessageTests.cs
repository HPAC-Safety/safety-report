using HpacSafety.Core.Features.Outbox;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Failures back off exponentially and move aside after a poison
///     threshold rather than retrying forever.
/// </summary>
public class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenFailedMessage_WhenFailureIsRecorded_ThenNextAttemptIsDelayed()
    {
        // Given
        var message = new OutboxMessage(TinyId.New(), OutboxMessageType.SummarizeReport, "{}", Now);

        // When
        message.RecordFailure("timeout", Now);

        // Then
        message.Attempts.ShouldBe(1);
        message.NextAttemptAt.ShouldBeGreaterThan(Now);
        message.IsPoisoned.ShouldBeFalse();
    }

    [Fact]
    public void GivenRepeatedFailures_WhenThresholdIsCrossed_ThenMessageIsSetAside()
    {
        // Given
        var message = new OutboxMessage(TinyId.New(), OutboxMessageType.SummarizeReport, "{}", Now);

        // When
        for (var attempt = 0; attempt < OutboxMessage.PoisonThreshold; attempt++) message.RecordFailure("timeout", Now);

        // Then — set aside for a human rather than retried forever
        message.IsPoisoned.ShouldBeTrue();
        message.LastError.ShouldBe("timeout");
    }

    [Fact]
    public void GivenMessage_WhenProcessed_ThenLastErrorIsCleared()
    {
        // Given
        var message = new OutboxMessage(TinyId.New(), OutboxMessageType.SummarizeReport, "{}", Now);
        message.RecordFailure("timeout", Now);

        // When
        message.MarkProcessed(Now.AddMinutes(1));

        // Then
        message.IsProcessed.ShouldBeTrue();
        message.LastError.ShouldBeNull();
    }

    [Fact]
    public void GivenSuccessiveAttempts_WhenBackoffIsCalculated_ThenGrows()
    {
        // Given / When
        var first = OutboxMessage.BackoffFor(1);
        var third = OutboxMessage.BackoffFor(3);

        // Then
        third.ShouldBeGreaterThan(first);
    }
}
