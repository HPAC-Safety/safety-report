using System.Globalization;

using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// Identity is a token subject and nothing else: no user table, no foreign
/// key, and an existing audit row's attribution survives the migration that
/// took the table away. See ADR-0065.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class TokenSubjectIdentityTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Given_a_migrated_database_When_the_tables_are_listed_Then_admin_users_is_absent()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var present = await ScalarAsync(
            connectionString,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'admin_users'");

        // Then — the fresh path creates the table in InitialSchema and this
        // migration drops it moments later; both paths end here.
        present.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_migrated_database_When_the_identity_columns_are_read_Then_neither_carries_a_foreign_key()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var foreignKeys = await ScalarAsync(
            connectionString,
            """
            SELECT COUNT(*)
            FROM information_schema.key_column_usage k
            JOIN information_schema.table_constraints c
              ON c.constraint_name = k.constraint_name
            WHERE c.constraint_type = 'FOREIGN KEY'
              AND ((k.table_name = 'audit_log' AND k.column_name = 'actor_subject')
                OR (k.table_name = 'summaries' AND k.column_name = 'approved_by_subject'))
            """);

        // Then — there is nothing to reference.
        foreignKeys.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_migrated_database_When_the_actor_column_is_read_Then_it_is_a_widened_string_with_a_lookup_index()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        var width = await ScalarAsync(
            connectionString,
            "SELECT character_maximum_length FROM information_schema.columns WHERE table_name = 'audit_log' AND column_name = 'actor_subject'");

        var indexes = await ScalarAsync(
            connectionString,
            "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'audit_log' AND indexname = 'ix_audit_log_actor_subject'");

        // Then — a provider's subject is not an eleven-character tiny id.
        width.ShouldBe(256);
        indexes.ShouldBe(1);
    }

    [Fact]
    public async Task Given_an_audit_row_written_with_a_token_subject_When_it_is_read_back_Then_the_subject_survives_the_round_trip()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        const string subject = "auth0|synthetic-officer-0123456789";
        var targetId = TinyId.New();

        await using (var writer = PostgresFixture.ContextFor(connectionString))
        {
            writer.AuditLog.Add(new AuditLogEntry(
                subject, AuditAction.ViewedRawReport, nameof(Report), targetId, DateTimeOffset.UtcNow));
            await writer.SaveChangesAsync();
        }

        // When
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var entry = await reader.AuditLog.SingleAsync();

        // Then — longer than a tiny id, and stored verbatim rather than padded
        // or truncated.
        entry.ActorSubject.ShouldBe(subject);
    }

    [Fact]
    public async Task Given_an_approved_summary_When_the_approver_is_read_back_Then_it_is_an_opaque_string()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        const string approver = "auth0|synthetic-approver";
        var reportId = TinyId.New();

        await using (var writer = PostgresFixture.ContextFor(connectionString))
        {
            var report = new Report(Locale.EnCa, DateTimeOffset.UtcNow);
            var summary = Summary.Generate(
                report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", DateTimeOffset.UtcNow);
            summary.Approve(approver, DateTimeOffset.UtcNow);
            report.AttachSummary(summary);

            writer.Reports.Add(report);
            await writer.SaveChangesAsync();
            reportId = report.Id;
        }

        // When
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var stored = await reader.Summaries.SingleAsync(summary => summary.ReportId == reportId);

        // Then
        stored.ApprovedBySubject.ShouldBe(approver);
        stored.IsApproved.ShouldBeTrue();
    }

    [Fact]
    public async Task Given_a_summary_approval_When_only_one_half_of_the_pair_is_set_Then_the_database_refuses_it()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When — the check constraint now names approved_by_subject; if the
        // rename left it pointing at the old column this insert would succeed.
        var writing = async () => await ExecuteAsync(
            connectionString,
            $"""
             INSERT INTO summaries (id, report_id, ai_summary_en, ai_summary_fr, model, prompt_version, approved_by_subject, approved_at, generated_at, updated_at)
             VALUES ('{TinyId.New()}', '{TinyId.New()}', 'en', 'fr', 'model', 'v1', 'auth0|someone', NULL, now(), now())
             """);

        // Then — 23514 is a check-constraint violation. Asserting the state
        // rather than just the exception type matters here: if the rename had
        // left the constraint naming the old column, this insert would fail
        // with 42703 (undefined column) and a bare type assertion would pass.
        var exception = await writing.ShouldThrowAsync<PostgresException>();
        exception.SqlState.ShouldBe("23514");
    }

    private static async Task<int> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
