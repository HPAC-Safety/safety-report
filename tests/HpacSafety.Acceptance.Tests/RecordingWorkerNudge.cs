using HpacSafety.Infrastructure.Worker;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Counts how many times the booted host nudged the Worker, so a scenario
///     can say a successful submission nudges it exactly once (ADR-0123,
///     REQ-SUB-118). Everything else about the request is unaffected — this
///     stands in for <see cref="LambdaWorkerNudge" /> only, never for the
///     drain itself.
/// </summary>
internal sealed class RecordingWorkerNudge : IWorkerNudge
{
	private static int _calls;

	/// <summary>How many nudges the booted host has sent since the last <see cref="Reset" />.</summary>
	public static int Calls => Volatile.Read(ref _calls);

	/// <summary>Zeroes the count. Call before the request under test.</summary>
	public static void Reset()
	{
		Volatile.Write(ref _calls, 0);
	}

	/// <inheritdoc />
	public Task NudgeAsync(CancellationToken cancellationToken)
	{
		Interlocked.Increment(ref _calls);
		return Task.CompletedTask;
	}
}
