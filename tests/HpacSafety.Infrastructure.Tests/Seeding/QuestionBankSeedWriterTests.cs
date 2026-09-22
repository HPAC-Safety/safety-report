using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
///     The guarded-insert SQL this writer builds, exercised against synthetic
///     questions rather than <see cref="QuestionBankSeed" /> — which seeds nothing
///     right now (see its remarks) and so cannot exercise this on its own.
/// </summary>
public sealed class QuestionBankSeedWriterTests
{
	private static readonly SeededQuestion GroupParent = new(
		"aircraft",
		QuestionType.Group,
		QuestionRole.None,
		false,
		false,
		false,
		"Aircraft",
		"Aéronef",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		false,
		[]);

	private static readonly SeededQuestion Question = new(
		"sample_question",
		QuestionType.SingleSelect,
		QuestionRole.None,
		true,
		false,
		false,
		"Sample question",
		"Question exemple",
		"Some help",
		"Une certaine aide",
		"e.g. Alberta",
		"p. ex. Alberta",
		"aircraft",
		null,
		"aircraft",
		false,
		[new SeededOption("a", "Option A", "Option A (fr)")]);

	/// <summary>
	///     Not private, and with no help text or dependency — the other side of
	///     every ternary <see cref="Question" /> alone leaves untouched.
	/// </summary>
	private static readonly SeededQuestion NonPrivateQuestionWithNoHelp = new(
		"another_question",
		QuestionType.ShortText,
		QuestionRole.None,
		false,
		false,
		false,
		"Another question",
		"Une autre question",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		false,
		[]);

	[Fact]
	public void GivenSeededQuestionsWithADependencyGroupingAndAnOption_WhenSqlIsBuilt_ThenEveryRowGetsAGuardedInsert()
	{
		var sql = QuestionBankSeedWriter.Sql([GroupParent, Question]);

		sql.ShouldContain("INSERT INTO questions");
		sql.ShouldContain("INSERT INTO question_revisions");
		sql.ShouldContain("INSERT INTO question_revision_options");
		sql.ShouldContain("is_private");
		sql.ShouldContain("grouped_under_question_id");
		sql.ShouldContain("depends_on_question_id");
		sql.ShouldContain("WHERE NOT EXISTS");
		sql.ShouldNotContain("question_versions");
		sql.ShouldNotContain("question_translations");
		sql.ShouldNotContain("question_option_translations");
	}

	[Fact]
	public void GivenNoQuestions_WhenSqlIsBuilt_ThenEmpty()
	{
		QuestionBankSeedWriter.Sql([]).ShouldBeEmpty();
	}

	[Fact]
	public void GivenNonEmptyQuestions_WhenWritten_ThenOneSqlOperationIsScheduled()
	{
		var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

		QuestionBankSeedWriter.Write(migration, [Question]);

		var operation = migration.Operations.ShouldHaveSingleItem().ShouldBeOfType<SqlOperation>();
		operation.Sql.ShouldContain("INSERT INTO questions");
	}

	[Fact]
	public void GivenAnEmptyQuestionList_WhenWritten_ThenNoOperationIsScheduled()
	{
		// MigrationBuilder.Sql refuses an empty string — Write must skip it
		// rather than pass one through.
		var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

		QuestionBankSeedWriter.Write(migration, []);

		migration.Operations.ShouldBeEmpty();
	}

	[Fact]
	public void GivenCurrentSeed_WhenWritten_ThenOneSqlOperationIsScheduled()
	{
		// The current seed now seeds the real form (see QuestionBankSeed's
		// remarks) — this exercises Write(migrationBuilder) against whatever
		// it currently holds, the same call the seed migration makes.
		var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

		QuestionBankSeedWriter.Write(migration);

		var operation = migration.Operations.ShouldHaveSingleItem().ShouldBeOfType<SqlOperation>();
		operation.Sql.ShouldContain("INSERT INTO questions");
	}

	[Fact]
	public void GivenCurrentSeed_WhenWrittenAgainstLegacySensitivitySchema_ThenOneSqlOperationIsScheduled()
	{
		// Only the already-shipped InitialSchema migration calls this, but it
		// still reads QuestionBankSeed.Questions at Up()-execution time — a
		// fresh database seeds the real form through this path too, then
		// MigrateCanonicalDomainAndPersistence carries it forward.
		var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

		QuestionBankSeedWriter.WriteLegacySensitivitySchema(migration);

		var operation = migration.Operations.ShouldHaveSingleItem().ShouldBeOfType<SqlOperation>();
		operation.Sql.ShouldContain("INSERT INTO questions");
	}

	[Fact]
	public void GivenQuestionWithNoHelpTextOrDependency_WhenSqlIsBuilt_ThenThoseColumnsAreNull()
	{
		var sql = QuestionBankSeedWriter.Sql([NonPrivateQuestionWithNoHelp]);

		sql.ShouldContain("NULL");
	}

	[Fact]
	public void GivenADependency_WhenSqlIsBuilt_ThenTheParentsSeedIdIsUsed()
	{
		var sql = QuestionBankSeedWriter.Sql([GroupParent, Question]);
		var parentId = SeedIds.For("question:aircraft");

		sql.ShouldContain($"'{parentId.Value}'");
	}

	[Fact]
	public void GivenSeededQuestionWithAnOption_WhenLegacySqlIsBuilt_ThenTheOriginalSchemaShapeIsUsed()
	{
		var sql = QuestionBankSeedWriter.LegacySql([Question, NonPrivateQuestionWithNoHelp]);

		sql.ShouldContain("INSERT INTO questions");
		sql.ShouldContain("'restricted'");
		sql.ShouldContain("'publishable'");
		sql.ShouldContain("INSERT INTO question_versions");
		sql.ShouldContain("INSERT INTO question_translations");
		sql.ShouldContain("INSERT INTO question_options");
		sql.ShouldContain("INSERT INTO question_option_translations");
		sql.ShouldNotContain("is_private");
		sql.ShouldNotContain("question_revisions");
	}

	[Fact]
	public void GivenNoQuestions_WhenLegacySqlIsBuilt_ThenEmpty()
	{
		QuestionBankSeedWriter.LegacySql([]).ShouldBeEmpty();
	}
}
