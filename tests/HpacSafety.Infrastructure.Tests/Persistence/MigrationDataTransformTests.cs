using System.Security.Cryptography;
using System.Text;

using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// The explicit, tested rules <c>MigrateCanonicalDomainAndPersistence</c>
/// applies to data written under the schema it replaces: complete a question
/// revision missing its French counterpart from the English wording, and
/// collapse two per-language summary rows into one bilingual row, approved
/// only when both languages were.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class MigrationDataTransformTests(PostgresFixture postgres)
{
    private const string PriorMigration = "ReplaceSensitivityWithQuestionPrivacy";

    [Fact]
    public async Task Given_a_question_version_with_no_French_translation_When_the_migration_runs_Then_the_revision_inherits_the_English_wording()
    {
        // Given — current-main allows a version with only its source
        // translation, before a machine translation is attached. The target
        // model has nowhere to put "missing"; a revision is complete or it
        // does not exist.
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, PriorMigration);
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                """
                INSERT INTO questions (id, key, is_system, role, is_private, display_order, section_key, is_active, created_at, deleted_at)
                VALUES ('qqqqqqqqqqq', 'untranslated_field', FALSE, 'none', TRUE, 0, NULL, TRUE, TIMESTAMPTZ '2026-08-22T00:00:00Z', NULL);
                INSERT INTO question_versions (id, question_id, version_number, type, is_required, created_at)
                VALUES ('vvvvvvvvvvv', 'qqqqqqqqqqq', 1, 'short_text', FALSE, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO question_translations (id, question_version_id, locale, label, help_text, placeholder, is_source, is_machine_translated, translated_at, updated_at)
                VALUES ('ttttttttttt', 'vvvvvvvvvvv', 'en-CA', 'Untranslated field', NULL, NULL, TRUE, FALSE, NULL, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                """);
        }

        // When
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, targetMigration: null);
        }

        // Then
        await using var reader = new NpgsqlConnection(connectionString);
        await reader.OpenAsync();
        var (labelEn, labelFr) = await ReadRevisionLabelsAsync(reader, "vvvvvvvvvvv");
        labelEn.ShouldBe("Untranslated field");
        labelFr.ShouldBe("Untranslated field");
    }

    [Fact]
    public async Task Given_two_approved_per_language_summaries_When_the_migration_runs_Then_they_become_one_approved_bilingual_row()
    {
        // Given
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, PriorMigration);
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                """
                INSERT INTO reports (id, language, status, submitted_at, consent_publish, occurred_on, occurred_at_local, province, time_of_day, pilot_injury, passenger_injury, summary_error)
                VALUES ('rrrrrrrrrrr', 'en-CA', 'pending_review', TIMESTAMPTZ '2026-08-22T00:00:00Z', TRUE, NULL, NULL, 'not_answered', 'not_answered', 'not_answered', 'not_answered', NULL);
                INSERT INTO admin_users (id, member_identifier, role, is_active, created_at)
                VALUES ('aaaaaaaaaaa', 'officer@example.test', 'safety_officer', TRUE, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO summaries (id, report_id, language, text, model, prompt_version, is_source, translated_from_summary_id, approved_by, approved_at, created_at)
                VALUES ('s1s1s1s1s1s', 'rrrrrrrrrrr', 'en-CA', 'A pilot landed hard.', 'model', 'v1', TRUE, NULL, 'aaaaaaaaaaa', TIMESTAMPTZ '2026-08-23T00:00:00Z', TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO summaries (id, report_id, language, text, model, prompt_version, is_source, translated_from_summary_id, approved_by, approved_at, created_at)
                VALUES ('s2s2s2s2s2s', 'rrrrrrrrrrr', 'fr-CA', 'Un pilote a atterri durement.', 'model', 'v1', FALSE, 's1s1s1s1s1s', 'aaaaaaaaaaa', TIMESTAMPTZ '2026-08-24T00:00:00Z', TIMESTAMPTZ '2026-08-22T00:00:00Z');
                """);
        }

        // When
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, targetMigration: null);
        }

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var summary = await reader.Summaries.SingleAsync(s => s.ReportId == TinyId.Parse("rrrrrrrrrrr"));
        summary.AiSummaryEn.ShouldBe("A pilot landed hard.");
        summary.AiSummaryFr.ShouldBe("Un pilote a atterri durement.");
        summary.IsApproved.ShouldBeTrue();
        summary.ApprovedAt.ShouldBe(new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Given_only_one_language_approved_When_the_migration_runs_Then_the_merged_pair_starts_unapproved()
    {
        // Given — the pair is approved only if every existing language row
        // was individually approved; a half-approved pair is not a defined
        // state in the target model, so the merged row starts unapproved
        // rather than guessing.
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, PriorMigration);
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                """
                INSERT INTO reports (id, language, status, submitted_at, consent_publish, occurred_on, occurred_at_local, province, time_of_day, pilot_injury, passenger_injury, summary_error)
                VALUES ('rrrrrrrrrr2', 'en-CA', 'pending_review', TIMESTAMPTZ '2026-08-22T00:00:00Z', TRUE, NULL, NULL, 'not_answered', 'not_answered', 'not_answered', 'not_answered', NULL);
                INSERT INTO admin_users (id, member_identifier, role, is_active, created_at)
                VALUES ('aaaaaaaaaa2', 'officer2@example.test', 'safety_officer', TRUE, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO summaries (id, report_id, language, text, model, prompt_version, is_source, translated_from_summary_id, approved_by, approved_at, created_at)
                VALUES ('s1s1s1s1s12', 'rrrrrrrrrr2', 'en-CA', 'A pilot landed hard.', 'model', 'v1', TRUE, NULL, 'aaaaaaaaaa2', TIMESTAMPTZ '2026-08-23T00:00:00Z', TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO summaries (id, report_id, language, text, model, prompt_version, is_source, translated_from_summary_id, approved_by, approved_at, created_at)
                VALUES ('s2s2s2s2s22', 'rrrrrrrrrr2', 'fr-CA', 'Un pilote a atterri durement.', 'model', 'v1', FALSE, 's1s1s1s1s12', NULL, NULL, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                """);
        }

        // When
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, targetMigration: null);
        }

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var summary = await reader.Summaries.SingleAsync(s => s.ReportId == TinyId.Parse("rrrrrrrrrr2"));
        summary.AiSummaryEn.ShouldBe("A pilot landed hard.");
        summary.AiSummaryFr.ShouldBe("Un pilote a atterri durement.");
        summary.IsApproved.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_an_encrypted_legacy_answer_value_When_the_migration_runs_Then_the_value_is_plaintext()
    {
        // Given — current-main stores every non-null report_answers.value as
        // v1 AES-GCM ciphertext (see the now-deleted EncryptedStringConverter
        // and AesGcmFieldCipher). The new mapping reads the column directly
        // with no converter, so an upgraded row must contain plaintext.
        const string plaintext = "The pilot reported a hard landing near the threshold.";
        const string keyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        var ciphertext = EncryptLegacyV1(plaintext, keyBase64);

        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, PriorMigration);
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO questions (id, key, is_system, role, is_private, display_order, section_key, is_active, created_at, deleted_at)
                VALUES ('qqqqqqqqqq3', 'narrative', FALSE, 'none', TRUE, 0, NULL, TRUE, TIMESTAMPTZ '2026-08-22T00:00:00Z', NULL);
                INSERT INTO question_versions (id, question_id, version_number, type, is_required, created_at)
                VALUES ('vvvvvvvvvv3', 'qqqqqqqqqq3', 1, 'long_text', FALSE, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO question_translations (id, question_version_id, locale, label, help_text, placeholder, is_source, is_machine_translated, translated_at, updated_at)
                VALUES ('tttttttttt3', 'vvvvvvvvvv3', 'en-CA', 'Narrative', NULL, NULL, TRUE, FALSE, NULL, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO reports (id, language, status, submitted_at, consent_publish, occurred_on, occurred_at_local, province, time_of_day, pilot_injury, passenger_injury, summary_error)
                VALUES ('rrrrrrrrrr3', 'en-CA', 'pending_review', TIMESTAMPTZ '2026-08-22T00:00:00Z', TRUE, NULL, NULL, 'not_answered', 'not_answered', 'not_answered', 'not_answered', NULL);
                INSERT INTO report_answers (id, report_id, question_id, question_version_id, question_key, is_private, value, selected_option_codes, answered_at)
                VALUES ('answer_enc1', 'rrrrrrrrrr3', 'qqqqqqqqqq3', 'vvvvvvvvvv3', 'narrative', TRUE, @value, '{}', TIMESTAMPTZ '2026-08-22T00:00:00Z');
                """,
                connection);
            command.Parameters.AddWithValue("value", ciphertext);
            await command.ExecuteNonQueryAsync();
        }

        // When
        Environment.SetEnvironmentVariable("HpacSafety__FieldEncryption__Key", keyBase64);
        try
        {
            await using var context = PostgresFixture.ContextFor(connectionString);
            await MigrateToAsync(context, targetMigration: null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HpacSafety__FieldEncryption__Key", null);
        }

        // Then
        await using var reader = new NpgsqlConnection(connectionString);
        await reader.OpenAsync();
        await using var select = new NpgsqlCommand("SELECT value FROM report_answers WHERE id = 'answer_enc1'", reader);
        var value = (string?)await select.ExecuteScalarAsync();
        value.ShouldBe(plaintext);
    }

    [Fact]
    public async Task Given_only_one_approved_legacy_summary_row_When_the_migration_runs_Then_the_merged_pair_starts_unapproved()
    {
        // Given — a report that only ever had one language's summary
        // generated and approved. The migration's earlier UPDATEs duplicate
        // that one text into the missing language column, but nobody
        // produced or reviewed that second language, so the merged pair must
        // not inherit the single row's approval.
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, PriorMigration);
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                """
                INSERT INTO reports (id, language, status, submitted_at, consent_publish, occurred_on, occurred_at_local, province, time_of_day, pilot_injury, passenger_injury, summary_error)
                VALUES ('rrrrrrrrrr4', 'en-CA', 'pending_review', TIMESTAMPTZ '2026-08-22T00:00:00Z', TRUE, NULL, NULL, 'not_answered', 'not_answered', 'not_answered', 'not_answered', NULL);
                INSERT INTO admin_users (id, member_identifier, role, is_active, created_at)
                VALUES ('aaaaaaaaaa4', 'officer4@example.test', 'safety_officer', TRUE, TIMESTAMPTZ '2026-08-22T00:00:00Z');
                INSERT INTO summaries (id, report_id, language, text, model, prompt_version, is_source, translated_from_summary_id, approved_by, approved_at, created_at)
                VALUES ('s1s1s1s1s14', 'rrrrrrrrrr4', 'en-CA', 'A pilot landed hard.', 'model', 'v1', TRUE, NULL, 'aaaaaaaaaa4', TIMESTAMPTZ '2026-08-23T00:00:00Z', TIMESTAMPTZ '2026-08-22T00:00:00Z');
                """);
        }

        // When
        await using (var context = PostgresFixture.ContextFor(connectionString))
        {
            await MigrateToAsync(context, targetMigration: null);
        }

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var summary = await reader.Summaries.SingleAsync(s => s.ReportId == TinyId.Parse("rrrrrrrrrr4"));
        summary.AiSummaryEn.ShouldBe("A pilot landed hard.");
        summary.AiSummaryFr.ShouldBe("A pilot landed hard.");
        summary.IsApproved.ShouldBeFalse();
    }

    /// <summary>
    /// Reproduces the stored format the now-deleted AesGcmFieldCipher wrote —
    /// "v1." + base64(nonce[12] || tag[16] || ciphertext) — purely to build a
    /// legacy fixture. Does not reintroduce the deleted cipher.
    /// </summary>
    private static string EncryptLegacyV1(string plaintext, string keyBase64)
    {
        var key = Convert.FromBase64String(keyBase64);
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        var tag = new byte[16];
        var ct = new byte[bytes.Length];

        using (var aes = new AesGcm(key, tag.Length))
        {
            aes.Encrypt(nonce, bytes, ct, tag);
        }

        var envelope = new byte[nonce.Length + tag.Length + ct.Length];
        Buffer.BlockCopy(nonce, 0, envelope, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, envelope, nonce.Length, tag.Length);
        Buffer.BlockCopy(ct, 0, envelope, nonce.Length + tag.Length, ct.Length);

        return "v1." + Convert.ToBase64String(envelope);
    }

    private static async Task MigrateToAsync(HpacSafetyDbContext context, string? targetMigration)
    {
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(string LabelEn, string LabelFr)> ReadRevisionLabelsAsync(NpgsqlConnection connection, string revisionId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT label_en, label_fr FROM question_revisions WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", revisionId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetString(0), reader.GetString(1));
    }
}
