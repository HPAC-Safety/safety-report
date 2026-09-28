namespace HpacSafety.Infrastructure.Worker;

/// <summary>
///     Invokes a Lambda function asynchronously, with no payload and no
///     response awaited beyond the request being accepted. Third-party
///     coupling stays at this one small edge (ADR-0033) rather than exposing
///     the AWS SDK's own client type to <see cref="LambdaWorkerNudge" />'s
///     caller — a test fakes this, never the SDK.
/// </summary>
public interface ILambdaInvoker
{
	/// <summary>Invokes <paramref name="functionName" />, fire-and-forget.</summary>
	Task InvokeAsync(string functionName,
					 CancellationToken cancellationToken);
}
