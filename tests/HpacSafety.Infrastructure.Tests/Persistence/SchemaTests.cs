using HpacSafety.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>Checks the complete schema against PostgreSQL.</summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SchemaTests(PostgresFixture postgres)
{
    private static readonly string[] ExpectedTables =
    [
        "admin_users",
        "audit_log",
        "outbox_messages",
        "question_options",
        "questions",
        "report_answers",
        "reports",
        "summaries",
    ];

    [Fact]
    public async Task Given_a_clean_database_When_migrations_are_applied_Then_only_the_required_tables_exist()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        var tables = await QueryStringsAsync(
            connectionString,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory' ORDER BY table_name");

        tables.ShouldBe(ExpectedTables);
    }

    [Fact]
    public async Task Given_a_migrated_database_When_pending_migrations_are_checked_Then_there_are_none()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);

        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_seeded_database_When_the_seed_is_run_again_Then_it_is_an_idempotent_no_op()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var before = await RowCountsAsync(connectionString);

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(QuestionBankSeedWriter.Sql(), connection);
            await Should.NotThrowAsync(() => command.ExecuteNonQueryAsync());
        }

        (await RowCountsAsync(connectionString)).ShouldBe(before);
    }

    [Fact]
    public async Task Given_a_migrated_database_When_the_outbox_index_is_read_Then_only_claimable_rows_are_indexed()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        var definitions = await QueryStringsAsync(
            connectionString,
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'outbox_messages' AND indexname = 'ix_outbox_messages_claimable'");

        definitions.Length.ShouldBe(1);
        definitions[0].ShouldContain("next_attempt_at");
        definitions[0].ShouldContain("processed_at IS NULL");
        definitions[0].ShouldContain("poisoned_at IS NULL");
    }

    [Fact]
    public async Task Given_a_migrated_database_When_summary_indexes_are_read_Then_there_can_be_only_one_per_report()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        var definitions = await QueryStringsAsync(
            connectionString,
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'summaries' AND indexdef LIKE '%UNIQUE%'");

        definitions.ShouldContain(definition =>
            definition.Contains("report_id", StringComparison.Ordinal)
            && !definition.Contains("language", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_a_migrated_database_When_an_answer_column_is_read_Then_option_codes_are_an_array()
    {
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        var types = await QueryStringsAsync(
            connectionString,
            "SELECT data_type FROM information_schema.columns WHERE table_name = 'report_answers' AND column_name = 'selected_option_codes'");

        types.ShouldBe(["ARRAY"]);
    }

    private static async Task<Dictionary<string, long>> RowCountsAsync(string connectionString)
    {
        string[] seededTables = ["questions", "question_options"];
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var table in seededTables)
        {
            await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
            counts[table] = (long)(await command.ExecuteScalarAsync())!;
        }

        return counts;
    }

    private static async Task<string[]> QueryStringsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }
}
