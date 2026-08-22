namespace HpacSafety.Core.Features.Outbox;

/// <summary>
/// Work to be done, written in the same transaction as the report that caused
/// it. Summarization, translation, and notification all ride this — there is no
/// "save, then notify", because that loses reports whenever the process dies
/// between the two.
/// </summary>
public class OutboxMessage
{
    /// <summary>Attempts after which a message is set aside rather than retried forever.</summary>
    public const int PoisonThreshold = 5;

    /// <summary>Queues work against an aggregate.</summary>
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private OutboxMessage()
    {
    }
#pragma warning restore CS8618

    public OutboxMessage(Guid aggregateId, string type, string payload, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        AggregateId = aggregateId;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
        NextAttemptAt = occurredAt;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The report, or other aggregate, this work is about.</summary>
    public Guid AggregateId { get; private init; }

    /// <summary>What kind of work this is.</summary>
    public string Type { get; private init; }

    /// <summary>
    /// The message body. Identifiers, not report content — an outbox row is read
    /// by logs and operators. See docs/data-handling.md.
    /// </summary>
    public string Payload { get; private init; }

    /// <summary>When the work became necessary.</summary>
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>When it completed.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Earliest time a worker should claim this row.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>How many times it has been tried.</summary>
    public int Attempts { get; private set; }

    /// <summary>The most recent failure.</summary>
    public string? LastError { get; private set; }

    /// <summary>When it was set aside as poison.</summary>
    public DateTimeOffset? PoisonedAt { get; private set; }

    /// <summary>True once it has completed.</summary>
    public bool IsProcessed => ProcessedAt is not null;

    /// <summary>True once it has been set aside for a human.</summary>
    public bool IsPoisoned => PoisonedAt is not null;

    /// <summary>Marks the work done.</summary>
    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at;
        LastError = null;
    }

    /// <summary>
    /// Records a failure, backing off exponentially and moving the message aside
    /// once it crosses <see cref="PoisonThreshold"/> rather than retrying it
    /// forever.
    /// </summary>
    public void RecordFailure(string error, DateTimeOffset at)
    {
        Attempts++;
        LastError = error;

        if (Attempts >= PoisonThreshold)
        {
            PoisonedAt = at;
            return;
        }

        NextAttemptAt = at + BackoffFor(Attempts);
    }

    /// <summary>The delay before attempt <paramref name="attempts"/> + 1.</summary>
    public static TimeSpan BackoffFor(int attempts) =>
        TimeSpan.FromSeconds(Math.Pow(2, Math.Clamp(attempts, 1, 10)));
}
