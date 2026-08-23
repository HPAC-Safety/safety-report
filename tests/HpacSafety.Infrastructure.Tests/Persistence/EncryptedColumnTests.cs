using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// Contact details are private: encrypted at rest, and unreadable without
/// the key. See <c>docs/data-handling.md</c> and ADR-0019.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class EncryptedColumnTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset At = new(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);

    private const string ReporterName = "Vince Bergeron";
    private const string ReporterPhone = "403-555-0134";

    [Fact]
    public async Task Given_a_reporter_s_contact_details_When_the_row_is_read_straight_out_of_postgres_Then_the_plaintext_is_not_there()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var reportId = await StoreContactDetailsAsync(connectionString);

        // When — no DbContext, no converter, no key. This is what a database
        // backup, a psql session, or a stolen dump sees.
        var stored = await RawAnswerValuesAsync(connectionString, reportId);

        // Then
        stored.Length.ShouldBe(2);
        foreach (var value in stored)
        {
            value.ShouldNotContain(ReporterName);
            value.ShouldNotContain("Bergeron");
            value.ShouldNotContain(ReporterPhone);
            value.ShouldStartWith("v1.");
        }
    }

    [Fact]
    public async Task Given_a_reporter_s_contact_details_When_they_are_read_back_with_the_key_Then_they_come_back_as_written()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var reportId = await StoreContactDetailsAsync(connectionString);

        // When
        await using var context = PostgresFixture.ContextFor(connectionString);
        var answers = await context.ReportAnswers
            .Where(answer => answer.ReportId == reportId)
            .OrderBy(answer => answer.QuestionKey)
            .ToListAsync();

        // Then
        answers.Select(answer => answer.Value).ShouldBe([ReporterName, ReporterPhone]);
    }

    [Fact]
    public async Task Given_a_reporter_s_contact_details_When_they_are_read_back_with_a_different_key_Then_they_cannot_be_read()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var reportId = await StoreContactDetailsAsync(connectionString);

        // When
        await using var context = PostgresFixture.ContextFor(connectionString, PostgresFixture.OtherKey);

        // Then — not wrong text, and not empty text. No text.
        await Should.ThrowAsync<FieldDecryptionException>(
            () => context.ReportAnswers.Where(answer => answer.ReportId == reportId).ToListAsync());
    }

    [Fact]
    public async Task Given_two_reporters_giving_the_same_phone_number_When_the_stored_values_are_compared_Then_they_are_not_visibly_the_same()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var first = await StoreContactDetailsAsync(connectionString);
        var second = await StoreContactDetailsAsync(connectionString);

        // When
        var one = await RawAnswerValuesAsync(connectionString, first);
        var other = await RawAnswerValuesAsync(connectionString, second);

        // Then — a fresh nonce per value, so equal plaintexts do not line up in
        // a dump and give away who reported twice.
        one.Intersect(other, StringComparer.Ordinal).ShouldBeEmpty();
    }

    [Fact]
    public async Task Given_an_unanswered_question_When_the_row_is_read_straight_out_of_postgres_Then_it_is_still_visibly_unanswered()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = new Report(Locale.EnCa, At);
        var damage = await QuestionAsync(context, "damage");
        report.Answer(damage, (string?)null, At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        // When
        var stored = await RawAnswerValuesAsync(connectionString, report.Id, includeNulls: true);

        // Then — encryption must not turn "no answer" into a value.
        stored.ShouldBe([string.Empty]);
    }

    private static async Task<TinyId> StoreContactDetailsAsync(string connectionString)
    {
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = new Report(Locale.EnCa, At);

        report.Answer(await QuestionAsync(context, "reporter_first_name"), ReporterName, At);
        report.Answer(await QuestionAsync(context, "reporter_phone"), ReporterPhone, At);

        context.Reports.Add(report);
        await context.SaveChangesAsync();
        return report.Id;
    }

    private static Task<Question> QuestionAsync(HpacSafetyDbContext context, string key) =>
        context.Questions.SingleAsync(q => q.Key == key && q.IsActive);

    private static async Task<string[]> RawAnswerValuesAsync(
        string connectionString, TinyId reportId, bool includeNulls = false)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(value, '') FROM report_answers WHERE report_id = @report ORDER BY question_key",
            connection);
        command.Parameters.AddWithValue("report", reportId.Value);

        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            var value = reader.GetString(0);
            if (includeNulls || value.Length > 0)
            {
                values.Add(value);
            }
        }

        return [.. values];
    }
}
