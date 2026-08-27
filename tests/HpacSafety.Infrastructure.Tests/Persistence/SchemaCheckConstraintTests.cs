using HpacSafety.Infrastructure.Persistence;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// The canonical persistence specification requires database checks for
/// valid status/role/type/work codes and coherent nullable approval and
/// processing fields — see <c>docs/data-and-persistence.md</c> and
/// <c>MigrateCanonicalDomainAndPersistence</c>. Every constraint is proven
/// here by attempting the exact write it exists to reject.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SchemaCheckConstraintTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset At = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(
        "questions",
        "INSERT INTO questions (id, key, is_system, role, created_at) VALUES ('qqqqqqqqqq1', 'bad_role', FALSE, 'not_a_role', @at)")]
    [InlineData(
        "reports (language)",
        "INSERT INTO reports (id, language, status, submitted_at) VALUES ('rrrrrrrrrr1', 'de-DE', 'submitted', @at)")]
    [InlineData(
        "reports (status)",
        "INSERT INTO reports (id, language, status, submitted_at) VALUES ('rrrrrrrrrr2', 'en-CA', 'not_a_status', @at)")]
    [InlineData(
        "outbox_messages",
        "INSERT INTO outbox_messages (id, aggregate_id, type, payload, occurred_at, next_attempt_at, attempts) " +
        "VALUES ('oooooooooo1', 'rrrrrrrrrr1', 'not_a_type', '{}', @at, @at, 0)")]
    [InlineData(
        "admin_users",
        "INSERT INTO admin_users (id, member_identifier, role, is_active, created_at) " +
        "VALUES ('aaaaaaaaaa1', 'nobody@example.test', 'not_a_role', TRUE, @at)")]
    public async Task Given_a_bad_enum_code_When_it_is_inserted_Then_the_check_constraint_rejects_it(string table, string sql)
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var inserting = () => ExecuteAsync(connectionString, sql);

        // Then
        var exception = await Should.ThrowAsync<PostgresException>(inserting);
        exception.SqlState.ShouldBe("23514", $"{table} should reject an out-of-range enum code with a check-constraint violation.");
    }

    [Fact]
    public async Task Given_a_summary_with_only_one_of_approved_by_and_approved_at_set_When_it_is_inserted_Then_it_is_refused()
    {
        // Given — Summary.Approve/ClearApproval always set or clear both together
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await ExecuteAsync(
            connectionString,
            "INSERT INTO reports (id, language, status, submitted_at) VALUES ('rrrrrrrrrr3', 'en-CA', 'pending_review', @at)");

        // When
        var inserting = () => ExecuteAsync(
            connectionString,
            "INSERT INTO summaries (id, report_id, ai_summary_en, ai_summary_fr, model, prompt_version, approved_by, approved_at, generated_at, updated_at) " +
            "VALUES ('ssssssssss1', 'rrrrrrrrrr3', 'en', 'fr', 'model', 'v1', 'aaaaaaaaaa2', NULL, @at, @at)");

        // Then
        var exception = await Should.ThrowAsync<PostgresException>(inserting);
        exception.SqlState.ShouldBe("23514");
    }

    [Fact]
    public async Task Given_a_report_file_with_a_stripped_time_but_no_key_When_it_is_inserted_Then_it_is_refused()
    {
        // Given — AwaitsStripping treats exif_stripped_at and stripped_blob_key as one fact
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await ExecuteAsync(
            connectionString,
            "INSERT INTO reports (id, language, status, submitted_at) VALUES ('rrrrrrrrrr4', 'en-CA', 'pending_review', @at)");

        // When
        var inserting = () => ExecuteAsync(
            connectionString,
            "INSERT INTO report_files (id, report_id, kind, blob_key, content_type, byte_size, uploaded_at, exif_stripped_at, stripped_blob_key) " +
            "VALUES ('ffffffffff1', 'rrrrrrrrrr4', 'image', 'original/x', 'image/jpeg', 1, @at, @at, NULL)");

        // Then
        var exception = await Should.ThrowAsync<PostgresException>(inserting);
        exception.SqlState.ShouldBe("23514");
    }

    [Fact]
    public async Task Given_a_report_file_with_an_unknown_kind_When_it_is_inserted_Then_it_is_refused()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await ExecuteAsync(
            connectionString,
            "INSERT INTO reports (id, language, status, submitted_at) VALUES ('rrrrrrrrrr5', 'en-CA', 'pending_review', @at)");

        // When
        var inserting = () => ExecuteAsync(
            connectionString,
            "INSERT INTO report_files (id, report_id, kind, blob_key, content_type, byte_size, uploaded_at) " +
            "VALUES ('ffffffffff2', 'rrrrrrrrrr5', 'audio', 'original/x', 'audio/mpeg', 1, @at)");

        // Then
        var exception = await Should.ThrowAsync<PostgresException>(inserting);
        exception.SqlState.ShouldBe("23514");
    }

    [Fact]
    public async Task Given_a_question_revision_with_an_unknown_type_When_it_is_inserted_Then_it_is_refused()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await ExecuteAsync(
            connectionString,
            "INSERT INTO questions (id, key, is_system, role, created_at) VALUES ('qqqqqqqqqq2', 'some_key', FALSE, 'none', @at)");

        // When
        var inserting = () => ExecuteAsync(
            connectionString,
            "INSERT INTO question_revisions " +
            "(id, question_id, revision_number, type, is_system, is_required, is_private, is_active, display_order, label_en, label_fr, created_at) " +
            "VALUES ('vvvvvvvvvv1', 'qqqqqqqqqq2', 1, 'not_a_type', FALSE, FALSE, TRUE, TRUE, 0, 'a', 'b', @at)");

        // Then
        var exception = await Should.ThrowAsync<PostgresException>(inserting);
        exception.SqlState.ShouldBe("23514");
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("at", At);
        await command.ExecuteNonQueryAsync();
    }
}
