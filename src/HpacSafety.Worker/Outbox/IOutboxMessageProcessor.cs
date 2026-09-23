using HpacSafety.Core.Features.Outbox;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     Handles one kind of outbox work. The Worker's claim loop drains every
///     registered processor in turn — a new message type registers its own
///     processor rather than growing a branch inside the loop.
/// </summary>
public interface IOutboxMessageProcessor
{
	/// <summary>Which outbox message type this processor claims and handles.</summary>
	OutboxMessageType HandlesType { get; }

	/// <summary>
	///     Does the work one claimed message represents. Throwing fails the
	///     message — the claimer records the failure and reschedules or poisons
	///     it; this method itself never marks anything processed.
	/// </summary>
	Task Process(OutboxMessage message,
				 CancellationToken cancellationToken);
}
