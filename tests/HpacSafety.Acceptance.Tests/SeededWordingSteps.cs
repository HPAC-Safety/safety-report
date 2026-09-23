using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The seeded attachment question's wording, on a new database and on one
///     seeded before the wording changed (<c>REQ-QB-104</c> to <c>REQ-QB-107</c>).
/// </summary>
/// <remarks>
///     A migration's effect is a fact about the database, so these run the real
///     migrations against a real PostgreSQL database. A database seeded before the
///     change is reproduced by migrating to the migration before the rewording and
///     putting the original seeded wording back on the question. The report that
///     answers it is synthetic.
/// </remarks>
[Binding]
public sealed class SeededWordingSteps
{
	private const string PriorMigration = "AddReportFileOriginalFileName";
	private const string AttachmentKey = "418e72ec_1edc_4e0d_9429_af65ca564ab1";

	private const string OldLabelEn = "Photo or video:";
	private const string OldLabelFr = "Photo ou vidéo:";
	private const string OldHelpEn = "Upload one photo or video of the occurrence. Please contact us directly for multiple files (safety@hpac.ca).";
	private const string OldHelpFr = "Téléchargez une photo ou vidéo de l'événement. Contactez-nous directement pour plusieurs fichiers (safety@hpac.ca).";

	private const string NewLabelEn = "Photos or videos:";
	private const string NewLabelFr = "Photos ou vidéos:";
	private const string NewHelpEn = "Upload photos, videos, or documents of the occurrence, if you have any.";
	private const string NewHelpFr = "Téléversez des photos, vidéos ou documents de l'événement, si vous en avez.";

	private string? _connectionString;
	private string? _originalQuestionId;
	private int _originalRevisionCount;
	private string[] _revisionsBefore = [];

	private string ConnectionString =>
		_connectionString ?? throw new InvalidOperationException("No database has been created for this scenario.");

	[Given(@"a new, empty database")]
	public async Task GivenANewEmptyDatabase()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
	}

	[When(@"the migrations run")]
	[When(@"the attachment rewording migration runs")]
	public async Task WhenTheMigrationsRun()
	{
		await MigrateTo(null);
	}

	[Then(@"the seeded attachment question is labelled ""(.*)"" and ""(.*)""")]
	public async Task ThenTheAttachmentQuestionIsLabelled(string labelEn,
														  string labelFr)
	{
		var live = await LiveRevision();
		live.LabelEn.ShouldBe(labelEn);
		live.LabelFr.ShouldBe(labelFr);
	}

	[Then(@"its help text asks for photos, videos, or documents in both languages")]
	public async Task ThenItsHelpTextAsksForSeveralFiles()
	{
		var live = await LiveRevision();
		live.HelpEn.ShouldBe(NewHelpEn);
		live.HelpFr.ShouldBe(NewHelpFr);
	}

	[Given(@"a database whose attachment question still carries its original single-file wording")]
	public async Task GivenTheOriginalSingleFileWording()
	{
		await SeededBeforeTheRewording();
		await Execute(
			"""
			UPDATE question_revisions
			SET label_en = @labelEn, label_fr = @labelFr, help_text_en = @helpEn, help_text_fr = @helpFr
			WHERE question_id = @questionId
			""",
			("labelEn", OldLabelEn), ("labelFr", OldLabelFr), ("helpEn", OldHelpEn), ("helpFr", OldHelpFr),
			("questionId", _originalQuestionId!));
	}

	[Given(@"no answer references the attachment question")]
	public async Task GivenNoAnswerReferencesIt()
	{
		(await Count("SELECT count(*) FROM report_answers WHERE question_id = @id", ("id", _originalQuestionId!)))
			.ShouldBe(0);
	}

	[Given(@"a report has answered the attachment question")]
	public async Task GivenAReportHasAnsweredIt()
	{
		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES ('rptattach01', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO report_answers (id, report_id, question_id, question_revision_id, question_key, is_private, value, locale, answered_at)
			SELECT 'ansattach01', 'rptattach01', q.id, r.id, q.key, FALSE, 'synthetic.jpg', 'en-CA', TIMESTAMPTZ '2026-09-01T00:00:00Z'
			FROM questions q JOIN question_revisions r ON r.question_id = q.id
			WHERE q.id = @questionId
			ORDER BY r.revision_number DESC
			LIMIT 1
			""",
			("questionId", _originalQuestionId!));
	}

	[Given(@"a database whose attachment question an Administrator has already reworded")]
	public async Task GivenAnAdministratorAlreadyReworded()
	{
		await SeededBeforeTheRewording();
		await Execute(
			"""
			UPDATE question_revisions
			SET label_en = @labelEn, label_fr = @labelFr, help_text_en = @helpEn, help_text_fr = @helpFr
			WHERE question_id = @questionId;
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
				 is_system, is_required, is_private, is_active, display_order, created_at)
			SELECT 'revadmin001', question_id, revision_number + 1, type, 'Pictures:', 'Images :',
			       'Add any pictures you took.', 'Ajoutez les images que vous avez prises.',
			       is_system, is_required, is_private, is_active, display_order, TIMESTAMPTZ '2026-09-10T00:00:00Z'
			FROM question_revisions WHERE question_id = @questionId
			""",
			("labelEn", OldLabelEn), ("labelFr", OldLabelFr), ("helpEn", OldHelpEn), ("helpFr", OldHelpFr),
			("questionId", _originalQuestionId!));
		_revisionsBefore = await Revisions();
	}

	[Then(@"the attachment question has a new revision with the several-files wording")]
	public async Task ThenANewRevisionCarriesTheNewWording()
	{
		var live = await LiveRevision();
		live.RevisionNumber.ShouldBe(_originalRevisionCount + 1);
		(live.LabelEn, live.LabelFr, live.HelpEn, live.HelpFr).ShouldBe((NewLabelEn, NewLabelFr, NewHelpEn, NewHelpFr));
	}

	[Then(@"the attachment question keeps its identifier")]
	public async Task ThenItKeepsItsIdentifier()
	{
		(await LiveRevision()).QuestionId.ShouldBe(_originalQuestionId);
		(await Count("SELECT count(*) FROM questions WHERE key = @key", ("key", AttachmentKey))).ShouldBe(1);
	}

	[Then(@"the original attachment question is stamped as deleted and keeps its single-file wording")]
	public async Task ThenTheOriginalIsRetiredWithItsWording()
	{
		(await Count("SELECT count(*) FROM questions WHERE id = @id AND deleted IS NOT NULL", ("id", _originalQuestionId!)))
			.ShouldBe(1);
		(await Count(
				"SELECT count(*) FROM question_revisions WHERE question_id = @id AND label_en = @label AND help_text_en = @help",
				("id", _originalQuestionId!), ("label", OldLabelEn), ("help", OldHelpEn)))
			.ShouldBe(_originalRevisionCount);
		(await Count("SELECT count(*) FROM report_answers WHERE question_id = @id", ("id", _originalQuestionId!)))
			.ShouldBe(1);
	}

	[Then(@"a new live question with the same key carries the several-files wording")]
	public async Task ThenAReplacementCarriesTheNewWording()
	{
		var live = await LiveRevision();
		live.QuestionId.ShouldNotBe(_originalQuestionId);
		live.RevisionNumber.ShouldBe(1);
		(live.LabelEn, live.LabelFr, live.HelpEn, live.HelpFr).ShouldBe((NewLabelEn, NewLabelFr, NewHelpEn, NewHelpFr));
	}

	[Then(@"the attachment question and its revisions are unchanged")]
	public async Task ThenNothingChanged()
	{
		(await LiveRevision()).QuestionId.ShouldBe(_originalQuestionId);
		(await Revisions()).ShouldBe(_revisionsBefore);
	}

	private async Task SeededBeforeTheRewording()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(PriorMigration);

		_originalQuestionId = await Text("SELECT id FROM questions WHERE key = @key AND deleted IS NULL", ("key", AttachmentKey));
		_originalRevisionCount = (int)await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", _originalQuestionId));
	}

	private async Task MigrateTo(string? targetMigration)
	{
		await using var context = WorkerDatabase.ContextFor(ConnectionString);
		await context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(targetMigration);
	}

	private async Task<LiveAttachmentRevision> LiveRevision()
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(
			"""
			SELECT q.id, r.revision_number, r.label_en, r.label_fr, r.help_text_en, r.help_text_fr
			FROM questions q
			JOIN question_revisions r ON r.question_id = q.id
			WHERE q.key = @key AND q.deleted IS NULL
			ORDER BY r.revision_number DESC
			LIMIT 1
			""",
			connection);
		command.Parameters.AddWithValue("key", AttachmentKey);
		await using var reader = await command.ExecuteReaderAsync();
		(await reader.ReadAsync()).ShouldBeTrue("No live attachment question.");

		return new LiveAttachmentRevision(
			reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
			reader.GetString(4), reader.GetString(5));
	}

	private async Task<string[]> Revisions()
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(
			"""
			SELECT r.id || '|' || r.revision_number || '|' || r.label_en || '|' || coalesce(r.help_text_en, '')
			FROM question_revisions r JOIN questions q ON q.id = r.question_id
			WHERE q.key = @key
			ORDER BY r.question_id, r.revision_number
			""",
			connection);
		command.Parameters.AddWithValue("key", AttachmentKey);
		await using var reader = await command.ExecuteReaderAsync();

		var rows = new List<string>();
		while (await reader.ReadAsync())
		{
			rows.Add(reader.GetString(0));
		}

		return [.. rows];
	}

	private async Task Execute(string sql,
							   params (string Name, string Value)[] parameters)
	{
		await using var connection = await Open();
		await using var command = Command(connection, sql, parameters);
		await command.ExecuteNonQueryAsync();
	}

	private async Task<long> Count(string sql,
								   params (string Name, string Value)[] parameters)
	{
		await using var connection = await Open();
		await using var command = Command(connection, sql, parameters);
		return (long)(await command.ExecuteScalarAsync())!;
	}

	private async Task<string> Text(string sql,
									params (string Name, string Value)[] parameters)
	{
		await using var connection = await Open();
		await using var command = Command(connection, sql, parameters);
		return (string)(await command.ExecuteScalarAsync())!;
	}

	private static NpgsqlCommand Command(NpgsqlConnection connection,
										 string sql,
										 (string Name, string Value)[] parameters)
	{
		var command = new NpgsqlCommand(sql, connection);
		foreach (var (name, value) in parameters)
		{
			command.Parameters.AddWithValue(name, value);
		}

		return command;
	}

	private async Task<NpgsqlConnection> Open()
	{
		var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		return connection;
	}

	private sealed record LiveAttachmentRevision(
		string QuestionId,
		int RevisionNumber,
		string LabelEn,
		string LabelFr,
		string HelpEn,
		string HelpFr);
}
