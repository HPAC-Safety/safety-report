namespace HpacSafety.Core.Features.Outbox;

/// <summary>
/// What kind of work an outbox message carries. Stored as a stable invariant
/// code, like every other domain enum. See <c>docs/data-and-persistence.md</c>.
/// </summary>
public enum OutboxMessageType
{
    /// <summary>Summarize a report: the Worker's one model call per report.</summary>
    SummarizeReport = 0,
}
