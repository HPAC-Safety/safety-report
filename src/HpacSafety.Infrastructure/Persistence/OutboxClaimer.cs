using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
///     Claims and completes one outbox message at a time, safely under concurrent
///     Worker instances. See <c>OutboxMessageConfiguration.ClaimableFilter</c> and
///     <c>ix_outbox_messages_claimable</c>, which this query is built to use.
/// </summary>
public static class OutboxClaimer
{
	/// <summary>
	///     Claims the oldest due, unclaimed message of <paramref name="type" />, runs
	///     <paramref name="handler" /> against it, and commits the result — success
	///     marks it processed, failure records the error and reschedules it (or
	///     poisons it past <see cref="OutboxMessage.PoisonThreshold" />). Uses
	///     <c>FOR UPDATE SKIP LOCKED</c> so two Worker instances never claim the same
	///     row.
	/// </summary>
	/// <returns>
	///     True if a message was claimed and handled (whether it succeeded or
	///     failed), false if none was due — the caller can stop draining.
	/// </returns>
	public static async Task<bool> ClaimNext(
		HpacSafetyDbContext database,
		OutboxMessageType type,
		DateTimeOffset now,
		Func<OutboxMessage, CancellationToken, Task> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(handler);

		await using var transaction = await database.Database
			.BeginTransactionAsync(cancellationToken)
			.ConfigureAwait(false);

		// FromSqlInterpolated does not run the enum's configured value
		// converter, so the invariant code is passed explicitly rather than
		// the C# enum, which Npgsql would otherwise send as an int.
		var typeCode = EnumCode.Of(type);

		var claimed = await database.OutboxMessages
			.FromSqlInterpolated(
				$"""
				 SELECT * FROM outbox_messages
				 WHERE type = {typeCode}
				   AND processed_at IS NULL
				   AND poisoned_at IS NULL
				   AND next_attempt_at <= {now}
				 ORDER BY next_attempt_at
				 LIMIT 1
				 FOR UPDATE SKIP LOCKED
				 """)
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (claimed is null)
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			return false;
		}

		try
		{
			await handler(claimed, cancellationToken).ConfigureAwait(false);
			claimed.MarkProcessed(now);
		}
		catch (Exception cause) when (cause is not OperationCanceledException)
		{
			// A failure still commits: RecordFailure moves NextAttemptAt (or
			// PoisonedAt past the threshold) so the row is not re-claimed on
			// every poll forever. See OutboxMessage.RecordFailure.
			claimed.RecordFailure(cause.Message, now);
		}

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		return true;
	}
}
