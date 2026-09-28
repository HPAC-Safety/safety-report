using Microsoft.Extensions.Logging;

namespace HpacSafety.Infrastructure.Worker;

/// <summary>
///     Invokes the Worker's Lambda function, fire-and-forget. See ADR-0123.
/// </summary>
public sealed partial class LambdaWorkerNudge(
	ILambdaInvoker invoker,
	string functionName,
	ILogger<LambdaWorkerNudge> logger) : IWorkerNudge
{
	/// <inheritdoc />
	public async Task NudgeAsync(CancellationToken cancellationToken)
	{
		try
		{
			await invoker.InvokeAsync(functionName, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// The EventBridge sweep is the delivery guarantee; a lost nudge
			// only delays work up to a minute. Never fails the request it rode
			// in on.
			LogNudgeFailed(logger, exception);
		}
	}

	[LoggerMessage(Level = LogLevel.Warning, Message = "Nudging the Worker failed; the next EventBridge sweep will pick the work up.")]
	private static partial void LogNudgeFailed(ILogger logger, Exception exception);
}
