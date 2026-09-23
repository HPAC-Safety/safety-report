using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>GiveEachQuestionItsOwnChoices</c> copies every question's current choices
///     onto the question before dropping the tables they came from (ADR-0095).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class ChoiceMigrationTests(PostgresFixture postgres)
{
	private const string PriorMigration = "RecordReporterChoiceLocale";

	[Fact]
	public async Task GivenRevisionOptionsAndASharedList_WhenMigrationRuns_ThenEveryChoiceLandsOnItsQuestion()
	{
		// Given
		var connectionString = await postgres.CreateDatabase();
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, PriorMigration);
		}

		long seededOptions;
		await using (var connection = new NpgsqlConnection(connectionString))
		{
			await connection.OpenAsync();
			await Execute(
				connection,
				"""
				INSERT INTO questions (id, key, is_system, role, created_at, deleted)
				VALUES ('qselect0001', 'synthetic_select', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z', NULL),
				       ('qtypeahead1', 'synthetic_site', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z', NULL);
				INSERT INTO option_sets (id, key, name_en, name_fr, created_at, deleted)
				VALUES ('setsites001', 'synthetic_sites', 'Sites', 'Sites', TIMESTAMPTZ '2026-09-01T00:00:00Z', NULL);
				INSERT INTO question_revisions (id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, option_set_id, allows_reporter_additions, created_at)
				VALUES ('rselect0001', 'qselect0001', 1, 'single_select', 'Pick', 'Choisir', FALSE, FALSE, TRUE, FALSE, 90, NULL, FALSE, TIMESTAMPTZ '2026-09-01T00:00:00Z'),
				       ('rselect0002', 'qselect0001', 2, 'single_select', 'Pick one', 'Choisir un', FALSE, FALSE, TRUE, TRUE, 90, NULL, FALSE, TIMESTAMPTZ '2026-09-02T00:00:00Z'),
				       ('rtypeahead1', 'qtypeahead1', 1, 'autocomplete', 'Site', 'Site', FALSE, FALSE, TRUE, TRUE, 91, 'setsites001', TRUE, TIMESTAMPTZ '2026-09-01T00:00:00Z');
				INSERT INTO question_revision_options (id, question_revision_id, code, label_en, label_fr, display_order, source_item_id, deleted)
				VALUES ('oold0000001', 'rselect0001', 'stale', 'Stale', 'Périmé', 0, NULL, NULL),
				       ('onew0000001', 'rselect0002', 'alpha', 'Alpha', 'Alpha', 0, NULL, NULL),
				       ('onew0000002', 'rselect0002', 'beta', 'Beta', 'Bêta', 1, NULL, NULL),
				       ('osnap000001', 'rtypeahead1', 'coopers', 'Cooper''s', 'Cooper''s', 0, NULL, NULL);
				INSERT INTO option_set_items (id, option_set_id, code, display_order, label_en, label_fr, added_by_reporter, needs_translation, reporter_locale, deleted)
				VALUES ('item0000001', 'setsites001', 'coopers', 0, 'Cooper''s', 'Cooper''s', FALSE, FALSE, NULL, NULL),
				       ('item0000002', 'setsites001', 'elevation', 1, 'Élévation', 'Élévation', TRUE, TRUE, 'fr-CA', NULL),
				       ('item0000003', 'setsites001', 'junk', 2, 'Junk', 'Junk', TRUE, TRUE, 'en-CA', TIMESTAMPTZ '2026-09-03T00:00:00Z');
				""");

			seededOptions = await Scalar(
				connection,
				"""
				SELECT count(*) FROM question_revision_options o
				JOIN question_revisions r ON r.id = o.question_revision_id
				WHERE r.revision_number = (SELECT max(revision_number) FROM question_revisions x WHERE x.question_id = r.question_id)
				  AND r.question_id NOT IN ('qselect0001', 'qtypeahead1')
				""");
		}

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, null);
		}

		// Then
		await using var reader = new NpgsqlConnection(connectionString);
		await reader.OpenAsync();

		(await Rows(reader, "qselect0001")).ShouldBe(
		[
			"alpha|Alpha|Alpha|False||",
			"beta|Beta|Bêta|False||",
		]);

		(await Rows(reader, "qtypeahead1")).ShouldBe(
		[
			"coopers|Cooper's|Cooper's|False||",
			"elevation||Élévation|True|fr-CA|",
			"junk|Junk||True|en-CA|removed",
		]);

		(await Scalar(reader, "SELECT count(*) FROM question_choices WHERE question_id NOT IN ('qselect0001', 'qtypeahead1')"))
			.ShouldBe(seededOptions);

		(await Scalar(reader, "SELECT count(*) FROM information_schema.tables WHERE table_name IN ('option_sets', 'option_set_items', 'question_revision_options')"))
			.ShouldBe(0);
	}

	private static async Task<string[]> Rows(NpgsqlConnection connection,
											 string questionId)
	{
		await using var command = new NpgsqlCommand(
			"""
			SELECT code, coalesce(label_en, ''), coalesce(label_fr, ''), added_by_reporter, coalesce(reporter_locale, ''),
			       CASE WHEN deleted IS NULL THEN '' ELSE 'removed' END
			FROM question_choices WHERE question_id = @id ORDER BY display_order
			""",
			connection);
		command.Parameters.AddWithValue("id", questionId);
		await using var reader = await command.ExecuteReaderAsync();

		var rows = new List<string>();
		while (await reader.ReadAsync())
		{
			rows.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetBoolean(3)}|{reader.GetString(4)}|{reader.GetString(5)}");
		}

		return [.. rows];
	}

	private static async Task MigrateTo(HpacSafetyDbContext context,
										string? targetMigration)
	{
		var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
		await migrator.MigrateAsync(targetMigration);
	}

	private static async Task Execute(NpgsqlConnection connection,
									  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<long> Scalar(NpgsqlConnection connection,
										   string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		return (long)(await command.ExecuteScalarAsync())!;
	}
}
