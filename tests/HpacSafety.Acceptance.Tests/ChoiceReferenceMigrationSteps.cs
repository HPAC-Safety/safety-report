using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Existing select answers are linked to their choices without being rewritten
///     (<c>REQ-QB-136</c>, ADR-0128).
/// </summary>
/// <remarks>
///     A migration's effect is a fact about the database, so this runs the real
///     migrations against a real PostgreSQL database: up to the one before the
///     choice reference, then a question and two answers stored the way answers
///     were stored then — as the label the reporter saw — then the rest. Every
///     question, choice, and answer here is synthetic.
/// </remarks>
[Binding]
public sealed class ChoiceReferenceMigrationSteps
{
	private const string PriorMigration = "StoreYesOrNoAsABoolean";

	private string? _connectionString;

	private string ConnectionString =>
		_connectionString ?? throw new InvalidOperationException("No database has been created for this scenario.");

	[Given(@"reports stored before this change answered a single-select question with one of its current labels and with a label it no longer offers")]
	public async Task GivenAnswersStoredAsLabels()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(PriorMigration);

		// "Mauve" was a choice once, relabelled or removed since: no row carries it.
		await Execute(
			"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qcolour0001', 'synthetic_colour', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, created_at)
			VALUES ('rcolour0001', 'qcolour0001', 1, 'single_select', 'Colour', 'Couleur', FALSE, FALSE, FALSE, TRUE, 90, TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr)
			VALUES ('cblue000001', 'qcolour0001', 'blue', 0, 'Blue', 'Bleu');
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES ('rptcolour01', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z'),
			       ('rptcolour02', 'fr-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO report_answers
				(id, report_id, question_id, question_revision_id, question_key, is_private, value, locale, translation_mode, answered_at)
			VALUES ('anscurrent1', 'rptcolour01', 'qcolour0001', 'rcolour0001', 'synthetic_colour', FALSE, 'Blue', 'en-CA', 'machine', TIMESTAMPTZ '2026-09-01T00:00:00Z'),
			       ('ansorphan01', 'rptcolour02', 'qcolour0001', 'rcolour0001', 'synthetic_colour', FALSE, 'Mauve', 'fr-CA', 'machine', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			""");
	}

	[When(@"the choice-reference migration runs")]
	public async Task WhenTheMigrationRuns()
	{
		await MigrateTo(null);
	}

	[Then(@"the first answer names the choice with that label")]
	public async Task ThenTheFirstAnswerNamesItsChoice()
	{
		(await Text("SELECT choice_id FROM report_answers WHERE id = 'anscurrent1'")).ShouldBe("cblue000001");
	}

	[Then(@"the second answer names a removed choice carrying the label it stored, in its language")]
	public async Task ThenTheSecondAnswerNamesARemovedChoice()
	{
		var choice = await Text(
			"""
			SELECT coalesce(c.label_en, '-') || '|' || coalesce(c.label_fr, '-') || '|' || (c.deleted IS NOT NULL) || '|' || c.question_id
			FROM report_answers a JOIN question_choices c ON c.id = a.choice_id
			WHERE a.id = 'ansorphan01'
			""");

		choice.ShouldBe("-|Mauve|true|qcolour0001");
	}

	[Then(@"neither answer's stored text changes")]
	public async Task ThenNeitherAnswerChanges()
	{
		(await Text("SELECT value || '|' || locale FROM report_answers WHERE id = 'anscurrent1'")).ShouldBe("Blue|en-CA");
		(await Text("SELECT value || '|' || locale FROM report_answers WHERE id = 'ansorphan01'")).ShouldBe("Mauve|fr-CA");
	}

	private async Task MigrateTo(string? targetMigration)
	{
		await using var context = WorkerDatabase.ContextFor(ConnectionString);
		await context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(targetMigration);
	}

	private async Task Execute(string sql)
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private async Task<string> Text(string sql)
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(sql, connection);
		return (string)(await command.ExecuteScalarAsync())!;
	}

	private async Task<NpgsqlConnection> Open()
	{
		var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		return connection;
	}
}
