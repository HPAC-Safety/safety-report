using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HpacSafety.Core.Features.Moderation;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The label-colon scenarios (<c>REQ-QB-244</c> to <c>REQ-QB-246</c>, ADR-0181): the API
///     refuses a label that ends in a colon, the migration that trims stored colons
///     changes labels in place and nothing else, and a clean database holds none.
/// </summary>
/// <remarks>
///     The migration scenario reproduces a database that still has colons by migrating
///     to the migration before it and writing colons onto seeded labels. Every value is
///     synthetic.
/// </remarks>
[Binding]
public sealed partial class QuestionLabelColonSteps
{
	private const string PriorMigration = "RefuseChangesToTheReportersAccount";
	private const string ReportId = "rcolonmig1";
	private const string AnswerId = "acolonmig1";

	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private HttpClient? _client;
	private string? _createdQuestionId;
	private HttpResponseMessage? _refusal;
	private JsonElement _refusalBody;
	private string _marker = "";

	private string? _connectionString;
	private Dictionary<string, (string En, string Fr)> _labelsBefore = [];
	private long _revisionsBefore;
	private long _questionsBeforeMigration;
	private string _answeredRevision = "";

	private string ConnectionString =>
		_connectionString ?? throw new InvalidOperationException("No database has been created for this scenario.");

	// --- REQ-QB-244: the API refuses a label that ends in a colon ---

	[Given(@"a signed-in Administrator")]
	public async Task GivenASignedInAdministrator()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);
	}

	[When(@"^they (create|revise) a question whose (English|French) label is ""([^""]+)""$")]
	public async Task WhenTheyCreateOrReviseWithALabel(string action,
													   string language,
													   string label)
	{
		// The other language's label carries a marker no other scenario uses, because
		// the booted host is shared: what was stored is found by the marker, never by
		// counting every question.
		_marker = Guid.NewGuid().ToString("N")[..8];
		var marker = _marker;
		var labelEn = language == "English" ? label : $"Landing {marker}";
		var labelFr = language == "French" ? label : $"Atterrissage {marker}";

		if (action == "create")
		{
			_refusal = await _client!.PostAsJsonAsync(Questions, Body(labelEn, labelFr));
		}
		else
		{
			// The question to revise exists with a good label; the revision is what is refused.
			using var created = await _client!.PostAsJsonAsync(Questions, Body($"Landing {marker}", $"Atterrissage {marker}"));
			created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
			_createdQuestionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

			_refusal = await _client!.PutAsJsonAsync(new Uri($"{Questions}/{_createdQuestionId}", UriKind.Relative), Body(labelEn, labelFr));
		}

		_refusalBody = await _refusal.Content.ReadFromJsonAsync<JsonElement>();
	}

	[Then(@"the API refuses it with a problem that says, in English and French, that the form adds the colon itself")]
	public void ThenTheApiRefusesWithABilingualProblem()
	{
		_refusal!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		_refusalBody.GetProperty("type").GetString().ShouldEndWith("/label-colon");
		var detail = _refusalBody.GetProperty("detail").GetString()!;
		detail.ShouldContain("the form adds it itself");
		detail.ShouldContain("le formulaire l'ajoute lui-même");
	}

	[Then(@"no question or revision is stored")]
	public async Task ThenNothingIsStored()
	{
		using var response = await _client!.GetAsync(Questions);
		var marked = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
			.Where(question => question.GetProperty("labelEn").GetString()!.Contains(_marker, StringComparison.Ordinal)
							   || question.GetProperty("labelFr").GetString()!.Contains(_marker, StringComparison.Ordinal))
			.ToList();

		if (_createdQuestionId is null)
		{
			marked.ShouldBeEmpty();
			return;
		}

		// The question that was being revised still has its one good wording.
		var stored = marked.ShouldHaveSingleItem();
		stored.GetProperty("id").GetString().ShouldBe(_createdQuestionId);
		stored.GetProperty("labelEn").GetString().ShouldBe($"Landing {_marker}");
		stored.GetProperty("labelFr").GetString().ShouldBe($"Atterrissage {_marker}");
	}

	// --- REQ-QB-245: the migration trims colons in place ---

	[Given(@"a database holding questions whose labels end in "":"" or "" :"" in English and in French")]
	public async Task GivenADatabaseWithColonLabels()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(PriorMigration);

		await Execute(
			"""
			UPDATE question_revisions SET label_en = label_en || ':', label_fr = label_fr || ' :' WHERE label_en IN ('Date', 'Description');
			UPDATE question_revisions SET label_en = label_en || ' :', label_fr = label_fr || E' :' WHERE label_en = 'Province';
			UPDATE question_revisions SET label_en = label_en || E' :', label_fr = label_fr || ':' WHERE label_en = 'Damage';
			""");

		_labelsBefore = await Labels();
		_labelsBefore.Values.ShouldContain(labels => labels.En.EndsWith(':') || labels.Fr.EndsWith(':'));
		_labelsBefore.Values.ShouldContain(labels => !labels.En.EndsWith(':') && !labels.Fr.EndsWith(':'));
		_revisionsBefore = await Scalar<long>("SELECT count(*) FROM question_revisions");
		_questionsBeforeMigration = await Scalar<long>("SELECT count(*) FROM questions");
	}

	[Given(@"a report answered under one of them")]
	public async Task GivenAReportAnsweredUnderOne()
	{
		_answeredRevision = await Scalar<string>("SELECT id FROM question_revisions WHERE label_en = 'Description:' LIMIT 1");

		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES (@report, 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-30T12:00:00Z');
			""",
			("report", ReportId));
		await Execute(
			"""
			INSERT INTO report_answers (id, report_id, question_id, question_revision_id, question_key, is_private,
			                            value, locale, translation_mode, answered_at)
			SELECT @answer, @report, revision.question_id, revision.id, question.key, revision.is_private,
			       'A synthetic hard landing.', 'en-CA', 'none', TIMESTAMPTZ '2026-09-30T12:00:00Z'
			FROM question_revisions AS revision
			         JOIN questions AS question ON question.id = revision.question_id
			WHERE revision.id = @revision;
			""",
			("answer", AnswerId), ("report", ReportId), ("revision", _answeredRevision));
	}

	[When(@"the migration that trims label colons runs")]
	public async Task WhenTheMigrationRuns()
	{
		// Stops at the migration under test: a later one adds questions and revisions (#750).
		await MigrateTo("TrimLabelColons");
	}

	[Then(@"every label has no trailing colon, and a label that had none is unchanged")]
	public async Task ThenEveryLabelIsTrimmed()
	{
		var after = await Labels();
		after.Keys.ShouldBe(_labelsBefore.Keys, ignoreOrder: true);

		foreach (var (id, before) in _labelsBefore)
		{
			after[id].En.ShouldBe(Trimmed(before.En));
			after[id].Fr.ShouldBe(Trimmed(before.Fr));
			after[id].En.ShouldNotEndWith(":");
			after[id].Fr.ShouldNotEndWith(":");
		}
	}

	[Then(@"no question or revision was created or deleted")]
	public async Task ThenNothingWasCreatedOrDeleted()
	{
		(await Scalar<long>("SELECT count(*) FROM question_revisions")).ShouldBe(_revisionsBefore);
		(await Scalar<long>("SELECT count(*) FROM questions")).ShouldBe(_questionsBeforeMigration);
	}

	[Then(@"the answer still names the same revision")]
	public async Task ThenTheAnswerStillNamesTheSameRevision()
	{
		(await Scalar<string>($"SELECT question_revision_id FROM report_answers WHERE id = '{AnswerId}'")).ShouldBe(_answeredRevision);
		(await Scalar<string>($"SELECT label_en FROM question_revisions WHERE id = '{_answeredRevision}'")).ShouldBe("Description");
	}

	// --- REQ-QB-246: the seed has no colons ---

	[Given(@"a clean database")]
	public async Task GivenACleanDatabase()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
	}

	[When(@"the migrations have run")]
	public async Task WhenTheMigrationsHaveRun()
	{
		await MigrateTo(null);
	}

	[Then(@"no question revision's English or French label ends in a colon")]
	public async Task ThenNoLabelEndsInAColon()
	{
		(await Scalar<long>("SELECT count(*) FROM question_revisions")).ShouldBeGreaterThan(0);
		(await Scalar<long>(
			"SELECT count(*) FROM question_revisions WHERE label_en ~ ':[[:space:]]*$' OR label_fr ~ ':[[:space:]]*$'"))
			.ShouldBe(0);
	}

	// --- helpers ---

	private static object Body(string labelEn,
							   string labelFr)
	{
		return new
		{
			type = "short_text",
			labelEn,
			labelFr,
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = Array.Empty<object>(),
		};
	}

	private static string Trimmed(string label)
	{
		return TrailingColon().Replace(label, "");
	}

	[GeneratedRegex("[\\s  ]*:[\\s  ]*$")]
	private static partial Regex TrailingColon();

	private async Task<Dictionary<string, (string En, string Fr)>> Labels()
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand("SELECT id, label_en, label_fr FROM question_revisions", connection);
		await using var reader = await command.ExecuteReaderAsync();
		var labels = new Dictionary<string, (string, string)>();

		while (await reader.ReadAsync())
		{
			labels[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2));
		}

		return labels;
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
}
