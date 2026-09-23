using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     One PostgreSQL 17 container for this suite, mirroring
///     <c>HpacSafety.Infrastructure.Tests.Persistence.PostgresFixture</c> — the
///     translation processor needs a real database (raw SQL claim, EF Core
///     migrations) and there is no shared test-fixtures project to pull the
///     original from.
/// </summary>
public sealed class WorkerPostgresFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

	/// <summary>Starts the container.</summary>
	public Task InitializeAsync()
	{
		return _postgres.StartAsync();
	}

	/// <summary>Stops and removes the container.</summary>
	public Task DisposeAsync()
	{
		return _postgres.DisposeAsync().AsTask();
	}

	/// <summary>Creates an empty database, migrates it, and returns a connection string.</summary>
	public async Task<string> CreateMigratedDatabase()
	{
		var name = "db_" + Guid.NewGuid().ToString("n");

		await using (var maintenance = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString()))
		{
			await maintenance.OpenAsync();
			await using var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{name}\"", maintenance);
			await create.ExecuteNonQueryAsync();
		}

		var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
		{
			Database = name,
		}.ConnectionString;

		await using var context = ContextFor(connectionString);
		await context.Database.MigrateAsync();
		return connectionString;
	}

	/// <summary>Opens a context against an existing database.</summary>
	public static HpacSafetyDbContext ContextFor(string connectionString)
	{
		var options = new DbContextOptionsBuilder<HpacSafetyDbContext>()
			.UseNpgsql(connectionString)
			.Options;

		return new HpacSafetyDbContext(options);
	}
}

/// <summary>Shares one container across every test class in this suite.</summary>
[CollectionDefinition(Name)]
public sealed class SharedWorkerPostgres : ICollectionFixture<WorkerPostgresFixture>
{
	/// <summary>The collection name.</summary>
	public const string Name = "PostgreSQL 17 (Worker)";
}
