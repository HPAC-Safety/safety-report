using HpacSafety.Infrastructure.Persistence.Seeding;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
/// <b>History.</b> The seeded local administrator wrote to a table that no
/// longer exists — ADR-0065 drops <c>admin_users</c>. These tests survive
/// because <c>20260823001528_InitialSchema</c> still calls the seed and a
/// committed migration is never edited: the SQL has to keep producing exactly
/// the bytes it always did, including against a fresh database where a later
/// migration then drops what it wrote.
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
        DevelopmentAdminSeed.MemberIdentifier.ShouldBe("admin@localhost");
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
    public void Given_the_seed_statement_When_the_role_enum_no_longer_exists_Then_it_still_writes_the_literal_code()
    {
        // Given / When
        var sql = DevelopmentAdminSeed.InsertSql();

        // Then — AdminRole was deleted with the table, so the code is now a
        // literal. A committed migration must not change the SQL it emits.
        sql.ShouldContain("'administrator'");
    }

    [Fact]
    public void Given_the_seeded_administrator_When_its_identifier_is_derived_Then_it_is_stable()
    {
        // Given / When / Then
        DevelopmentAdminSeed.Id.ShouldBe(SeedIds.For("admin_user:admin@localhost"));
    }
}
