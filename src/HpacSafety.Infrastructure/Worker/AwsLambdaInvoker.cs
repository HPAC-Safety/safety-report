using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda;
using Amazon.Lambda.Model;

namespace HpacSafety.Infrastructure.Worker;

/// <summary>
///     Invokes through the AWS SDK's own credential and region resolution —
///     the function's Lambda execution role in every deployed environment
///     (ADR-0033: the only place this codebase names <see cref="IAmazonLambda" />
///     directly).
/// </summary>
/// <remarks>
///     Excluded from coverage for the same reason as <c>FfmpegVideoRemuxer</c>'s
///     process-launching methods: it is a thin wrapper with nothing to unit
///     test except "does the AWS SDK's own client work", which needs a real
///     Lambda function to answer. <see cref="LambdaWorkerNudge" /> — the class
///     with an actual decision to test (swallow and log, never fail the
///     caller) — is covered exhaustively against a fake of this interface.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class AwsLambdaInvoker : ILambdaInvoker
{
	/// <inheritdoc />
	public async Task InvokeAsync(string functionName,
								  CancellationToken cancellationToken)
	{
		using var client = new AmazonLambdaClient();
		await client
			.InvokeAsync(
				new InvokeRequest
				{
					FunctionName = functionName,
					InvocationType = InvocationType.Event,

					// The nudge carries no content (ADR-0123) — the Worker
					// drains whatever is due, not what this call names.
					Payload = "{}",
				},
				cancellationToken)
			.ConfigureAwait(false);
	}
}
