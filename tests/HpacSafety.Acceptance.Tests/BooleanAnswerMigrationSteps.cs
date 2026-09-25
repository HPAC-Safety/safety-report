using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     <c>REQ-QB-137</c> and <c>REQ-QB-138</c>: the migration that stores a yes/no or
///     checkbox answer as a boolean converts every stored word once, and stops on
///     anything else (ADR-0130).
/// </summary>
/// <remarks>
///     A migration's effect is a fact about the database, so these run the real
///     migrations against a real PostgreSQL database. A database answered before the
///     change is reproduced by migrating to the migration before it and writing the
///     answer as ADR-0127 stored it. The seeded publication-consent revision is the
///     yes/no question; a checkbox is a synthetic revision cloned from a seeded
///     ordinary one. Every value is synthetic.
/// </remarks>
[Binding]
public sealed class BooleanAnswerMigrationSteps
{
	private const string PriorMigration = "StoreYesOrNoInTheReportersLanguage";
	private const string ReportId = "rbooleanmig";
	private const string AnswerId = "abooleanmig";
	private const string CheckboxRevisionId = "cbooleanmig";

	private string? _connectionString;
	private string _locale = "en-CA";
	private Exception? _migrationFailure;

	private string ConnectionString =>
		_connectionString ?? throw new InvalidOperationException("No database has been created for this scenario.");

	[Given(@"^a (yes_no|checkbox) answer was stored as the word ""(\w+)"" before yes\/no answers were booleans$")]
	public async Task GivenAWordAnswer(string type,
									   string word)
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(PriorMigration);

		// A French word was written by a French report, with the other language's
		// word as its fixed counterpart (ADR-0127).
		_locale = word is "oui" or "non" ? "fr-CA" : "en-CA";
		var counterpart = word switch
		{
			"yes" => "oui",
			"oui" => "yes",
			"no" => "non",
			"non" => "no",
			_ => null,
		};

		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES (@report, @locale, 'submitted', TIMESTAMPTZ '2026-09-24T12:00:00Z');
			""",
			("report", ReportId), ("locale", _locale));

		var revisionId = type == "checkbox" ? await CheckboxRevision() : await ConsentRevision();

		await Execute(
			"""
			INSERT INTO report_answers (id, report_id, question_id, question_revision_id, question_key, is_private,
			                            value, locale, translated_value, translation_source, translation_mode, answered_at)
			SELECT @answer, @report, revision.question_id, revision.id, question.key, revision.is_private,
			       @word, @locale, @counterpart::text, @source::text, 'fixed',
			       TIMESTAMPTZ '2026-09-24T12:00:00Z'
			FROM question_revisions AS revision
			         JOIN questions AS question ON question.id = revision.question_id
			WHERE revision.id = @revision;
			""",
			("answer", AnswerId), ("report", ReportId), ("word", word), ("locale", _locale),
			("counterpart", (object?)counterpart ?? DBNull.Value), ("source", counterpart is null ? DBNull.Value : "fixed"),
			("revision", revisionId));
	}

	[When(@"the database is migrated")]
	public async Task WhenTheDatabaseIsMigrated()
	{
		try
		{
			await MigrateTo(null);
		}
		catch (PostgresException failure)
		{
			_migrationFailure = failure;
		}
	}

	[Then(@"^that answer's boolean is (true|false)$")]
	public async Task ThenThatAnswersBooleanIs(bool expected)
	{
		_migrationFailure.ShouldBeNull();
		(await Answer()).Boolean.ShouldBe(expected);
	}

	[Then(@"the converted answer carries no words and no second language, and its translation mode is none")]
	public async Task ThenItCarriesNoWords()
	{
		var answer = await Answer();
		answer.Value.ShouldBeNull();
		answer.TranslatedValue.ShouldBeNull();
		answer.TranslationSource.ShouldBeNull();
		answer.TranslationMode.ShouldBe("none");
	}

	[Then(@"its locale is unchanged")]
	public async Task ThenItsLocaleIsUnchanged()
	{
		(await Answer()).Locale.ShouldBe(_locale);
	}

	[Then(@"the migration fails and names no answer's value")]
	public void ThenTheMigrationFails()
	{
		var failure = _migrationFailure.ShouldBeOfType<PostgresException>();
		failure.MessageText.ShouldContain("StoreYesOrNoAsABoolean");
		failure.MessageText.ShouldNotContain("maybe");
	}

	[Then(@"no answer was converted")]
	public async Task ThenNoAnswerWasConverted()
	{
		// The migration rolled back as a whole: the column it adds is not there,
		// and the answer still holds its word.
		(await Scalar<long>(
			"SELECT count(*) FROM information_schema.columns WHERE table_name = 'report_answers' AND column_name = 'value_boolean'"))
			.ShouldBe(0);
		(await Scalar<string>($"SELECT value FROM report_answers WHERE id = '{AnswerId}'")).ShouldBe("maybe");
	}

	private async Task<string> ConsentRevision()
	{
		return await Scalar<string>(
			"""
			SELECT revision.id
			FROM question_revisions AS revision
			         JOIN questions AS question ON question.id = revision.question_id
			WHERE question.role = 'consent_publish' AND revision.deleted IS NULL
			ORDER BY revision.revision_number DESC
			LIMIT 1
			""");
	}

	/// <summary>No seeded question is a checkbox, so one ordinary revision is cloned as one.</summary>
	private async Task<string> CheckboxRevision()
	{
		await Execute(
			"""
			CREATE TEMP TABLE checkbox_revision AS
			SELECT revision.*
			FROM question_revisions AS revision
			         JOIN questions AS question ON question.id = revision.question_id
			WHERE question.role = 'none' AND revision.type = 'short_text' AND revision.deleted IS NULL
			ORDER BY revision.id
			LIMIT 1;

			UPDATE checkbox_revision
			SET id = @revision, type = 'checkbox', revision_number = revision_number + 1000;

			INSERT INTO question_revisions SELECT * FROM checkbox_revision;
			""",
			("revision", CheckboxRevisionId));

		return CheckboxRevisionId;
	}

	private async Task<StoredAnswer> Answer()
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand(
			$"SELECT value_boolean, value, translated_value, translation_source, translation_mode, locale FROM report_answers WHERE id = '{AnswerId}'",
			connection);
		await using var reader = await command.ExecuteReaderAsync();
		(await reader.ReadAsync()).ShouldBeTrue();

		return new StoredAnswer(
			reader.IsDBNull(0) ? null : reader.GetBoolean(0),
			reader.IsDBNull(1) ? null : reader.GetString(1),
			reader.IsDBNull(2) ? null : reader.GetString(2),
			reader.IsDBNull(3) ? null : reader.GetString(3),
			reader.GetString(4),
			reader.GetString(5));
	}

	private async Task MigrateTo(string? targetMigration)
	{
		await using var context = WorkerDatabase.ContextFor(ConnectionString);
		await context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(targetMigration);
	}

	private async Task Execute(string sql,
							   params (string Name, object Value)[] parameters)
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand(sql, connection);

		foreach (var (name, value) in parameters)
		{
			command.Parameters.AddWithValue(name, value);
		}

		await command.ExecuteNonQueryAsync();
	}

	private async Task<T> Scalar<T>(string sql)
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand(sql, connection);
		return (T)(await command.ExecuteScalarAsync())!;
	}

	private sealed record StoredAnswer(
		bool? Boolean,
		string? Value,
		string? TranslatedValue,
		string? TranslationSource,
		string TranslationMode,
		string Locale);
}
