using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     One pass across every registered <see cref="IOutboxMessageProcessor" />,
///     claiming and running at most one due message per processor. Shared by
///     the developer-mode polling loop (<see cref="Worker" />) and the Lambda
///     drain-once entry point (<c>Program.cs</c>), so both hosts claim, back
///     off, and poison exactly the same way (ADR-0123).
/// </summary>
public static class OutboxDrainPass
{
	/// <summary>
	///     Claims and runs at most one due message per registered processor, in
	///     a fresh scope.
	/// </summary>
	/// <returns>True if any processor claimed a due message this pass.</returns>
	public static async Task<bool> RunOnce(
		IServiceScopeFactory scopeFactory,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(clock);

		var claimedAny = false;

		await using var scope = scopeFactory.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var processors = scope.ServiceProvider.GetServices<IOutboxMessageProcessor>();

		foreach (var processor in processors)
		{
			var claimed = await OutboxClaimer
				.ClaimNext(database, processor.HandlesType, clock.GetUtcNow(), processor.Process, cancellationToken)
				.ConfigureAwait(false);

			claimedAny |= claimed;
		}

		return claimedAny;
	}

	/// <summary>
	///     Repeats <see cref="RunOnce" /> until a pass claims nothing or the time
	///     budget is nearly spent. A Lambda invocation drains once and returns
	///     rather than idling — polling (<see cref="Worker" />) stays the source
	///     of truth, so a lost or throttled invocation only delays work until the
	///     next EventBridge sweep (ADR-0123).
	/// </summary>
	/// <param name="scopeFactory">Creates the scope each pass claims and processes within.</param>
	/// <param name="clock">The clock messages are claimed against.</param>
	/// <param name="remainingTime">
	///     The invocation's remaining time budget, re-read every pass — a Lambda
	///     context's own remaining time, or a fixed budget in a host with none.
	/// </param>
	/// <param name="safetyMargin">
	///     Stop claiming new work once less than this much time is left, so the
	///     in-flight message still has room to finish and commit.
	/// </param>
	/// <param name="cancellationToken">Cancels the drain early.</param>
	public static async Task DrainUntilIdleOrOutOfTime(
		IServiceScopeFactory scopeFactory,
		TimeProvider clock,
		Func<TimeSpan> remainingTime,
		TimeSpan safetyMargin,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(remainingTime);

		while (!cancellationToken.IsCancellationRequested && remainingTime() > safetyMargin)
		{
			var claimedAny = await RunOnce(scopeFactory, clock, cancellationToken).ConfigureAwait(false);
			if (!claimedAny)
			{
				return;
			}
		}
	}
}
