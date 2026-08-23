using HpacSafety.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>dotnet ef database update</c> against a clean PostgreSQL 17, and the
/// shape it leaves behind.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SchemaTests(PostgresFixture postgres)
{
    private static readonly string[] ExpectedTables =
    [
        "admin_users",
        "audit_log",
        "outbox_messages",
        "question_option_translations",
        "question_options",
        "question_translations",
        "question_versions",
        "questions",
        "report_aircraft",
        "report_answers",
        "report_files",
        "reports",
        "summaries",
    ];

    [Fact]
    public async Task Given_a_clean_postgres_17_When_the_migrations_are_applied_Then_every_table_exists()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var tables = await QueryStringsAsync(
            connectionString,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory' ORDER BY table_name");

        // Then
        tables.ShouldBe(ExpectedTables);
    }

    [Fact]
    public async Task Given_a_migrated_database_When_pending_migrations_are_checked_Then_there_are_none()
    {
        // Given — this proves EF's own bookkeeping, not that the migration's
        // content is safe to re-run. `dotnet ef database update` reads
        // __EFMigrationsHistory and never re-invokes a migration already
        // recorded there, so this alone cannot catch a non-idempotent
        // statement inside one. See the seed-reapplication tests below.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        await using var context = PostgresFixture.ContextFor(connectionString);
        var pending = await context.Database.GetPendingMigrationsAsync();

        // Then
        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_a_seeded_database_When_the_question_bank_seed_is_executed_a_second_time_Then_it_does_not_error()
    {
        // Given — the scenario __EFMigrationsHistory cannot protect against:
        // a future migration re-seeding after an administrator emptied the
        // question bank by hand, or a deployment script re-run outside EF's
        // own tracking. Every row is written with a WHERE NOT EXISTS guard on
        // its own identifier, the same shape DevelopmentAdminSeed uses.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // When / Then — this line is what would have failed before the guard:
        // EF's InsertData has no WHERE NOT EXISTS, so running the same insert
        // twice violates the primary key on the second pass.
        await using var command = new NpgsqlCommand(QuestionBankSeedWriter.Sql(), connection);
        await Should.NotThrowAsync(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Given_a_seeded_database_When_the_question_bank_seed_is_executed_a_second_time_Then_it_writes_no_duplicate_rows()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var before = await RowCountsAsync(connectionString);

        // When
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(QuestionBankSeedWriter.Sql(), connection);
            await command.ExecuteNonQueryAsync();
        }

        // Then — a safe no-op, not merely a survivable one.
        var after = await RowCountsAsync(connectionString);
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Given_a_migrated_database_When_the_outbox_index_is_read_Then_it_covers_only_the_rows_a_worker_may_claim()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var definitions = await QueryStringsAsync(
            connectionString,
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'outbox_messages' AND indexname = 'ix_outbox_messages_claimable'");

        // Then — without the filter the claim query reads the whole processed
        // history for the rest of the system's life.
        definitions.Length.ShouldBe(1);
        definitions[0].ShouldContain("next_attempt_at");
        definitions[0].ShouldContain("processed_at IS NULL");
        definitions[0].ShouldContain("poisoned_at IS NULL");
    }

    [Fact]
    public async Task Given_a_migrated_database_When_the_summaries_table_is_read_Then_one_language_may_appear_once_per_report()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var definitions = await QueryStringsAsync(
            connectionString,
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'summaries' AND indexdef LIKE '%UNIQUE%'");

        // Then — a reviewer must never have to choose between two French
        // summaries of the same report.
        definitions.ShouldContain(d => d.Contains("report_id", StringComparison.Ordinal) && d.Contains("language", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_a_migrated_database_When_an_answer_column_is_read_Then_the_option_codes_are_stored_as_an_array()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var types = await QueryStringsAsync(
            connectionString,
            "SELECT data_type FROM information_schema.columns WHERE table_name = 'report_answers' AND column_name = 'selected_option_codes'");

        // Then
        types.ShouldBe(["ARRAY"]);
    }

    private static async Task<Dictionary<string, long>> RowCountsAsync(string connectionString)
    {
        string[] seededTables =
        [
            "questions", "question_versions", "question_translations", "question_options", "question_option_translations",
        ];

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
