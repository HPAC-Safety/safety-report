using System.Data;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Proves the Testcontainers harness works — a real PostgreSQL container starts,
///     accepts a connection, and is torn down.
///     <para>
///         There is no schema to assert against yet; the DbContext and its migrations
///         arrive with the database issue. This exists now because every later
///         integration test depends on this harness, and a harness that has never run is
///         a harness nobody can distinguish from a broken one.
///     </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class PostgresContainerTests : IAsyncLifetime
{
	// Pinned rather than floating on `latest`: a database version that changes
	// underneath the suite is a test failure nobody can reproduce.
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

	public Task InitializeAsync()
	{
		return _postgres.StartAsync();
	}

	public Task DisposeAsync()
	{
		return _postgres.DisposeAsync().AsTask();
	}

	[Fact]
	public async Task GivenPostgresContainer_WhenConnectionIsOpened_ThenSucceeds()
	{
		// Given
		await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());

		// When
		await connection.OpenAsync();

		// Then
		connection.State.ShouldBe(ConnectionState.Open);
	}

	[Fact]
	public async Task GivenPostgresContainer_WhenServerVersionIsRead_ThenPinnedMajorVersion()
	{
		// Given
		await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
		await connection.OpenAsync();

		// When
		await using var command = new NpgsqlCommand("SHOW server_version;", connection);
		var version = (string?)await command.ExecuteScalarAsync();

		// Then
		version.ShouldNotBeNull();
		version.ShouldStartWith("17.");
	}
}
