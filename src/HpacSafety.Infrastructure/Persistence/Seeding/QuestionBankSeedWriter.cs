using System.Globalization;
using System.Text;

using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

using Microsoft.EntityFrameworkCore.Migrations;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>Writes the initial immutable question revisions.</summary>
public static class QuestionBankSeedWriter
{
    /// <summary>Writes every seeded row through a migration.</summary>
    public static void Write(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(Sql());
    }

    /// <summary>Returns deterministic guarded seed SQL.</summary>
    public static string Sql()
    {
        var sql = new StringBuilder();

        for (var order = 0; order < QuestionBankSeed.Questions.Count; order++)
        {
            var question = QuestionBankSeed.Questions[order];
            var questionId = SeedIds.For($"question:{question.Key}:1");
            var isConsent = question.Key == QuestionKey.ConsentPublish;

            AppendGuardedInsert(
                sql,
                "questions",
                [
                    "id", "key", "revision", "type", "label_en", "label_fr", "help_en", "help_fr",
                    "is_private", "sort_order", "is_active", "section_key", "supersedes_question_id",
                    "is_system", "is_required", "created_at",
                ],
                [
                    Id(questionId), Str(question.Key), Int(1), Str(EnumCode.Of(question.Type)),
                    Str(question.LabelEn), Str(question.LabelFr), StrOrNull(question.HelpEn), StrOrNull(question.HelpFr),
                    Bool(question.IsPrivate), Int(order), Bool(question.IsActive), StrOrNull(question.SectionKey), "NULL",
                    Bool(isConsent), Bool(isConsent), Timestamp(QuestionBankSeed.SeededAt),
                ],
                "id",
                Id(questionId));

            for (var optionOrder = 0; optionOrder < question.Options.Count; optionOrder++)
            {
                var option = question.Options[optionOrder];
                var optionId = SeedIds.For($"question_option:{question.Key}:1:{option.Code}");

                AppendGuardedInsert(
                    sql,
                    "question_options",
                    ["id", "question_id", "code", "label_en", "label_fr", "sort_order"],
                    [
                        Id(optionId), Id(questionId), Str(option.Code), Str(option.LabelEn),
                        Str(option.LabelFr), Int(optionOrder),
                    ],
                    "id",
                    Id(optionId));
            }
        }

        return sql.ToString();
    }

    private static void AppendGuardedInsert(
        StringBuilder sql,
        string table,
        string[] columns,
        string[] values,
        string guardColumn,
        string guardValue) =>
        sql.Append("INSERT INTO ").Append(table)
            .Append(" (").AppendJoin(", ", columns).Append(')')
            .Append(" SELECT ").AppendJoin(", ", values)
            .Append(" WHERE NOT EXISTS (SELECT 1 FROM ").Append(table)
            .Append(" WHERE ").Append(guardColumn).Append(" = ").Append(guardValue).Append(");")
            .Append('\n');

    private static string Id(TinyId id) => Str(id.Value);
    private static string Str(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    private static string StrOrNull(string? value) => value is null ? "NULL" : Str(value);
    private static string Bool(bool value) => value ? "TRUE" : "FALSE";
    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Timestamp(DateTimeOffset value) =>
        $"TIMESTAMPTZ '{value.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture)}'";
}
