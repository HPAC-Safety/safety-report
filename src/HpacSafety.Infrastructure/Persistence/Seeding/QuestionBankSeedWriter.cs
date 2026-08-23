using System.Globalization;
using System.Text;

using HpacSafety.Core.SharedKernel;

using Microsoft.EntityFrameworkCore.Migrations;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
/// Writes <see cref="QuestionBankSeed"/> into a fresh database, so a clean
/// install asks exactly the question set HPAC has been collecting.
/// </summary>
/// <remarks>
/// <para>
/// The rows are written by the migration rather than declared with
/// <c>HasData</c>. <c>HasData</c> makes seed rows part of the model snapshot,
/// and the whole point of ADR-0016 is that an administrator edits these rows
/// after deployment — every one of those edits would then show up as a model
/// difference that the next migration tries to undo. See ADR-0020.
/// </para>
/// <para>
/// Identifiers are derived from the question key rather than drawn at random,
/// so the same migration produces the same rows on every database and in a
/// generated SQL script. See <see cref="SeedIds"/>.
/// </para>
/// <para>
/// Every row is written with an <c>INSERT ... SELECT ... WHERE NOT EXISTS</c>
/// guard on its own identifier, the same shape <see cref="DevelopmentAdminSeed"/>
/// uses for its one row — not EF's <c>InsertData</c>, which has no guard and
/// errors on a second write. That makes re-applying this seed a safe no-op:
/// against a database that only lost its <c>__EFMigrationsHistory</c> row, or
/// against one a future migration deliberately re-seeds because an
/// administrator emptied the question bank by hand. Deleting and re-inserting
/// was considered and rejected — a seeded question a report has already
/// answered is referenced by <c>report_answers</c> with
/// <c>DeleteBehavior.Restrict</c>, so a delete would fail once real answers
/// exist. See ADR-0020.
/// </para>
/// </remarks>
public static class QuestionBankSeedWriter
{
    /// <summary>Writes every seeded row through the migration.</summary>
    /// <param name="migrationBuilder">The migration being applied.</param>
    public static void Write(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(Sql());
    }

    /// <summary>
    /// The guarded SQL every row is written with. Exposed so a test can execute
    /// it a second time against an already-seeded database and prove that is a
    /// no-op, independent of the schema-creation half of the migration.
    /// </summary>
    public static string Sql()
    {
        var sql = new StringBuilder();
        var at = QuestionBankSeed.SeededAt;

        for (var order = 0; order < QuestionBankSeed.Questions.Count; order++)
        {
            var question = QuestionBankSeed.Questions[order];
            var questionId = SeedIds.For($"question:{question.Key}");
            var versionId = SeedIds.For($"question_version:{question.Key}:1");

            AppendGuardedInsert(
                sql,
                "questions",
                ["id", "key", "is_system", "role", "sensitivity", "display_order", "section_key", "is_active", "created_at", "deleted_at"],
                [Id(questionId), Str(question.Key), Bool(question.IsSystem), Str(EnumCode.Of(question.Role)), Str(EnumCode.Of(question.Sensitivity)), Int(order), StrOrNull(question.SectionKey), Bool(true), Timestamp(at), "NULL"],
                guardColumn: "id",
                guardValue: Id(questionId));

            AppendGuardedInsert(
                sql,
                "question_versions",
                ["id", "question_id", "version_number", "type", "is_required", "created_at"],
                [Id(versionId), Id(questionId), Int(1), Str(EnumCode.Of(question.Type)), Bool(question.IsRequired), Timestamp(at)],
                guardColumn: "id",
                guardValue: Id(versionId));

            AppendQuestionTranslation(sql, question, versionId, Locale.EnCa, question.LabelEn, question.HelpEn, isSource: true);
            AppendQuestionTranslation(sql, question, versionId, Locale.FrCa, question.LabelFr, question.HelpFr, isSource: false);

            for (var optionOrder = 0; optionOrder < question.Options.Count; optionOrder++)
            {
                var option = question.Options[optionOrder];
                var optionId = SeedIds.For($"question_option:{question.Key}:{option.Code}");

                AppendGuardedInsert(
                    sql,
                    "question_options",
                    ["id", "question_version_id", "code", "display_order"],
                    [Id(optionId), Id(versionId), Str(option.Code), Int(optionOrder)],
                    guardColumn: "id",
                    guardValue: Id(optionId));

                AppendOptionTranslation(sql, question, option, optionId, Locale.EnCa, option.LabelEn, isSource: true);
                AppendOptionTranslation(sql, question, option, optionId, Locale.FrCa, option.LabelFr, isSource: false);
            }
        }

        return sql.ToString();
    }

    private static void AppendQuestionTranslation(
        StringBuilder sql,
        SeededQuestion question,
        TinyId versionId,
        Locale locale,
        string label,
        string? helpText,
        bool isSource)
    {
        var at = QuestionBankSeed.SeededAt;
        var id = SeedIds.For($"question_translation:{question.Key}:1:{locale.Code}");

        AppendGuardedInsert(
            sql,
            "question_translations",
            ["id", "question_version_id", "locale", "label", "help_text", "placeholder", "is_source", "is_machine_translated", "translated_at", "updated_at"],
            [Id(id), Id(versionId), Str(locale.Code), Str(label), StrOrNull(helpText), "NULL", Bool(isSource), Bool(!isSource), isSource ? "NULL" : Timestamp(at), Timestamp(at)],
            guardColumn: "id",
            guardValue: Id(id));
    }

    private static void AppendOptionTranslation(
        StringBuilder sql,
        SeededQuestion question,
        SeededOption option,
        TinyId optionId,
        Locale locale,
        string label,
        bool isSource)
    {
        var at = QuestionBankSeed.SeededAt;
        var id = SeedIds.For($"question_option_translation:{question.Key}:{option.Code}:{locale.Code}");

        AppendGuardedInsert(
            sql,
            "question_option_translations",
            ["id", "question_option_id", "locale", "label", "is_source", "is_machine_translated", "translated_at", "updated_at"],
            [Id(id), Id(optionId), Str(locale.Code), Str(label), Bool(isSource), Bool(!isSource), isSource ? "NULL" : Timestamp(at), Timestamp(at)],
            guardColumn: "id",
            guardValue: Id(id));
    }

    /// <summary>
    /// <c>INSERT INTO table (columns) SELECT values WHERE NOT EXISTS (SELECT 1
    /// FROM table WHERE guardColumn = guardValue);</c> — one row, written once,
    /// however many times this statement runs.
    /// </summary>
    private static void AppendGuardedInsert(
        StringBuilder sql, string table, string[] columns, string[] values, string guardColumn, string guardValue)
    {
        sql.Append("INSERT INTO ").Append(table)
            .Append(" (").AppendJoin(", ", columns).Append(')')
            .Append(" SELECT ").AppendJoin(", ", values)
            .Append(" WHERE NOT EXISTS (SELECT 1 FROM ").Append(table)
            .Append(" WHERE ").Append(guardColumn).Append(" = ").Append(guardValue).Append(");")
            .Append('\n');
    }

    private static string Id(TinyId id) => Str(id.Value);

    private static string Str(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string StrOrNull(string? value) => value is null ? "NULL" : Str(value);

    private static string Bool(bool value) => value ? "TRUE" : "FALSE";

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Timestamp(DateTimeOffset value) =>
        $"TIMESTAMPTZ '{value.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture)}'";
}
