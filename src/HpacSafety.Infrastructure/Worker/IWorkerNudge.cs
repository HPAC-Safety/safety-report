namespace HpacSafety.Infrastructure.Worker;

/// <summary>
///     Invokes the Worker's Lambda function asynchronously, right after the API
///     commits a report, comment, or review action that queues outbox work
///     (ADR-0123). The nudge carries no content and only shortens the wait
///     until the EventBridge sweep would otherwise pick the work up — a failed
///     nudge never fails the request it rode in on, and calling it is never
///     required for correctness.
/// </summary>
public interface IWorkerNudge
{
	/// <summary>
	///     Asks the Worker to drain due outbox messages soon. Never throws: a
	///     failure is logged and swallowed by the implementation, because the
	///     EventBridge sweep is the delivery guarantee, not this call.
	/// </summary>
	Task NudgeAsync(CancellationToken cancellationToken);
}
