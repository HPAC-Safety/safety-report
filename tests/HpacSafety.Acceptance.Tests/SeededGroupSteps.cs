using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.AspNetCore.Hosting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The seeded groups on a database created from scratch, and the migration that
///     restores them (<c>REQ-QB-259</c> to <c>REQ-QB-265</c>, ADR-0187).
/// </summary>
/// <remarks>
///     A database created from scratch before the repair is reproduced by migrating
///     an empty database to the migration before it, which loses the seeded group
///     links exactly as staging did. Every report and question added here is
///     synthetic.
/// </remarks>
public sealed partial class SeededWordingSteps
{
	private const string GroupPriorMigration = "MakeCountryAPinnedPickList";
	private const string GroupMigration = "RestoreSeededGroups";
	private const string GroupScript = "20261002200257_RestoreSeededGroups.sql";

	/// <summary>
	///     Set to <c>1</c> to rewrite the browser suite's fixture from what a freshly
	///     migrated database sends, instead of comparing against it (REQ-QB-265).
	/// </summary>
	private const string WriteFixtureVariable = "HPAC_WRITE_SEEDED_FORM_FIXTURE";

	/// <summary>Each seeded group, by English label, and the keys QuestionBankSeed groups under it, in order.</summary>
	private static readonly Dictionary<string, (string Key, string[] ChildKeys)> SeededGroups = new()
	{
		["From"] = ("01hd6vk528fxpra3wcnsadr5qq",
		[
			"da89ae06_f229_4f38_8faa_e9c5bafef2f3", "3d662189_41cb_4430_9db8_7b2e4861df53",
			"65ed41c8_5fbd_4fb0_9ec3_5dabdb42d7be", "d4cded5e_1393_47e1_b703_d44e2865ccd8",
		]),
		["Pilot"] = ("f9a22e64_b121_4dd0_b28a_6d30b9d1f7da",
		[
			"52afac6c_b30c_4bd2_a052_212fa9249a45", "41c4d104_82c5_4f31_9d86_cb95e35622e4",
		]),
		["Aircraft"] = ("b86820e5_114e_4266_b971_03b959e74167",
		[
			"7590a371_c9cd_4e19_9954_be5137a669d2", "7018ad9b_bc9c_4f06_9a36_5fb3eed0368c",
			"762e8ce8_ccd3_4d1f_be09_68eb33136aa5", "6879f513_fe00_4bdc_acda_138947650b3c",
		]),
	};

	private const string ReporterFirstNameKey = "da89ae06_f229_4f38_8faa_e9c5bafef2f3";

	private readonly Dictionary<string, (string Id, long Revisions)> _groupChildrenBefore = new();
	private string? _groupReadFromApi;
	private string? _ownGroupId;

	// ---------------------------------------------------------- the database --

	[Given(@"a database created from scratch before the seeded-group repair")]
	public async Task GivenADatabaseBeforeTheGroupRepair()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(GroupPriorMigration);

		foreach (var key in SeededGroups.Values.SelectMany(group => group.ChildKeys))
		{
			var id = await Text("SELECT id FROM questions WHERE key = @key AND deleted IS NULL", ("key", key));
			var revisions = await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", id));
			(await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id AND grouped_under_question_id IS NOT NULL", ("id", id)))
				.ShouldBe(0, $"Question {key} should have lost its group before the repair, as staging did.");
			_groupChildrenBefore[key] = (id, revisions);
		}
	}

	[Given(@"no answer references the ""(.*)"" group's questions")]
	public async Task GivenNoAnswerReferencesTheGroup(string group)
	{
		foreach (var key in SeededGroups[group].ChildKeys)
		{
			(await Count("SELECT count(*) FROM report_answers WHERE question_key = @key", ("key", key))).ShouldBe(0);
		}
	}

	[Given(@"a report has answered the reporter's ""First name""")]
	public async Task GivenAReportAnsweredTheReportersFirstName()
	{
		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES ('rptgroup001', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO report_answers
				(id, report_id, question_id, question_revision_id, question_key, is_private, value, locale, translation_mode, answered_at)
			SELECT 'ansgroup001', 'rptgroup001', q.id, r.id, q.key, TRUE, 'Synthetic', 'en-CA', 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z'
			FROM questions q JOIN question_revisions r ON r.question_id = q.id
			WHERE q.id = @questionId
			ORDER BY r.revision_number DESC
			LIMIT 1
			""",
			("questionId", _groupChildrenBefore[ReporterFirstNameKey].Id));
	}

	[Given(@"an Administrator has grouped the reporter's ""First name"" under a group of their own")]
	public async Task GivenAnAdministratorGroupedTheReportersFirstName()
	{
		_ownGroupId = "qowngroup01";
		await Execute(
			"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qowngroup01', 'synthetic_own_group', FALSE, 'none', TIMESTAMPTZ '2026-09-10T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private,
				 is_active, display_order, created_at)
			VALUES ('rowngroup01', 'qowngroup01', 1, 'group', 'Contact', 'Contact',
			        FALSE, FALSE, FALSE, TRUE, 99, TIMESTAMPTZ '2026-09-10T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
				 is_system, is_required, is_private, is_active, display_order, grouped_under_question_id, created_at)
			SELECT 'rownfirst01', question_id, revision_number + 1, type, label_en, label_fr, help_text_en, help_text_fr,
			       is_system, is_required, is_private, is_active, display_order, 'qowngroup01', TIMESTAMPTZ '2026-09-10T00:00:00Z'
			FROM question_revisions WHERE question_id = @questionId
			ORDER BY revision_number DESC LIMIT 1
			""",
			("questionId", _groupChildrenBefore[ReporterFirstNameKey].Id));
		_groupChildrenBefore[ReporterFirstNameKey] = _groupChildrenBefore[ReporterFirstNameKey] with
		{
			Revisions = _groupChildrenBefore[ReporterFirstNameKey].Revisions + 1,
		};
	}

	[Given(@"the ""(.*)"" group has been deleted")]
	public async Task GivenTheGroupHasBeenDeleted(string group)
	{
		await Execute(
			"UPDATE questions SET deleted = TIMESTAMPTZ '2026-09-10T00:00:00Z' WHERE key = @key AND deleted IS NULL",
			("key", SeededGroups[group].Key));
	}

	[Given(@"the seeded-group repair migration has run")]
	public async Task GivenTheGroupRepairHasRun()
	{
		await MigrateTo(GroupMigration);
		_snapshotBefore = await Snapshot();
	}

	[When(@"the seeded-group repair migration runs")]
	public async Task WhenTheGroupRepairRuns()
	{
		await MigrateTo(GroupMigration);
	}

	[When(@"the seeded-group repair script is run again")]
	public async Task WhenTheGroupRepairScriptIsRunAgain()
	{
		await Execute(SqlScript.Read(GroupScript));
	}

	[When(@"the report form's questions are read from a freshly migrated database")]
	public async Task WhenTheFormIsReadFromAFreshDatabase()
	{
		// Its own database, not the shared host's, which other scenarios add to.
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(null);
		var host = (await BootedApi.Factory()).WithWebHostBuilder(builder =>
			builder.UseSetting("ConnectionStrings:HpacSafety", ConnectionString));
		using var reader = host.CreateClient();
		_publicForm = await reader.GetFromJsonAsync<JsonElement>(PublicQuestionsUri);
	}

	// ------------------------------------------------- the repair (QB-260..264) --

	[Then(@"each of the ""(.*)"" group's questions has a new revision grouped under ""(.*)"" and keeps its identifier")]
	public async Task ThenEachQuestionIsRevisedUnderItsGroup(string group,
															 string groupedUnder)
	{
		var groupId = await LiveQuestionId(SeededGroups[groupedUnder].Key);

		foreach (var key in SeededGroups[group].ChildKeys)
		{
			var (id, revisionsBefore) = _groupChildrenBefore[key];
			(await LiveQuestionId(key)).ShouldBe(id);
			(await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", id))).ShouldBe(revisionsBefore + 1);
			(await CurrentGroupOf(id)).ShouldBe(groupId);
		}
	}

	[Then(@"the original ""First name"" question is marked deleted and keeps its answer")]
	public async Task ThenTheOriginalFirstNameIsRetired()
	{
		var original = _groupChildrenBefore[ReporterFirstNameKey].Id;
		(await Count("SELECT count(*) FROM questions WHERE id = @id AND deleted IS NOT NULL", ("id", original))).ShouldBe(1);
		(await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", original)))
			.ShouldBe(_groupChildrenBefore[ReporterFirstNameKey].Revisions);
		(await CurrentGroupOf(original)).ShouldBeNull();
		(await Count("SELECT count(*) FROM report_answers WHERE question_id = @id", ("id", original))).ShouldBe(1);
	}

	[Then(@"a new live question with the same key is grouped under ""(.*)""")]
	public async Task ThenAReplacementIsGrouped(string group)
	{
		var replacement = await LiveQuestionId(ReporterFirstNameKey);
		replacement.ShouldNotBe(_groupChildrenBefore[ReporterFirstNameKey].Id);
		(await Text("SELECT role FROM questions WHERE id = @id", ("id", replacement))).ShouldBe("reporter_first_name");
		(await CurrentGroupOf(replacement)).ShouldBe(await LiveQuestionId(SeededGroups[group].Key));
	}

	[Then(@"the one answer still names the original ""First name"" question, unchanged")]
	public async Task ThenTheAnswerIsUnchanged()
	{
		(await Count("SELECT count(*) FROM report_answers")).ShouldBe(1);
		(await Count(
				"SELECT count(*) FROM report_answers WHERE id = 'ansgroup001' AND question_id = @id AND value = 'Synthetic'",
				("id", _groupChildrenBefore[ReporterFirstNameKey].Id)))
			.ShouldBe(1);
	}

	[Then(@"the reporter's ""First name"" is still grouped under that group, with no new revision")]
	public async Task ThenTheAdministratorsGroupingStands()
	{
		var (id, revisions) = _groupChildrenBefore[ReporterFirstNameKey];
		(await LiveQuestionId(ReporterFirstNameKey)).ShouldBe(id);
		(await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", id))).ShouldBe(revisions);
		(await CurrentGroupOf(id)).ShouldBe(_ownGroupId);
	}

	[Then(@"the ""(.*)"" group's questions are still ungrouped, with no new revision")]
	public async Task ThenTheQuestionsAreStillUngrouped(string group)
	{
		foreach (var key in SeededGroups[group].ChildKeys)
		{
			var (id, revisions) = _groupChildrenBefore[key];
			(await LiveQuestionId(key)).ShouldBe(id);
			(await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", id))).ShouldBe(revisions);
			(await CurrentGroupOf(id)).ShouldBeNull();
		}
	}

	// ---------------------------------------- what the API sends (QB-259, 265) --

	[Then(@"the group ""(.*)"" holds (.*), in that order")]
	public void ThenTheGroupHoldsItsQuestions(string group,
											  string labels)
	{
		_groupReadFromApi = group;
		var sent = TopLevel().Single(question => question.GetProperty("key").GetString() == SeededGroups[group].Key);
		sent.GetProperty("type").GetString().ShouldBe("group");
		sent.GetProperty("labelEn").GetString().ShouldBe(group);

		var children = sent.GetProperty("children").EnumerateArray().ToList();
		children.Select(child => child.GetProperty("key").GetString()).ShouldBe(SeededGroups[group].ChildKeys);
		children.Select(child => child.GetProperty("labelEn").GetString()).ShouldBe(Quoted(labels));
	}

	[Then(@"none of those questions is sent at the top level")]
	public void ThenNoneIsSentAtTheTopLevel()
	{
		var topLevelKeys = TopLevel().Select(question => question.GetProperty("key").GetString()).ToHashSet();
		foreach (var key in SeededGroups[_groupReadFromApi!].ChildKeys)
		{
			topLevelKeys.ShouldNotContain(key);
		}
	}

	[Then(@"they are exactly the browser suite's seeded-form fixture")]
	public async Task ThenTheyAreTheBrowserFixture()
	{
		var path = Path.Combine(GroupRepositoryRoot(), "tests", "e2e", "fixtures", "seeded-questions.json");
		var sent = JsonNode.Parse(_publicForm.GetRawText())!;

		if (Environment.GetEnvironmentVariable(WriteFixtureVariable) == "1")
		{
			await File.WriteAllTextAsync(path, sent.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
		}

		File.Exists(path).ShouldBeTrue($"No fixture at {path}. Run this scenario with {WriteFixtureVariable}=1 to write it.");
		var fixture = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
		JsonNode.DeepEquals(sent, fixture).ShouldBeTrue(
			$"tests/e2e/fixtures/seeded-questions.json no longer matches what a freshly migrated database sends. "
			+ $"Re-run this scenario with {WriteFixtureVariable}=1 and commit the fixture.");
	}

	private JsonElement.ArrayEnumerator TopLevel()
	{
		return _publicForm.EnumerateArray();
	}

	private static string[] Quoted(string list)
	{
		return [.. Regex.Matches(list, "\"([^\"]*)\"").Select(match => match.Groups[1].Value)];
	}

	private Task<string> LiveQuestionId(string key)
	{
		return Text("SELECT id FROM questions WHERE key = @key AND deleted IS NULL", ("key", key));
	}

	private async Task<string?> CurrentGroupOf(string questionId)
	{
		await using var connection = await Open();
		await using var command = Command(
			connection,
			"SELECT grouped_under_question_id FROM question_revisions WHERE question_id = @id ORDER BY revision_number DESC LIMIT 1",
			[("id", questionId)]);
		var value = await command.ExecuteScalarAsync();
		return value is null or DBNull ? null : ((string)value).Trim();
	}

	private static string GroupRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null
			   && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
	}
}
