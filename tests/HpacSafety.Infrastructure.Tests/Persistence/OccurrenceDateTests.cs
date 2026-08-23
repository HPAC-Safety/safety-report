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

    [Fact]
    public async Task Given_the_clock_time_a_reporter_gave_When_the_row_is_read_straight_out_of_postgres_Then_the_precise_time_is_not_there()
    {
        // Given — the exact minute is private. Province, day, aircraft type,
        // injury severity, and a time of day already narrow to one person in a
        // small flying community; the minute makes it certain.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await StoreOccurrenceTimeAsync(connectionString, new TimeOnly(15, 20));

        // When — no DbContext, no converter, no key.
        var stored = await ScalarAsync(connectionString, "SELECT occurred_at_local FROM reports");

        // Then
        stored.ShouldNotBeNull();
        stored.ShouldNotContain("15:20");
        stored.ShouldNotContain("15");
        stored.ShouldStartWith("v1.");
    }

    [Fact]
    public async Task Given_the_clock_time_a_reporter_gave_When_it_is_read_back_with_the_key_Then_it_is_the_time_they_gave()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var reportId = await StoreOccurrenceTimeAsync(connectionString, new TimeOnly(15, 20));

        // When
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = await context.Reports.SingleAsync(r => r.Id == reportId);

        // Then
        report.OccurredAtLocal.ShouldBe(new TimeOnly(15, 20));
    }

    [Fact]
    public async Task Given_the_clock_time_a_reporter_gave_When_it_is_read_back_with_a_different_key_Then_it_cannot_be_read()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await StoreOccurrenceTimeAsync(connectionString, new TimeOnly(15, 20));

        // When
        await using var context = PostgresFixture.ContextFor(connectionString, PostgresFixture.OtherKey);

        // Then
        await Should.ThrowAsync<FieldDecryptionException>(() => context.Reports.ToListAsync());
    }

    [Fact]
    public async Task Given_a_report_with_a_precise_time_When_the_coarse_bucket_is_read_Then_it_is_readable_without_the_key()
    {
        // Given — the bucket is what a summary publishes and what analysis
        // groups by, so it is deliberately in the clear beside the ciphertext.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await StoreOccurrenceTimeAsync(connectionString, new TimeOnly(15, 20));

        // When
        var bucket = await ScalarAsync(connectionString, "SELECT time_of_day FROM reports");

        // Then
        bucket.ShouldBe("afternoon");
    }

    [Fact]
    public async Task Given_a_reporter_who_did_not_give_a_time_When_the_report_is_read_back_Then_it_says_do_not_know_rather_than_midnight()
    {
        // Given — #68 makes the time optional so somebody who does not remember
        // still files. Midnight is a real answer; this is not one.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        var reportId = await StoreOccurrenceTimeAsync(connectionString, localTime: null);

        // When
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = await context.Reports.SingleAsync(r => r.Id == reportId);

        // Then
        report.OccurredAtLocal.ShouldBeNull();
        report.TimeOfDay.ShouldBe(TimeOfDay.Unknown);
        (await ScalarAsync(connectionString, "SELECT occurred_at_local FROM reports")).ShouldBeNull();
        (await ScalarAsync(connectionString, "SELECT time_of_day FROM reports")).ShouldBe("unknown");
    }

    [Theory]
    [InlineData(10, 59, "morning")]
    [InlineData(11, 0, "mid_day")]
    [InlineData(13, 59, "mid_day")]
    [InlineData(14, 0, "afternoon")]
    [InlineData(16, 59, "afternoon")]
    [InlineData(17, 0, "evening")]
    public async Task Given_a_clock_time_on_a_bucket_boundary_When_the_report_is_stored_Then_the_stored_bucket_is_the_derived_one(
        int hour, int minute, string expected)
    {
        // Given — the boundaries are defined once, on TimeOfDay. This proves
        // the derivation survives the round trip rather than being recomputed
        // differently somewhere between the domain and the column.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await StoreOccurrenceTimeAsync(connectionString, new TimeOnly(hour, minute));

        // When
        var bucket = await ScalarAsync(connectionString, "SELECT time_of_day FROM reports");

        // Then
        bucket.ShouldBe(expected);
    }

    private static async Task<string?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull or null ? null : (string)value;
    }

    private static async Task<TinyId> StoreOccurrenceTimeAsync(string connectionString, TimeOnly? localTime)
    {
        await using var context = PostgresFixture.ContextFor(connectionString);

        // #68 adds this question to the form and regenerates docs/form-spec.md.
        // The storage contract it will land on is the one under test here.
        var question = Question.Create(
            "occurrence_time", QuestionType.ShortText, Locale.EnCa, "What time?", At,
            role: QuestionRole.OccurrenceTime);
        context.Questions.Add(question);

        var report = new Report(Locale.EnCa, At);
        report.Answer(question, localTime?.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), At);
        context.Reports.Add(report);

        await context.SaveChangesAsync();
        return report.Id;
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
