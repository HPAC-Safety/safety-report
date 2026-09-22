using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     Whichever of the API or the Worker starts first after a deploy applies
///     pending migrations; the other must not fail or double-apply them. See
///     ADR-0055.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class MigrationRunnerTests(PostgresFixture postgres)
{
	[Fact]
	public async Task GivenFreshDatabase_WhenTwoProcessesCallEnsureMigratedAsyncConcurrently_ThenBothSucceedAndSchemaIsMigratedOnce()
	{
		// Given
		var connectionString = await postgres.CreateDatabase();
		await using var first = PostgresFixture.ContextFor(connectionString);
		await using var second = PostgresFixture.ContextFor(connectionString);

		// When
		await Task.WhenAll(
			first.EnsureMigrated(NullLogger.Instance),
			second.EnsureMigrated(NullLogger.Instance));

		// Then
		await using var verify = PostgresFixture.ContextFor(connectionString);
		var pending = await verify.Database.GetPendingMigrationsAsync();
		pending.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenAlreadyMigratedDatabase_WhenEnsureMigratedAsyncRunsAgain_ThenNoOp()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);

		// When / Then — a second application on top of a fully migrated schema
		// must not throw, which is the case a Worker restart after the API has
		// already migrated exercises.
		await Should.NotThrowAsync(() => context.EnsureMigrated(NullLogger.Instance));
	}
}
