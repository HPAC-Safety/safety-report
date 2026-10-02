using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using HpacSafety.Infrastructure.Persistence.Sql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The seeded Country question as a pinned country pick list, and Province
///     following it (<c>REQ-QB-249</c> to <c>REQ-QB-255</c>, ADR-0186).
/// </summary>
/// <remarks>
///     A migration's effect is a fact about the database, so these run the real
///     migrations against a real PostgreSQL database. A database seeded before the
///     change is reproduced by migrating to the migration before it. Every report
///     and question added here is synthetic.
/// </remarks>
public sealed partial class SeededWordingSteps
{
	private const string CountryPriorMigration = "TrimLabelColons";
	private const string CountryMigration = "MakeCountryAPinnedPickList";
	private const string CountryScript = "20261002181219_MakeCountryAPinnedPickList.sql";
	private const string CountryKey = "2a401575_8cf4_4c87_b8df_95798d07a772";
	private const string ProvinceKey = "849ea0c4_b36e_44a7_936e_e578967907a3";
	private const string NewCountryHelpEn = "Country where the occurrence happened.";
	private const string NewCountryHelpFr = "Pays où l'évènement a eu lieu.";

	private static readonly Uri PublicQuestionsUri = new("/api/v1/questions", UriKind.Relative);

	private string? _originalCountryId;
	private string? _originalProvinceId;
	private int _countryRevisionsBefore;
	private int _provinceRevisionsBefore;
	private string _snapshotBefore = "";
	private string _countryHistoryBefore = "";
	private string? _followUpQuestionId;
	private JsonElement _publicForm;

	// ---------------------------------------------------------- the database --

	[Given(@"^a database whose Country question is still the seeded yes/no question$")]
	public async Task GivenTheSeededCountryQuestion()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(CountryPriorMigration);

		_originalCountryId = await Text("SELECT id FROM questions WHERE key = @key AND deleted IS NULL", ("key", CountryKey));
		_originalProvinceId = await Text("SELECT id FROM questions WHERE key = @key AND deleted IS NULL", ("key", ProvinceKey));
		_countryRevisionsBefore = (int)await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", _originalCountryId));
		_provinceRevisionsBefore = (int)await Count("SELECT count(*) FROM question_revisions WHERE question_id = @id", ("id", _originalProvinceId));
	}

	[Given(@"a database whose Country question an Administrator has already reworded")]
	public async Task GivenAnAdministratorRewordedCountry()
	{
		await GivenTheSeededCountryQuestion();
		await Execute(
			"""
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
				 is_system, is_required, is_private, is_active, display_order, created_at)
			SELECT 'revcountry1', question_id, revision_number + 1, type, 'Nation', 'Nation',
			       'Which nation was it?', 'Dans quelle nation?',
			       is_system, is_required, is_private, is_active, display_order, TIMESTAMPTZ '2026-09-10T00:00:00Z'
			FROM question_revisions WHERE question_id = @questionId
			ORDER BY revision_number DESC LIMIT 1
			""",
			("questionId", _originalCountryId!));
		_countryHistoryBefore = await History(CountryKey);
		_snapshotBefore = await Snapshot();
	}

	[Given(@"no answer references the Country question")]
	public async Task GivenNoAnswerReferencesCountry()
	{
		(await Count("SELECT count(*) FROM report_answers WHERE question_id = @id", ("id", _originalCountryId!))).ShouldBe(0);
	}

	[Given(@"a report has answered the Country question yes")]
	public async Task GivenAReportAnsweredCountryYes()
	{
		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES ('rptcountry1', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO report_answers
				(id, report_id, question_id, question_revision_id, question_key, is_private, value_boolean, locale, translation_mode, answered_at)
			SELECT 'anscountry1', 'rptcountry1', q.id, r.id, q.key, FALSE, TRUE, 'en-CA', 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z'
			FROM questions q JOIN question_revisions r ON r.question_id = q.id
			WHERE q.id = @questionId
			ORDER BY r.revision_number DESC
			LIMIT 1
			""",
			("questionId", _originalCountryId!));
	}

	[Given(@"a report has answered the Province question ""(.*)""")]
	public async Task GivenAReportAnsweredProvince(string province)
	{
		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES ('rptprovnc01', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO report_answers
				(id, report_id, question_id, question_revision_id, question_key, is_private, value, locale, translation_mode, choice_id, answered_at)
			SELECT 'ansprovnce1', 'rptprovnc01', q.id,
			       (SELECT r.id FROM question_revisions r WHERE r.question_id = q.id ORDER BY r.revision_number DESC LIMIT 1),
			       q.key, FALSE, c.label_en, 'en-CA', 'choice', c.id, TIMESTAMPTZ '2026-09-01T00:00:00Z'
			FROM questions q JOIN question_choices c ON c.question_id = q.id AND c.label_en = @province
			WHERE q.id = @questionId
			""",
			("questionId", _originalProvinceId!), ("province", province));
	}

	[Given(@"another question is shown only when the old Country question is answered yes")]
	public async Task GivenAFollowUpQuestion()
	{
		_followUpQuestionId = "qfollowup01";
		await Execute(
			"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qfollowup01', 'synthetic_follow_up', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private,
				 is_active, display_order, depends_on_question_id, created_at)
			VALUES ('rfollowup01', 'qfollowup01', 1, 'short_text', 'Where in Canada', 'Où au Canada',
			        FALSE, FALSE, FALSE, TRUE, 99, @countryId, TIMESTAMPTZ '2026-09-01T00:00:00Z')
			""",
			("countryId", _originalCountryId!));
	}

	[Given(@"the Country pick list migration has run")]
	public async Task GivenTheCountryMigrationHasRun()
	{
		await MigrateTo(CountryMigration);
		_snapshotBefore = await Snapshot();
	}

	// ------------------------------------------------------------------ when --

	[When(@"the Country pick list migration runs")]
	public async Task WhenTheCountryMigrationRuns()
	{
		await MigrateTo(CountryMigration);
	}

	[When(@"the Country pick list script is run again")]
	public async Task WhenTheCountryScriptIsRunAgain()
	{
		await Execute(SqlScript.Read(CountryScript));
	}

	[When(@"the report form's questions are read from the API")]
	public async Task WhenTheFormIsReadFromTheApi()
	{
		var host = await BootedApi.Factory();
		using var reader = host.CreateClient();
		_publicForm = await reader.GetFromJsonAsync<JsonElement>(PublicQuestionsUri);
	}

	// ----------------------------------------------- the API's view (QB-249) --

	[Then(@"the Country question is an optional single-select labelled ""(.*)"" and ""(.*)""")]
	public void ThenCountryIsAnOptionalSingleSelect(string labelEn,
													string labelFr)
	{
		var country = PublicQuestion(CountryKey);
		country.GetProperty("type").GetString().ShouldBe("single_select");
		country.GetProperty("isRequired").GetBoolean().ShouldBeFalse();
		country.GetProperty("labelEn").GetString().ShouldBe(labelEn);
		country.GetProperty("labelFr").GetString().ShouldBe(labelFr);
	}

	[Then(@"its help text is ""(.*)"" and ""(.*)""")]
	public void ThenItsHelpTextIs(string helpEn,
								  string helpFr)
	{
		var country = PublicQuestion(CountryKey);
		country.GetProperty("helpTextEn").GetString().ShouldBe(helpEn);
		country.GetProperty("helpTextFr").GetString().ShouldBe(helpFr);
	}

	[Then(@"it offers 249 countries, each coded by its lowercase ISO 3166-1 alpha-2 code")]
	public void ThenItOffersEveryCountry()
	{
		var codes = PublicQuestion(CountryKey).GetProperty("options").EnumerateArray()
			.Select(option => option.GetProperty("code").GetString()!).ToList();

		codes.Count.ShouldBe(249);
		codes.Distinct().Count().ShouldBe(249);
		codes.ShouldAllBe(code => Regex.IsMatch(code, "^[a-z]{2}$"));
	}

	[Then(@"""(.*)"" and ""(.*)"" are pinned first and every other country is not pinned")]
	public void ThenCanadaAndTheUnitedStatesArePinnedFirst(string first,
														   string second)
	{
		var pins = PublicQuestion(CountryKey).GetProperty("options").EnumerateArray()
			.ToDictionary(option => option.GetProperty("labelEn").GetString()!, option => option.GetProperty("pin").GetString());

		pins[first].ShouldBe("first");
		pins[second].ShouldBe("first");
		pins.Where(pin => pin.Key != first && pin.Key != second).ShouldAllBe(pin => pin.Value == "none");
	}

	[Then(@"the French wording of ""(.*)"" is ""(.*)""")]
	public void ThenTheFrenchWordingIs(string code,
									   string wording)
	{
		PublicQuestion(CountryKey).GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("code").GetString() == code)
			.GetProperty("labelFr").GetString().ShouldBe(wording);
	}

	[Then(@"the Province question depends on the Canada choice of the Country question")]
	public void ThenProvinceDependsOnCanada()
	{
		var country = PublicQuestion(CountryKey);
		var canada = country.GetProperty("options").EnumerateArray().Single(option => option.GetProperty("code").GetString() == "ca");
		var province = PublicQuestion(ProvinceKey);

		province.GetProperty("dependsOnQuestionId").GetString().ShouldBe(country.GetProperty("id").GetString());
		province.GetProperty("dependsOnChoiceId").GetString().ShouldBe(canada.GetProperty("id").GetString());
	}

	// -------------------------------------------------- the revised path (QB-250) --

	[Then(@"the Country question has a new revision as a single-select with the pick-list wording")]
	public async Task ThenCountryHasANewRevision()
	{
		var live = await LiveRevisionOf(CountryKey);
		live.RevisionNumber.ShouldBe(_countryRevisionsBefore + 1);
		live.Type.ShouldBe("single_select");
		(live.LabelEn, live.LabelFr, live.HelpEn, live.HelpFr).ShouldBe(("Country", "Pays", NewCountryHelpEn, NewCountryHelpFr));
		live.IsRequired.ShouldBeFalse();
	}

	[Then(@"the Country question keeps its identifier and offers 249 countries")]
	public async Task ThenCountryKeepsItsIdentifier()
	{
		(await LiveRevisionOf(CountryKey)).QuestionId.ShouldBe(_originalCountryId);
		(await Count("SELECT count(*) FROM questions WHERE key = @key", ("key", CountryKey))).ShouldBe(1);
		(await Count("SELECT count(*) FROM question_choices WHERE question_id = @id", ("id", _originalCountryId!))).ShouldBe(249);
	}

	[Then(@"the Province question has a new revision that depends on the Canada choice and keeps its identifier")]
	public async Task ThenProvinceHasANewConditionalRevision()
	{
		var live = await LiveRevisionOf(ProvinceKey);
		live.QuestionId.ShouldBe(_originalProvinceId);
		live.RevisionNumber.ShouldBe(_provinceRevisionsBefore + 1);
		await ProvinceShouldFollowLiveCanada(live);
	}

	// ---------------------------------------------------- the forked path (QB-251) --

	[Then(@"^the original Country question is stamped as deleted and keeps its yes/no wording and its answer$")]
	public async Task ThenTheOriginalCountryIsRetired()
	{
		(await Count("SELECT count(*) FROM questions WHERE id = @id AND deleted IS NOT NULL", ("id", _originalCountryId!))).ShouldBe(1);
		(await Count(
				"SELECT count(*) FROM question_revisions WHERE question_id = @id AND type = 'yes_no' AND help_text_en = 'Did the occurrence happen in Canada?'",
				("id", _originalCountryId!)))
			.ShouldBe(_countryRevisionsBefore);
		(await Count("SELECT count(*) FROM report_answers WHERE question_id = @id AND value_boolean IS TRUE", ("id", _originalCountryId!)))
			.ShouldBe(1);
	}

	[Then(@"a new live question with the same key is a single-select offering 249 countries")]
	public async Task ThenACountryReplacementExists()
	{
		var live = await LiveRevisionOf(CountryKey);
		live.QuestionId.ShouldNotBe(_originalCountryId);
		live.RevisionNumber.ShouldBe(1);
		live.Type.ShouldBe("single_select");
		(live.HelpEn, live.HelpFr).ShouldBe((NewCountryHelpEn, NewCountryHelpFr));
		(await Count("SELECT count(*) FROM question_choices WHERE question_id = @id", ("id", live.QuestionId))).ShouldBe(249);
	}

	[Then(@"no answer is created, changed, or deleted")]
	public async Task ThenNoAnswerChanged()
	{
		(await Count("SELECT count(*) FROM report_answers")).ShouldBe(1);
		(await Count("SELECT count(*) FROM report_answers WHERE id = 'anscountry1' AND question_id = @id AND value_boolean IS TRUE", ("id", _originalCountryId!)))
			.ShouldBe(1);
	}

	// -------------------------------------------- the answered Province (QB-252) --

	[Then(@"the original Province question is stamped as deleted and keeps its answer")]
	public async Task ThenTheOriginalProvinceIsRetired()
	{
		(await Count("SELECT count(*) FROM questions WHERE id = @id AND deleted IS NOT NULL", ("id", _originalProvinceId!))).ShouldBe(1);
		(await Count("SELECT count(*) FROM report_answers WHERE id = 'ansprovnce1' AND question_id = @id", ("id", _originalProvinceId!)))
			.ShouldBe(1);
	}

	[Then(@"a new live Province question with the same key offers the same 13 provinces and depends on the Canada choice")]
	public async Task ThenAProvinceReplacementExists()
	{
		var live = await LiveRevisionOf(ProvinceKey);
		live.QuestionId.ShouldNotBe(_originalProvinceId);
		live.RevisionNumber.ShouldBe(1);
		(await Count("SELECT count(*) FROM question_choices WHERE question_id = @id AND deleted IS NULL", ("id", live.QuestionId))).ShouldBe(13);
		(await Text("SELECT string_agg(code, ',' ORDER BY code) FROM question_choices WHERE question_id = @id", ("id", live.QuestionId)))
			.ShouldBe(await Text("SELECT string_agg(code, ',' ORDER BY code) FROM question_choices WHERE question_id = @id", ("id", _originalProvinceId!)));
		await ProvinceShouldFollowLiveCanada(live);
	}

	[Then(@"the Province answer still names the choice it named")]
	public async Task ThenTheProvinceAnswerStillNamesItsChoice()
	{
		(await Count(
				"SELECT count(*) FROM report_answers a JOIN question_choices c ON c.id = a.choice_id WHERE a.id = 'ansprovnce1' AND c.question_id = @id AND c.label_en = 'Ontario'",
				("id", _originalProvinceId!)))
			.ShouldBe(1);
	}

	// ------------------------------------------- left alone and re-run (QB-253, 254) --

	[Then(@"the questions, revisions, and choices are unchanged")]
	public async Task ThenNothingChangedInTheBank()
	{
		(await Snapshot()).ShouldBe(_snapshotBefore);
	}

	[Then(@"the Country question and its revisions are unchanged")]
	public async Task ThenCountryIsUnchanged()
	{
		(await History(CountryKey)).ShouldBe(_countryHistoryBefore);
		(await Snapshot()).ShouldBe(_snapshotBefore);
	}

	[Then(@"the Province question is still unconditional")]
	public async Task ThenProvinceIsStillUnconditional()
	{
		var live = await LiveRevisionOf(ProvinceKey);
		live.QuestionId.ShouldBe(_originalProvinceId);
		live.RevisionNumber.ShouldBe(_provinceRevisionsBefore);
		live.DependsOnQuestionId.ShouldBeNull();
		live.DependsOnChoiceId.ShouldBeNull();
	}

	// ------------------------------------------------------------- QB-255 --

	[Then(@"that question is shown only when Country is the Canada choice")]
	public async Task ThenTheFollowUpWaitsForCanada()
	{
		var live = await LiveRevisionOfQuestion(_followUpQuestionId!);
		live.RevisionNumber.ShouldBe(2);
		var country = await LiveRevisionOf(CountryKey);
		live.DependsOnQuestionId.ShouldBe(country.QuestionId);
		live.DependsOnChoiceId.ShouldBe(await CanadaChoiceId(country.QuestionId));
	}

	// -------------------------------------------------------------- helpers --

	private JsonElement PublicQuestion(string key)
	{
		return _publicForm.EnumerateArray().Single(question => question.GetProperty("key").GetString() == key);
	}

	private async Task ProvinceShouldFollowLiveCanada(CountryRevision province)
	{
		var country = await LiveRevisionOf(CountryKey);
		province.DependsOnQuestionId.ShouldBe(country.QuestionId);
		province.DependsOnChoiceId.ShouldBe(await CanadaChoiceId(country.QuestionId));
		(await Count(
				"SELECT count(*) FROM question_choices WHERE id = @id AND deleted IS NULL AND code = 'ca' AND pin = 'first'",
				("id", province.DependsOnChoiceId!)))
			.ShouldBe(1);
	}

	private async Task<string> CanadaChoiceId(string questionId)
	{
		return await Text("SELECT id FROM question_choices WHERE question_id = @id AND code = 'ca'", ("id", questionId));
	}

	private Task<CountryRevision> LiveRevisionOf(string key)
	{
		return ReadRevision("q.key = @value AND q.deleted IS NULL", key);
	}

	private Task<CountryRevision> LiveRevisionOfQuestion(string questionId)
	{
		return ReadRevision("q.id = @value", questionId);
	}

	private async Task<CountryRevision> ReadRevision(string where,
													  string value)
	{
		await using var connection = await Open();
		await using var command = new Npgsql.NpgsqlCommand(
			$"""
			SELECT q.id, r.revision_number, r.type, r.label_en, r.label_fr, coalesce(r.help_text_en, ''),
			       coalesce(r.help_text_fr, ''), r.is_required, r.depends_on_question_id, r.depends_on_choice_id
			FROM questions q
			JOIN question_revisions r ON r.question_id = q.id
			WHERE {where}
			ORDER BY r.revision_number DESC
			LIMIT 1
			""",
			connection);
		command.Parameters.AddWithValue("value", value);
		await using var reader = await command.ExecuteReaderAsync();
		(await reader.ReadAsync()).ShouldBeTrue("No such live question.");

		return new CountryRevision(
			reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
			reader.GetString(5), reader.GetString(6), reader.GetBoolean(7),
			reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9));
	}

	/// <summary>Every revision of a key, live or retired, as one comparable string.</summary>
	private Task<string> History(string key)
	{
		return Text(
			"""
			SELECT coalesce(string_agg(r.id || '|' || r.revision_number || '|' || r.type || '|' || r.label_en, ';' ORDER BY r.question_id, r.revision_number), '')
			FROM question_revisions r JOIN questions q ON q.id = r.question_id
			WHERE q.key = @key
			""",
			("key", key));
	}

	/// <summary>Every question, revision, and choice row, as one comparable string.</summary>
	private Task<string> Snapshot()
	{
		return Text(
			"""
			SELECT (SELECT string_agg(id || '|' || key || '|' || coalesce(deleted::text, ''), ';' ORDER BY id) FROM questions)
			    || '#' || (SELECT string_agg(id || '|' || type || '|' || coalesce(depends_on_choice_id, ''), ';' ORDER BY id) FROM question_revisions)
			    || '#' || (SELECT string_agg(id || '|' || pin || '|' || coalesce(deleted::text, ''), ';' ORDER BY id) FROM question_choices)
			""");
	}

	private sealed record CountryRevision(
		string QuestionId,
		int RevisionNumber,
		string Type,
		string LabelEn,
		string LabelFr,
		string HelpEn,
		string HelpFr,
		bool IsRequired,
		string? DependsOnQuestionId,
		string? DependsOnChoiceId);
}
