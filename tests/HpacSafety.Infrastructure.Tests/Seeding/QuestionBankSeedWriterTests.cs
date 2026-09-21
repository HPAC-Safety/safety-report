using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
/// The guarded-insert SQL this writer builds, exercised against a synthetic
/// question rather than <see cref="QuestionBankSeed"/> — which seeds nothing
/// right now (see its remarks) and so cannot exercise this on its own.
/// </summary>
public sealed class QuestionBankSeedWriterTests
{
    private static readonly SeededQuestion Question = new(
        "sample_question",
        QuestionType.SingleSelect,
        QuestionRole.None,
        IsPrivate: true,
        IsRequired: false,
        IsSystem: false,
        "Sample question",
        "Question exemple",
        "Some help",
        "Une certaine aide",
        [new SeededOption("a", "Option A", "Option A (fr)")]);

    /// <summary>Not private, and with no help text — the other side of both
    /// ternaries <see cref="Question"/> alone leaves untouched.</summary>
    private static readonly SeededQuestion NonPrivateQuestionWithNoHelp = new(
        "another_question",
        QuestionType.ShortText,
        QuestionRole.None,
        IsPrivate: false,
        IsRequired: false,
        IsSystem: false,
        "Another question",
        "Une autre question",
        null,
        null,
        []);

    [Fact]
    public void GivenSeededQuestionWithAnOption_WhenSqlIsBuilt_ThenEveryRowGetsAGuardedInsert()
    {
        var sql = QuestionBankSeedWriter.Sql([Question]);

        sql.ShouldContain("INSERT INTO questions");
        sql.ShouldContain("is_private");
        sql.ShouldContain("INSERT INTO question_versions");
        sql.ShouldContain("INSERT INTO question_translations");
        sql.ShouldContain("INSERT INTO question_options");
        sql.ShouldContain("INSERT INTO question_option_translations");
        sql.ShouldContain("WHERE NOT EXISTS");
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
    public void GivenCurrentSeed_WhenWritten_ThenNoOperationIsScheduled()
    {
        // The current seed is empty (see QuestionBankSeed's remarks), and
        // MigrationBuilder.Sql refuses an empty string — Write must skip it
        // rather than pass one through.
        var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        QuestionBankSeedWriter.Write(migration);

        migration.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void GivenCurrentSeed_WhenWrittenAgainstLegacySensitivitySchema_ThenNoOperationIsScheduled()
    {
        var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        QuestionBankSeedWriter.WriteLegacySensitivitySchema(migration);

        migration.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void GivenSeededQuestions_WhenWrittenAgainstLegacySensitivitySchema_ThenUsesSensitivityNotPrivacyFlag()
    {
        var sql = QuestionBankSeedWriter.Sql([Question, NonPrivateQuestionWithNoHelp], legacySensitivitySchema: true);

        sql.ShouldContain("'restricted'");
        sql.ShouldContain("'publishable'");
        sql.ShouldNotContain("is_private");
    }

    [Fact]
    public void GivenQuestionWithNoHelpText_WhenSqlIsBuilt_ThenHelpColumnIsNull()
    {
        var sql = QuestionBankSeedWriter.Sql([NonPrivateQuestionWithNoHelp]);

        sql.ShouldContain("NULL");
    }
}
