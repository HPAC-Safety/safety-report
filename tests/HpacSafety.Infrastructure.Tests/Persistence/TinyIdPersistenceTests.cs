using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// Identifiers in the database: one shape everywhere, and a collision handled
/// rather than assumed away. See ADR-0034.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class TinyIdPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset At = new(2026, 8, 22, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_a_migrated_database_When_every_identifier_column_is_read_Then_all_of_them_are_the_same_eleven_character_type()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();

        // When — every primary key and every column that references one.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT table_name || '.' || column_name || ' ' || data_type || '(' || COALESCE(character_maximum_length, 0) || ')'
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND (column_name = 'id' OR column_name LIKE '%\_id' OR column_name = 'approved_by')
            ORDER BY 1
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        // Then — no mixed-type joins, and nothing left as uuid.
        columns.Count.ShouldBeGreaterThan(15);
        columns.ShouldAllBe(column => column.EndsWith("character(11)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Given_a_saved_report_When_its_identifier_is_read_out_of_postgres_Then_it_is_eleven_characters_of_the_alphabet()
    {
        // Given
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = new Report(Locale.EnCa, At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        // When — read as raw text, with no converter in the way.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM reports", connection);
        var stored = (string?)await command.ExecuteScalarAsync();

        // Then
        stored.ShouldNotBeNull();
        stored.Length.ShouldBe(TinyId.Length);
        stored.ShouldAllBe(character => TinyId.Alphabet.Contains(character, StringComparison.Ordinal));
        TinyId.Parse(stored).ShouldBe(report.Id);
    }

    [Fact]
    public async Task Given_a_new_row_that_draws_an_identifier_already_in_use_When_it_is_saved_Then_it_is_given_a_fresh_one()
    {
        // Given — a collision at sixty-six bits is vanishingly unlikely, which
        // is not the same as handled. Forced here, because waiting for one is
        // not a test.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var first = new Report(Locale.EnCa, At);
        context.Reports.Add(first);
        await context.SaveChangesAsync();

        // A second context, because the first one is still tracking `first` and
        // would reject the duplicate before the database ever saw it.
        await using var colliding = PostgresFixture.ContextFor(connectionString);
        var second = new Report(Locale.FrCa, At);
        colliding.Reports.Add(second).Property("Id").CurrentValue = first.Id;

        // When
        await colliding.SaveChangesAsync();

        // Then — two reports, two identifiers, no overwritten report.
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var ids = await reader.Reports.Select(report => report.Id).ToListAsync();
        ids.Count.ShouldBe(2);
        ids.Distinct().Count().ShouldBe(2);
        ids.ShouldContain(first.Id);
    }

    [Fact]
    public async Task Given_a_collision_inside_a_transaction_When_it_is_retried_Then_the_rest_of_the_transaction_survives()
    {
        // Given — the report endpoint writes a report and its outbox row in one
        // transaction. A retry must not cost ADR-0002's guarantee.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var first = new Report(Locale.EnCa, At);
        context.Reports.Add(first);
        await context.SaveChangesAsync();

        await using var colliding = PostgresFixture.ContextFor(connectionString);
        var second = new Report(Locale.FrCa, At);

        // When
        await using (var transaction = await colliding.Database.BeginTransactionAsync())
        {
            colliding.Reports.Add(second).Property("Id").CurrentValue = first.Id;
            colliding.OutboxMessages.Add(new OutboxMessage(first.Id, "report.submitted", "{}", At));

            await colliding.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Then — the retry rolled back to a savepoint, not to the transaction.
        await using var reader = PostgresFixture.ContextFor(connectionString);
        (await reader.Reports.CountAsync()).ShouldBe(2);
        (await reader.OutboxMessages.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Given_an_outbox_message_naming_a_report_that_had_to_take_a_new_identifier_When_both_are_saved_Then_it_names_the_new_one()
    {
        // Given — the outbox names a report by value, with no foreign key, so
        // EF cannot fix it up. A retry that left it pointing at the identifier
        // the report lost would commit a message about a report that is not
        // there.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var first = new Report(Locale.EnCa, At);
        context.Reports.Add(first);
        await context.SaveChangesAsync();

        await using var colliding = PostgresFixture.ContextFor(connectionString);
        var second = new Report(Locale.FrCa, At);
        colliding.Reports.Add(second).Property("Id").CurrentValue = first.Id;
        colliding.OutboxMessages.Add(new OutboxMessage(first.Id, "report.submitted", "{}", At));

        // When
        await colliding.SaveChangesAsync();

        // Then
        await using var reader = PostgresFixture.ContextFor(connectionString);
        var message = await reader.OutboxMessages.SingleAsync();
        message.AggregateId.ShouldBe(second.Id);
        message.AggregateId.ShouldNotBe(first.Id);
        (await reader.Reports.AnyAsync(report => report.Id == message.AggregateId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Given_a_unique_constraint_the_domain_put_there_When_it_is_violated_Then_it_is_reported_rather_than_retried_away()
    {
        // Given — one summary per language per report is a rule, not luck. The
        // retry must not paper over it by minting a new identifier and trying
        // again forever.
        var connectionString = await postgres.CreateMigratedDatabaseAsync();
        await using var context = PostgresFixture.ContextFor(connectionString);
        var report = new Report(Locale.EnCa, At);
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        context.Summaries.Add(Summary.Generated(report.Id, Locale.EnCa, "One.", "model", "v1", At));
        context.Summaries.Add(Summary.Generated(report.Id, Locale.EnCa, "Two.", "model", "v1", At));

        // When / Then
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
