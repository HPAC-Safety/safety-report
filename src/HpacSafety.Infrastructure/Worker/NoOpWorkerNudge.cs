namespace HpacSafety.Infrastructure.Worker;

/// <summary>
///     Used whenever no Worker Lambda function is configured to nudge —
///     Development, and the acceptance/integration test hosts. Polling (the
///     Worker's own loop in Development, or the EventBridge sweep once
///     deployed) is always the source of truth, so doing nothing here changes
///     no observable behavior other than the wait.
/// </summary>
public sealed class NoOpWorkerNudge : IWorkerNudge
{
	/// <inheritdoc />
	public Task NudgeAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
