using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// The seeded local administrator, and the guard that keeps it off any database
/// that has not asked for it. See ADR-0020.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class DevelopmentAdminSeedTests(PostgresFixture postgres)
{
    private const string OptIn = "-c hpac.seed_development_admin=true";

    [Fact]
    public async Task Given_a_database_that_has_not_asked_for_it_When_the_migrations_are_applied_Then_no_administrator_is_seeded()
    {
        // Given — this is every database by default, production included.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        await using var context = PostgresFixture.ContextFor(connectionString);
        var administrators = await context.AdminUsers.ToListAsync();

        // Then
        administrators.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_development_database_that_opts_in_When_the_migrations_are_applied_Then_one_local_administrator_is_seeded()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync(OptIn);

        // When
        await using var context = PostgresFixture.ContextFor(connectionString);
        var administrators = await context.AdminUsers.ToListAsync();

        // Then — exactly one, obviously local, and not a real person.
        administrators.Count.ShouldBe(1);
        administrators[0].Subject.ShouldBe("admin@localhost");
        administrators[0].Role.ShouldBe(AdminRole.Administrator);
        administrators[0].IsActive.ShouldBeTrue();
        administrators[0].Id.ShouldBe(DevelopmentAdminSeed.Id);
    }

    [Fact]
    public async Task Given_a_database_that_opts_in_twice_When_the_seed_runs_again_Then_it_does_not_duplicate_the_row()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync(OptIn);

        // When
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await context.Database.ExecuteSqlRawAsync(DevelopmentAdminSeed.InsertSql());
        }

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        (await reader.AdminUsers.CountAsync()).ShouldBe(1);
    }
}
