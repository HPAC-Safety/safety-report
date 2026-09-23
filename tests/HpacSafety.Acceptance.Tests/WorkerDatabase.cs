using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     A real PostgreSQL database for the Worker scenarios that describe what the
///     outbox claim query and the persisted outcome actually do — statements no
///     domain-only assertion can honestly make. One container for the whole run,
///     started on first use; each scenario gets its own fresh, migrated database.
/// </summary>
/// <remarks>
///     Deliberately not <see cref="BootedApi" />'s container: that one boots an HTTP
///     host for scenarios about what the API refuses over the wire. These scenarios
///     never touch HTTP — they exercise <c>SummarizationAttemptRunner</c> directly
///     against a database, the same way <c>HpacSafety.Infrastructure.Tests</c> does.
/// </remarks>
public static class WorkerDatabase
{
	private static readonly SemaphoreSlim Gate = new(1, 1);
	private static PostgreSqlContainer? container;

	/// <summary>Creates an empty, migrated database and returns a context open on it.</summary>
	public static async Task<HpacSafetyDbContext> NewMigratedContext()
	{
		var context = ContextFor(await NewEmptyDatabase().ConfigureAwait(false));
		await context.Database.MigrateAsync().ConfigureAwait(false);
		return context;
	}

	/// <summary>
	///     Creates an empty database with no migration applied and returns its
	///     connection string, for scenarios about what a migration does.
	/// </summary>
	public static async Task<string> NewEmptyDatabase()
	{
		var running = await Container().ConfigureAwait(false);
		var name = "db_" + Guid.NewGuid().ToString("n");

		await using (var maintenance = new NpgsqlConnection(running.GetConnectionString()))
		{
			await maintenance.OpenAsync().ConfigureAwait(false);
			await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", maintenance);
			await create.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		return new NpgsqlConnectionStringBuilder(running.GetConnectionString()) { Database = name }.ConnectionString;
	}

	/// <summary>Opens another context against the database a scenario is already using.</summary>
	public static HpacSafetyDbContext ContextFor(HpacSafetyDbContext existing)
	{
		ArgumentNullException.ThrowIfNull(existing);

		return ContextFor(existing.Database.GetConnectionString()!);
	}

	/// <summary>Opens a context against a database by its connection string.</summary>
	public static HpacSafetyDbContext ContextFor(string connectionString)
	{
		var options = new DbContextOptionsBuilder<HpacSafetyDbContext>().UseNpgsql(connectionString).Options;
		return new HpacSafetyDbContext(options);
	}

	private static async Task<PostgreSqlContainer> Container()
	{
		if (container is not null)
		{
			return container;
		}

		await Gate.WaitAsync().ConfigureAwait(false);
		try
		{
			if (container is null)
			{
				var starting = new PostgreSqlBuilder("postgres:17-alpine").Build();
				await starting.StartAsync().ConfigureAwait(false);
				container = starting;
			}
		}
		finally
		{
			Gate.Release();
		}

		return container;
	}

	/// <summary>Stops the container once, after the whole run.</summary>
	[AfterTestRun]
	public static async Task Stop()
	{
		if (container is not null)
		{
			await container.DisposeAsync().ConfigureAwait(false);
			container = null;
		}
	}
}
