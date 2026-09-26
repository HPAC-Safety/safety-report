using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>AllowFutureDatesOnDateQuestions</c>: every date question that existed
///     before the setting, the seeded occurrence date included, refuses future
///     dates afterwards, with no wording changed and no revision created; and only
///     a date revision may allow them (ADR-0138).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class FutureDatesMigrationTests(PostgresFixture postgres)
{
	private const string PriorMigration = "PinChoicesFirstOrLast";

	[Fact]
	public async Task GivenExistingDateQuestions_WhenMigrationRuns_ThenEachRefusesFutureDatesWithNoNewRevision()
	{
		// Given — the seeded bank, as it stood before the setting existed
		var connectionString = await postgres.CreateDatabase();
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, PriorMigration);
		}

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		var before = await Revisions(connection, "type = 'date'");
		before.ShouldNotBeEmpty();
		var everyRevisionBefore = await Count(connection, "SELECT count(*) FROM question_revisions");

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, null);
		}

		// Then
		var after = await Revisions(connection, "type = 'date'");
		after.Select(revision => (revision.Id, revision.LabelEn)).ShouldBe(before.Select(revision => (revision.Id, revision.LabelEn)), ignoreOrder: true);
		(await Count(connection, "SELECT count(*) FROM question_revisions WHERE type = 'date' AND allow_future_dates")).ShouldBe(0);
		(await Count(connection, "SELECT count(*) FROM question_revisions")).ShouldBe(everyRevisionBefore);
		(await Count(connection, "SELECT count(*) FROM question_revisions WHERE help_text_en = 'Tell us the date of the occurrence.' AND NOT allow_future_dates")).ShouldBe(1);
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenNonDateRevisionAllowsFutureDates_ThenCheckRefusesIt()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();

		// When
		await using var command = new NpgsqlCommand(
			"UPDATE question_revisions SET allow_future_dates = TRUE WHERE id = (SELECT id FROM question_revisions WHERE type <> 'date' LIMIT 1)",
			connection);
		var updating = () => command.ExecuteNonQueryAsync();

		// Then
		(await updating.ShouldThrowAsync<PostgresException>()).ConstraintName.ShouldBe("ck_question_revisions_future_dates_date");
	}

	private static async Task<List<Revision>> Revisions(NpgsqlConnection connection,
														string where)
	{
		await using var command = new NpgsqlCommand($"SELECT id, label_en FROM question_revisions WHERE {where}", connection);
		await using var reader = await command.ExecuteReaderAsync();
		var revisions = new List<Revision>();

		while (await reader.ReadAsync())
		{
			revisions.Add(new Revision(reader.GetString(0), reader.GetString(1)));
		}

		return revisions;
	}

	private static async Task<long> Count(NpgsqlConnection connection,
										  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		return (long)(await command.ExecuteScalarAsync())!;
	}

	private static async Task MigrateTo(HpacSafetyDbContext context,
										string? targetMigration)
	{
		var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
		await migrator.MigrateAsync(targetMigration);
	}

	private sealed record Revision(string Id, string LabelEn);
}
