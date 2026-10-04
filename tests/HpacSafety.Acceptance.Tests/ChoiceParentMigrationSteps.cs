using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The migration that folds each dependent choice's one parent link into
///     <c>question_choice_parents</c> and merges identical duplicates
///     (<c>REQ-QB-225</c>, <c>REQ-QB-226</c>, ADR-0151).
/// </summary>
/// <remarks>
///     A migration's effect is a fact about the database, so this runs the real
///     migrations against a real PostgreSQL database: up to the one before, then a
///     question bank shaped like today's dependent <c>Certification:</c> question —
///     <c>EN-A</c> to <c>EN-D</c> entered once under each of two aircraft types, and
///     <c>EN-CCC</c> once — then the rest. Every question, choice, and answer here is
///     synthetic.
/// </remarks>
[Binding]
public sealed class ChoiceParentMigrationSteps
{
	private const string PriorMigration = "LinkChoicesToParentChoices";
	private const string Paraglider = "cparaglide1";
	private const string HangGlider = "changglide1";
	private const string ParagliderTandem = "cparatandm1";

	private static readonly string[] Ratings = ["a", "b", "c", "d"];

	private string? _connectionString;

	private string ConnectionString =>
		_connectionString ?? throw new InvalidOperationException("No database has been created for this scenario.");

	[Given(@"a database one migration short, whose dependent {string} question offers {string} to {string} twice each, one copy under {string} and one under {string}, and {string} once, under {string}, with reports answering both copies of {string} and a question conditional on the {string} copy of {string}")]
	public async Task GivenTodaysCertificationDuplicatesAnsweredAndConditional(string question,
																			  string first,
																			  string last,
																			  string firstParent,
																			  string secondParent,
																			  string once,
																			  string onceParent,
																			  string answered,
																			  string conditionParent,
																			  string condition)
	{
		await GivenTodaysCertificationDuplicates(question, first, last, firstParent, secondParent, once, onceParent);
		await GivenReportsAnsweredBothCopies(question, answered);
		await GivenAConditionOnACopy(conditionParent, condition);
	}

	private async Task GivenTodaysCertificationDuplicates(string question,
														  string first,
														  string last,
														  string firstParent,
														  string secondParent,
														  string once,
														  string onceParent)
	{
		first.ShouldBe("EN-A");
		last.ShouldBe("EN-D");
		firstParent.ShouldBe("Paraglider");
		secondParent.ShouldBe("Hang Glider");
		once.ShouldBe("EN-CCC");
		onceParent.ShouldBe("Paraglider");

		await BeforeTheMigration();

		var choices = string.Join(
			",\n",
			Ratings.SelectMany(rating => new[]
			{
				$"('ccert{rating}00001', 'qcert000001', 'en_{rating}', 0, 'EN-{rating.ToUpperInvariant()}', 'EN-{rating.ToUpperInvariant()}', '{Paraglider}', 'human', 'human')",
				$"('ccert{rating}00002', 'qcert000001', 'en_{rating}_2', 1, 'EN-{rating.ToUpperInvariant()}', 'EN-{rating.ToUpperInvariant()}', '{HangGlider}', 'human', 'human')",
			}));

		await Execute(
			$"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qcert000001', 'synthetic_certification', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, created_at)
			VALUES ('rcert000001', 'qcert000001', 1, 'single_select', '{question}', 'Homologation:', FALSE, FALSE, FALSE, TRUE, 91, TIMESTAMPTZ '2026-09-01T00:00:00Z');
			UPDATE questions SET choices_depend_on_question_id = 'qaircraft01' WHERE id = 'qcert000001';
			INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr, parent_choice_id, label_en_source, label_fr_source)
			VALUES {choices},
			       ('ccertccc001', 'qcert000001', 'en_ccc', 2, 'EN-CCC', 'EN-CCC', '{Paraglider}', 'human', 'human');
			""");
	}

	private async Task GivenReportsAnsweredBothCopies(string _,
													  string __)
	{
		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES ('rptcertpara', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z'),
			       ('rptcerthang', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO report_answers
				(id, report_id, question_id, question_revision_id, question_key, is_private, choice_id, locale, translation_mode, answered_at)
			VALUES ('anscertpara', 'rptcertpara', 'qcert000001', 'rcert000001', 'synthetic_certification', FALSE, 'ccerta00001', 'en-CA', 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z'),
			       ('anscerthang', 'rptcerthang', 'qcert000001', 'rcert000001', 'synthetic_certification', FALSE, 'ccerta00002', 'en-CA', 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			""");
	}

	private async Task GivenAConditionOnACopy(string _,
											  string __)
	{
		await Execute(
			"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qcond000001', 'synthetic_harness_check', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, depends_on_question_id, depends_on_choice_id, created_at)
			VALUES ('rcond000001', 'qcond000001', 1, 'yes_no', 'Harness checked?', 'Sellette vérifiée ?', FALSE, FALSE, FALSE, TRUE, 92, 'qcert000001', 'ccertb00002', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			""");
	}

	[Given(@"a database one migration short, whose dependent question offers {string} \/ {string} under {string} and {string} \/ {string} under {string}")]
	public async Task GivenAOneLanguageMatch(string firstEn,
											 string firstFr,
											 string firstParent,
											 string secondEn,
											 string secondFr,
											 string secondParent)
	{
		await BeforeTheMigration();
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(
			"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qcert000002', 'synthetic_certification_two', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, created_at)
			VALUES ('rcert000002', 'qcert000002', 1, 'single_select', 'Certification two', 'Homologation deux', FALSE, FALSE, FALSE, TRUE, 91, TIMESTAMPTZ '2026-09-01T00:00:00Z');
			UPDATE questions SET choices_depend_on_question_id = 'qaircraft01' WHERE id = 'qcert000002';
			INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr, parent_choice_id, label_en_source, label_fr_source)
			VALUES ('cpairfirst1', 'qcert000002', 'en_a', 0, @firstEn, @firstFr, (SELECT id FROM question_choices WHERE question_id = 'qaircraft01' AND label_en = @firstParent), 'human', 'human'),
			       ('cpairsecnd1', 'qcert000002', 'en_a_2', 1, @secondEn, @secondFr, (SELECT id FROM question_choices WHERE question_id = 'qaircraft01' AND label_en = @secondParent), 'human', 'human');
			""",
			connection);
		command.Parameters.AddWithValue("firstEn", firstEn);
		command.Parameters.AddWithValue("firstFr", firstFr);
		command.Parameters.AddWithValue("firstParent", firstParent);
		command.Parameters.AddWithValue("secondEn", secondEn);
		command.Parameters.AddWithValue("secondFr", secondFr);
		command.Parameters.AddWithValue("secondParent", secondParent);
		await command.ExecuteNonQueryAsync();
	}

	[Given(@"a database one migration short, whose dependent type-ahead {string} question offers {string} twice, one copy under {string} and one under {string}")]
	public async Task GivenTypeAheadDuplicates(string question,
											   string wording,
											   string firstParent,
											   string secondParent)
	{
		firstParent.ShouldBe("Paraglider");
		secondParent.ShouldBe("Hang Glider");
		await BeforeTheMigration();
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(
			$"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qcertauto01', 'synthetic_certification_typed', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, created_at)
			VALUES ('rcertauto01', 'qcertauto01', 1, 'autocomplete', @question, 'Homologation:', FALSE, FALSE, FALSE, TRUE, 91, TIMESTAMPTZ '2026-09-01T00:00:00Z');
			UPDATE questions SET choices_depend_on_question_id = 'qaircraft01' WHERE id = 'qcertauto01';
			INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr, parent_choice_id, label_en_source, label_fr_source)
			VALUES ('cautoa00001', 'qcertauto01', 'en_a', 0, @wording, @wording, '{Paraglider}', 'human', 'human'),
			       ('cautoa00002', 'qcertauto01', 'en_a_2', 1, @wording, @wording, '{HangGlider}', 'human', 'human');
			""",
			connection);
		command.Parameters.AddWithValue("question", question);
		command.Parameters.AddWithValue("wording", wording);
		await command.ExecuteNonQueryAsync();
	}

	[Given(@"the value {string} was merged into the {string} copy")]
	public async Task GivenAnEarlierMerge(string value,
										  string _)
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(
			"""
			INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_en_source, added_by_reporter, reporter_locale, deleted, merged_into_choice_id, parent_choice_id)
			VALUES ('cautoenasp1', 'qcertauto01', 'en_a_typed', 2, @value, 'human', TRUE, 'en-CA', TIMESTAMPTZ '2026-09-02T00:00:00Z', 'cautoa00002', 'changglide1');
			""",
			connection);
		command.Parameters.AddWithValue("value", value);
		await command.ExecuteNonQueryAsync();
	}

	[Given(@"a report answered {string} with the {string} copy")]
	public async Task GivenAnAnswerNamingTheCopy(string _,
												 string __)
	{
		await Execute(
			"""
			INSERT INTO reports (id, language, status, submitted_at)
			VALUES ('rptcertauto', 'en-CA', 'submitted', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO report_answers
				(id, report_id, question_id, question_revision_id, question_key, is_private, choice_id, locale, translation_mode, answered_at)
			VALUES ('anscertauto', 'rptcertauto', 'qcertauto01', 'rcertauto01', 'synthetic_certification_typed', FALSE, 'cautoa00002', 'en-CA', 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			""");
	}

	[When(@"the migration runs")]
	public async Task WhenTheMigrationRuns()
	{
		await MigrateTo(null);
	}

	[Then(@"{string} offers one {string} to {string} each, the oldest copy, offered under {string} and {string}")]
	public async Task ThenOneOfEachUnderBoth(string _,
											 string __,
											 string ___,
											 string ____,
											 string _____)
	{
		foreach (var rating in Ratings)
		{
			var survivor = $"ccert{rating}00001";
			(await Scalar($"SELECT (deleted IS NULL)::text FROM question_choices WHERE id = '{survivor}'")).ShouldBe("true");
			(await Parents(survivor)).ShouldBe([HangGlider, Paraglider]);
		}

		(await Scalar("SELECT count(*)::text FROM question_choices WHERE question_id = 'qcert000001' AND deleted IS NULL")).ShouldBe("5");
	}

	[Then(@"each other copy is retired, replaced by the one that survived")]
	public async Task ThenEachCopyIsReplaced()
	{
		foreach (var rating in Ratings)
		{
			(await Scalar($"SELECT (deleted IS NOT NULL)::text || ':' || replaced_by_choice_id FROM question_choices WHERE id = 'ccert{rating}00002'"))
				.ShouldBe($"true:ccert{rating}00001");
		}
	}

	[Then(@"{string} is offered under {string} only")]
	public async Task ThenTheSingleCopyKeepsItsParent(string _,
													  string __)
	{
		(await Parents("ccertccc001")).ShouldBe([Paraglider]);
		(await Parents("ccertccc001")).ShouldNotContain(ParagliderTandem);
	}

	[Then(@"every answer still names the choice it named and reads the same wording")]
	public async Task ThenAnswersAreUntouched()
	{
		(await Scalar("SELECT choice_id FROM report_answers WHERE id = 'anscertpara'")).ShouldBe("ccerta00001");
		(await Scalar("SELECT choice_id FROM report_answers WHERE id = 'anscerthang'")).ShouldBe("ccerta00002");

		// The retired copy still reads "EN-A", as the survivor does.
		(await Scalar(
				"SELECT choice.label_en || '/' || choice.label_fr FROM report_answers AS answer JOIN question_choices AS choice ON choice.id = answer.choice_id WHERE answer.id = 'anscerthang'"))
			.ShouldBe("EN-A/EN-A");
	}

	[Then(@"the conditional question's condition follows the surviving {string}")]
	public async Task ThenTheConditionFollows(string _)
	{
		await using var context = WorkerDatabase.ContextFor(ConnectionString);
		var bank = await context.Questions.AsNoTracking()
			.Include(question => question.Revisions)
			.Include(question => question.AllChoices)
			.ToListAsync();
		var conditional = bank.Single(question => question.Key == "synthetic_harness_check");

		// The revision still names the copy; the condition reads the survivor (ADR-0128, ADR-0132).
		conditional.CurrentRevision.DependsOnChoiceId.ShouldBe(TinyId.Parse("ccertb00002"));
		QuestionDependencies.RequiredChoiceToday(bank, conditional.CurrentRevision)!.Id.ShouldBe(TinyId.Parse("ccertb00001"));
	}

	[Then(@"{string} is offered once, the oldest copy, under {string} and {string}")]
	public async Task ThenTheTypedOneIsUnderBoth(string _,
												 string __,
												 string ___)
	{
		(await Scalar("SELECT count(*)::text FROM question_choices WHERE question_id = 'qcertauto01' AND deleted IS NULL")).ShouldBe("1");
		(await Scalar("SELECT (deleted IS NULL)::text FROM question_choices WHERE id = 'cautoa00001'")).ShouldBe("true");
		(await Parents("cautoa00001")).ShouldBe([HangGlider, Paraglider]);
	}

	[Then(@"the other copy is merged into it, and so is {string}")]
	public async Task ThenTheCopyAndItsMergeFollow(string _)
	{
		(await Scalar("SELECT (deleted IS NOT NULL)::text || ':' || merged_into_choice_id || ':' || coalesce(replaced_by_choice_id, '-') FROM question_choices WHERE id = 'cautoa00002'"))
			.ShouldBe("true:cautoa00001:-");
		(await Scalar("SELECT merged_into_choice_id FROM question_choices WHERE id = 'cautoenasp1'")).ShouldBe("cautoa00001");
	}

	[Then(@"the answer still names the copy it named, which reads as {string}")]
	public async Task ThenTheAnswerReadsTheSurvivor(string wording)
	{
		(await Scalar("SELECT choice_id FROM report_answers WHERE id = 'anscertauto'")).ShouldBe("cautoa00002");

		await using var context = WorkerDatabase.ContextFor(ConnectionString);
		var copy = await context.QuestionChoices.AsNoTracking()
			.Include(choice => choice.MergedInto)
			.SingleAsync(choice => choice.Id == TinyId.Parse("cautoa00002"));
		copy.Resolved.Id.ShouldBe(TinyId.Parse("cautoa00001"));
		copy.Resolved.LabelEn.ShouldBe(wording);
	}

	[Then(@"no choice keeps its old single parent link")]
	public async Task ThenTheColumnIsGone()
	{
		(await Scalar(
				"SELECT count(*)::text FROM information_schema.columns WHERE table_name = 'question_choices' AND column_name = 'parent_choice_id'"))
			.ShouldBe("0");
	}

	[Then(@"both choices stay live, each under its own parent choice")]
	public async Task ThenBothStayLive()
	{
		(await Scalar("SELECT count(*)::text FROM question_choices WHERE question_id = 'qcert000002' AND deleted IS NULL")).ShouldBe("2");
		(await Parents("cpairfirst1")).ShouldBe([Paraglider]);
		(await Parents("cpairsecnd1")).ShouldBe([HangGlider]);
	}

	[Then(@"the question's next save is refused, naming {string}")]
	public async Task ThenTheNextSaveIsRefused(string wording)
	{
		await using var context = WorkerDatabase.ContextFor(ConnectionString);
		var question = await context.Questions
			.Include(candidate => candidate.Revisions)
			.Include(candidate => candidate.AllChoices)
			.SingleAsync(candidate => candidate.Key == "synthetic_certification_two");

		var options = question.Choices
			.Select(choice => new QuestionOptionInput(choice.Code, choice.LabelEn, choice.LabelFr, ParentChoiceIds: choice.ParentChoiceIds))
			.ToList();

		Should.Throw<DomainRuleViolationException>(() => question.ReplaceChoices(options, DateTimeOffset.UtcNow))
			.Message.ShouldContain($"'{wording}'");
	}

	/// <summary>A database one migration short, holding the aircraft-type parent every scenario here uses.</summary>
	private async Task BeforeTheMigration()
	{
		_connectionString = await WorkerDatabase.NewEmptyDatabase();
		await MigrateTo(PriorMigration);

		await Execute(
			$"""
			INSERT INTO questions (id, key, is_system, role, created_at)
			VALUES ('qaircraft01', 'synthetic_aircraft_type', FALSE, 'none', TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_revisions
				(id, question_id, revision_number, type, label_en, label_fr, is_system, is_required, is_private, is_active, display_order, created_at)
			VALUES ('raircraft01', 'qaircraft01', 1, 'single_select', 'Type of aircraft', 'Type d''aéronef', FALSE, FALSE, FALSE, TRUE, 90, TIMESTAMPTZ '2026-09-01T00:00:00Z');
			INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr, label_en_source, label_fr_source)
			VALUES ('{Paraglider}', 'qaircraft01', 'paraglider', 0, 'Paraglider', 'Parapente', 'human', 'human'),
			       ('{HangGlider}', 'qaircraft01', 'hang_glider', 1, 'Hang Glider', 'Deltaplane', 'human', 'human'),
			       ('{ParagliderTandem}', 'qaircraft01', 'paraglider_tandem', 2, 'Paraglider Tandem', 'Parapente biplace', 'human', 'human');
			""");
	}

	private async Task<string[]> Parents(string choiceId)
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(
			"SELECT parent_choice_id FROM question_choice_parents WHERE choice_id = @choice AND deleted IS NULL ORDER BY parent_choice_id",
			connection);
		command.Parameters.AddWithValue("choice", choiceId);
		var parents = new List<string>();
		await using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			parents.Add(reader.GetString(0));
		}

		return [.. parents];
	}

	private async Task MigrateTo(string? targetMigration)
	{
		await using var context = WorkerDatabase.ContextFor(ConnectionString);
		await context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(targetMigration);
	}

	private async Task<NpgsqlConnection> Open()
	{
		var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync();
		return connection;
	}

	private async Task Execute(string sql)
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private async Task<string?> Scalar(string sql)
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(sql, connection);
		return (string?)await command.ExecuteScalarAsync();
	}
}
