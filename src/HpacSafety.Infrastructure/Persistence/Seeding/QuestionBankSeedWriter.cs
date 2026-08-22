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
/// </remarks>
public static class QuestionBankSeedWriter
{
    private static readonly string[] QuestionColumns =
        ["id", "key", "is_system", "role", "sensitivity", "display_order", "section_key", "is_active", "created_at", "deleted_at"];

    private static readonly string[] VersionColumns =
        ["id", "question_id", "version_number", "type", "is_required", "created_at"];

    private static readonly string[] TranslationColumns =
        ["id", "question_version_id", "locale", "label", "help_text", "placeholder", "is_source", "is_machine_translated", "translated_at", "updated_at"];

    private static readonly string[] OptionColumns =
        ["id", "question_version_id", "code", "display_order"];

    private static readonly string[] OptionTranslationColumns =
        ["id", "question_option_id", "locale", "label", "is_source", "is_machine_translated", "translated_at", "updated_at"];

    /// <summary>Writes every seeded row through the migration.</summary>
    /// <param name="migrationBuilder">The migration being applied.</param>
    public static void Write(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        var at = QuestionBankSeed.SeededAt;

        for (var order = 0; order < QuestionBankSeed.Questions.Count; order++)
        {
            var question = QuestionBankSeed.Questions[order];
            var questionId = SeedIds.For($"question:{question.Key}");
            var versionId = SeedIds.For($"question_version:{question.Key}:1");

            migrationBuilder.InsertData(
                table: "questions",
                columns: QuestionColumns,
                values: [questionId, question.Key, question.IsSystem, EnumCode.Of(question.Role), EnumCode.Of(question.Sensitivity), order, question.SectionKey, true, at, null]);

            migrationBuilder.InsertData(
                table: "question_versions",
                columns: VersionColumns,
                values: [versionId, questionId, 1, EnumCode.Of(question.Type), question.IsRequired, at]);

            WriteQuestionTranslation(migrationBuilder, question, versionId, Locale.EnCa, question.LabelEn, question.HelpEn, isSource: true);
            WriteQuestionTranslation(migrationBuilder, question, versionId, Locale.FrCa, question.LabelFr, question.HelpFr, isSource: false);

            for (var optionOrder = 0; optionOrder < question.Options.Count; optionOrder++)
            {
                var option = question.Options[optionOrder];
                var optionId = SeedIds.For($"question_option:{question.Key}:{option.Code}");

                migrationBuilder.InsertData(
                    table: "question_options",
                    columns: OptionColumns,
                    values: [optionId, versionId, option.Code, optionOrder]);

                WriteOptionTranslation(migrationBuilder, question, option, optionId, Locale.EnCa, option.LabelEn, isSource: true);
                WriteOptionTranslation(migrationBuilder, question, option, optionId, Locale.FrCa, option.LabelFr, isSource: false);
            }
        }
    }

    private static void WriteQuestionTranslation(
        MigrationBuilder migrationBuilder,
        SeededQuestion question,
        Guid versionId,
        Locale locale,
        string label,
        string? helpText,
        bool isSource)
    {
        var at = QuestionBankSeed.SeededAt;

        migrationBuilder.InsertData(
            table: "question_translations",
            columns: TranslationColumns,
            values:
            [
                SeedIds.For($"question_translation:{question.Key}:1:{locale.Code}"),
                versionId,
                locale.Code,
                label,
                helpText,
                null,
                isSource,
                !isSource,
                isSource ? null : at,
                at,
            ]);
    }

    private static void WriteOptionTranslation(
        MigrationBuilder migrationBuilder,
        SeededQuestion question,
        SeededOption option,
        Guid optionId,
        Locale locale,
        string label,
        bool isSource)
    {
        var at = QuestionBankSeed.SeededAt;

        migrationBuilder.InsertData(
            table: "question_option_translations",
            columns: OptionTranslationColumns,
            values:
            [
                SeedIds.For($"question_option_translation:{question.Key}:{option.Code}:{locale.Code}"),
                optionId,
                locale.Code,
                label,
                isSource,
                !isSource,
                isSource ? null : at,
                at,
            ]);
    }
}
