using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
///     Applies pending migrations idempotently, whichever of the API or the
///     Worker calls it first after a deploy. See ADR-0055.
/// </summary>
public static partial class MigrationRunner
{
	/// <summary>
	///     A fixed, arbitrary key for the session-level advisory lock that
	///     serializes migration application. Any two processes calling
	///     <c>pg_advisory_lock</c> with the same key contend for the same lock,
	///     regardless of which database session holds it.
	/// </summary>
	private const long AdvisoryLockKey = 725_318_004_411;

	/// <summary>
	///     Takes a PostgreSQL advisory lock, then applies any migration still
	///     pending once the lock is held.
	/// </summary>
	/// <remarks>
	///     Pending migrations are checked only after the advisory lock is held,
	///     not before — a caller that blocked waiting for the lock may find the
	///     migration that was pending when it started already applied by whoever
	///     held the lock first, and this avoids doing that work twice.
	/// </remarks>
	/// <param name="context">The context whose schema is migrated.</param>
	/// <param name="logger">Where the outcome is logged.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	public static async Task EnsureMigratedAsync(
		this HpacSafetyDbContext context,
		ILogger logger,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(logger);

		var connection = context.Database.GetDbConnection();
		var wasClosed = connection.State != ConnectionState.Open;

		if (wasClosed)
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			await using (var acquireLock = connection.CreateCommand())
			{
				acquireLock.CommandText = "SELECT pg_advisory_lock(@key);";
				var parameter = acquireLock.CreateParameter();
				parameter.ParameterName = "key";
				parameter.Value = AdvisoryLockKey;
				acquireLock.Parameters.Add(parameter);
				await acquireLock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}

			try
			{
				var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

				if (pending.Count == 0)
				{
					LogNoPendingMigrations(logger);
					return;
				}

				LogApplyingMigrations(logger, pending.Count, pending[^1]);
				await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
				LogMigrationsApplied(logger);
			}
			finally
			{
				await using var releaseLock = connection.CreateCommand();
				releaseLock.CommandText = "SELECT pg_advisory_unlock(@key);";
				var parameter = releaseLock.CreateParameter();
				parameter.ParameterName = "key";
				parameter.Value = AdvisoryLockKey;
				releaseLock.Parameters.Add(parameter);
				await releaseLock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			if (wasClosed)
			{
				await connection.CloseAsync().ConfigureAwait(false);
			}
		}
	}

	[LoggerMessage(Level = LogLevel.Information, Message = "No pending migrations.")]
	private static partial void LogNoPendingMigrations(ILogger logger);

	[LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} pending migration(s), latest {LatestMigration}")]
	private static partial void LogApplyingMigrations(ILogger logger, int count, string latestMigration);

	[LoggerMessage(Level = LogLevel.Information, Message = "Migrations applied.")]
	private static partial void LogMigrationsApplied(ILogger logger);
}
