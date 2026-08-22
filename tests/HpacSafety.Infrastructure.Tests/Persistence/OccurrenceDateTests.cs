using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// The date a reporter gives is the day an accident happened, not a moment.
/// </summary>
/// <remarks>
/// This is not tidiness. `docs/anonymization-policy.md` narrows a published date
/// to a month and a year because a province, an exact date, an aircraft type,
/// and an injury severity together identify one person in a small flying
/// community. Storing that date as a moment invites a timezone conversion that
/// shifts it across midnight and silently changes which day an accident
/// happened on. See ADR-0035.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class OccurrenceDateTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset At = new(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);

    // Far enough from UTC, in both directions, that a moment near midnight
    // lands on a different day.
    private const string FarEast = "-c TimeZone=Pacific/Auckland";
    private const string FarWest = "-c TimeZone=America/Vancouver";

    [Fact]
    public async Task Given_a_migrated_database_When_the_occurrence_date_column_is_read_Then_it_is_a_date_and_not_a_moment()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT data_type FROM information_schema.columns WHERE table_name = 'reports' AND column_name = 'occurred_on'",
            connection);

        // Then — a `timestamptz` here would be a conversion waiting to happen.
        ((string?)await command.ExecuteScalarAsync()).ShouldBe("date");
    }

    [Theory]
    [InlineData(FarEast)]
    [InlineData(FarWest)]
    public async Task Given_an_occurrence_on_a_particular_day_When_it_is_read_back_in_another_timezone_Then_it_is_still_that_day(
        string sessionTimezone)
    {
        // Given — the last day of a month, so a shift of one day in either
        // direction also changes the month a summary would publish.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var occurredOn = new DateOnly(2026, 7, 31);
        var reportId = await StoreOccurrenceAsync(connectionString, occurredOn);

        // When
        await using var context = PostgresFixture.ContextFor(WithOptions(connectionString, sessionTimezone));
        var report = await context.Reports.SingleAsync(r => r.Id == reportId);

        // Then
        report.OccurredOn.ShouldBe(occurredOn);
    }

    [Theory]
    [InlineData(FarEast)]
    [InlineData(FarWest)]
    public async Task Given_an_occurrence_written_in_another_timezone_When_the_row_is_read_as_text_Then_the_day_is_the_one_the_reporter_gave(
        string sessionTimezone)
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var occurredOn = new DateOnly(2026, 1, 1);

        // When — written by a session on the other side of the world.
        await StoreOccurrenceAsync(WithOptions(connectionString, sessionTimezone), occurredOn);

        // Then — no DbContext, no converter, no session timezone in play.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT occurred_on::text FROM reports", connection);
        ((string?)await command.ExecuteScalarAsync()).ShouldBe("2026-01-01");
    }

    [Fact]
    public async Task Given_a_report_When_the_moment_it_was_submitted_is_read_back_Then_it_carries_its_offset()
    {
        // Given — when the SYSTEM did something is a moment, and a moment keeps
        // the offset it was recorded with.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = new Report(Locale.EnCa, At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        // When
        await using var reader = PostgresFixture.ContextFor(WithOptions(connectionString, FarEast));
        var stored = await reader.Reports.SingleAsync(r => r.Id == report.Id);

        // Then — the same instant, whatever the session's idea of local time.
        stored.SubmittedAt.ToUniversalTime().ShouldBe(At.ToUniversalTime());
    }

    private static string WithOptions(string connectionString, string options) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Options = options }.ConnectionString;

    private static async Task<TinyId> StoreOccurrenceAsync(string connectionString, DateOnly occurredOn)
    {
        await using var context = PostgresFixture.ContextFor(connectionString);
        var question = await context.Questions
            .Include(q => q.Versions)
            .SingleAsync(q => q.Key == "occurrence_date");

        var report = new Report(Locale.EnCa, At);
        report.Answer(question, occurredOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), At);
        report.OccurredOn.ShouldBe(occurredOn);

        context.Reports.Add(report);
        await context.SaveChangesAsync();
        return report.Id;
    }
}
