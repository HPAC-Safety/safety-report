using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>ReplacePickerOptionsAndNameConditionsByChoice</c>: a condition's required
///     option code becomes the identifier of the parent's choice with that code, and
///     a code no choice carries stops the migration rather than drop the condition
///     (ADR-0128, #480). Every question and choice here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class ConditionByChoiceMigrationTests(PostgresFixture postgres)
{
	private const string PriorMigration = "ReviewTypeAheadValues";

	[Fact]
	public async Task GivenAConditionNamingAnOptionCode_WhenMigrationRuns_ThenItNamesThatChoiceById()
	{
		// Given
		var connectionString = await BeforeTheMigration("paraglider");

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, null);
		}

		// Then
		await using var connection = await Open(connectionString);
		(await Scalar(connection, "SELECT depends_on_choice_id FROM question_revisions WHERE id = 'rchild00001'"))
			.ShouldBe("cpara000001");
		(await Scalar(
				connection,
				"SELECT count(*)::text FROM information_schema.columns WHERE table_name = 'question_revisions' AND column_name = 'depends_on_option_code'"))
			.ShouldBe("0");
	}

	[Fact]
	public async Task GivenAConditionNamingACodeNoChoiceCarries_WhenMigrationRuns_ThenItStopsAndChangesNothing()
	{
		// Given
		var connectionString = await BeforeTheMigration("trike");

		// When
		var migrating = async () =>
		{
			await using var context = PostgresFixture.ContextFor(connectionString);
			await MigrateTo(context, null);
		};

		// Then — rolled back: the code is still there, and nothing names a choice
		(await migrating.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("1 question revision(s)");

		await using var connection = await Open(connectionString);
		(await Scalar(connection, "SELECT depends_on_option_code FROM question_revisions WHERE id = 'rchild00001'"))
			.ShouldBe("trike");
	}

	/// <summary>A database one migration short, with a pilot-type parent and a child conditional on <paramref name="code" />.</summary>
	private async Task<string> BeforeTheMigration(string code)
	{
		var connectionString = await postgres.CreateDatabase();
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, PriorMigration);
		}

		await using var connection = await Open(connectionString);
		await using var command = new NpgsqlCommand(
			"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qparent0001', 'synthetic_pilot_type', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z'),
			       ('qchild00001', 'synthetic_rating', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, depends_on_question_id, depends_on_option_code, created_at)
			VALUES ('rparent0001', 'qparent0001', 1, 'single_select', 'Pilot type', 'Type de pilote', FALSE, FALSE, FALSE, TRUE, 90, NULL, NULL, TIMESTAMPTZ '2026-09-01T00:00:00Z'),
			       ('rchild00001', 'qchild00001', 1, 'short_text', 'Rating', 'Qualification', FALSE, FALSE, FALSE, TRUE, 91, 'qparent0001', @code, TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr, label_en_source, label_fr_source)
			VALUES ('chang000001', 'qparent0001', 'hang_glider', 0, 'Hang glider', 'Deltaplane', 'human', 'human'),
			       ('cpara000001', 'qparent0001', 'paraglider', 1, 'Paraglider', 'Parapente', 'human', 'human');
			""",
			connection);
		command.Parameters.AddWithValue("code", code);
		await command.ExecuteNonQueryAsync();

		return connectionString;
	}

	private static async Task MigrateTo(HpacSafetyDbContext context,
										string? targetMigration)
	{
		await context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(targetMigration);
	}

	private static async Task<NpgsqlConnection> Open(string connectionString)
	{
		var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		return connection;
	}

	private static async Task<string?> Scalar(NpgsqlConnection connection,
											  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		return (string?)await command.ExecuteScalarAsync();
	}
}
