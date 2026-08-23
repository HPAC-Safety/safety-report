using HpacSafety.Infrastructure.Persistence.Seeding;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
/// The seeded local administrator exists so a developer can open the admin UI.
/// It must not be able to reach a real database. See ADR-0020.
/// </summary>
public sealed class DevelopmentAdminSeedSqlTests
{
    [Fact]
    public void Given_the_seed_statement_When_it_is_read_Then_it_writes_nothing_unless_the_database_asked_for_it()
    {
        // Given / When
        var sql = DevelopmentAdminSeed.InsertSql();

        // Then — the guard is in the SQL, so it is evaluated by the database
        // being changed rather than by whoever generated the script.
        sql.ShouldContain($"current_setting('{DevelopmentAdminSeed.SettingName}', true) = 'true'");
    }

    [Fact]
    public void Given_the_seed_statement_When_it_is_read_Then_it_seeds_one_obviously_local_identifier()
    {
        // Given / When
        var sql = DevelopmentAdminSeed.InsertSql();

        // Then
        DevelopmentAdminSeed.Subject.ShouldBe("admin@localhost");
        sql.ShouldContain("'admin@localhost'");
        sql.ShouldContain("INSERT INTO admin_users");
    }

    [Fact]
    public void Given_the_seed_statement_When_it_is_read_Then_it_does_nothing_a_second_time()
    {
        // Given / When
        var sql = DevelopmentAdminSeed.InsertSql();

        // Then
        sql.ShouldContain("NOT EXISTS");
    }

    [Fact]
    public void Given_the_seeded_administrator_When_its_identifier_is_derived_Then_it_is_stable()
    {
        // Given / When / Then
        DevelopmentAdminSeed.Id.ShouldBe(SeedIds.For("admin_user:admin@localhost"));
    }
}
