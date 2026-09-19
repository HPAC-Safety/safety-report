using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// Whichever of the API or the Worker starts first after a deploy applies
/// pending migrations; the other must not fail or double-apply them. See
/// ADR-0055.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class MigrationRunnerTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Given_a_fresh_database_When_two_processes_call_EnsureMigratedAsync_concurrently_Then_both_succeed_and_the_schema_is_migrated_once()
    {
        // Given
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var first = PostgresFixture.ContextFor(connectionString);
        await using var second = PostgresFixture.ContextFor(connectionString);

        // When
        await Task.WhenAll(
            first.EnsureMigratedAsync(NullLogger.Instance),
            second.EnsureMigratedAsync(NullLogger.Instance));

        // Then
        await using var verify = PostgresFixture.ContextFor(connectionString);
        var pending = await verify.Database.GetPendingMigrationsAsync();
        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_an_already_migrated_database_When_EnsureMigratedAsync_runs_again_Then_it_is_a_no_op()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        // When / Then — a second application on top of a fully migrated schema
        // must not throw, which is the case a Worker restart after the API has
        // already migrated exercises.
        await Should.NotThrowAsync(() => context.EnsureMigratedAsync(NullLogger.Instance));
    }
}
