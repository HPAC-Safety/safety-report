using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForkAnsweredQuestionsAndStringAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_report_answers_report_id_question_id",
                table: "report_answers");

            migrationBuilder.DropIndex(
                name: "ix_questions_key",
                table: "questions");

            // Dropped rather than retained empty. Product invariant #8 forbids
            // physically deleting application records; this column has never
            // held one in any deployed environment because no submission
            // endpoint exists to write it. ADR-0072 makes that argument on its
            // own facts, as ADR-0065 requires of any drop.
            migrationBuilder.DropColumn(
                name: "selected_option_codes",
                table: "report_answers");

            // No row exists to backfill: nothing writes report_answers outside
            // a test, because there is no submission endpoint yet — the same
            // fact that lets selected_option_codes be dropped at all
            // (ADR-0072). The default is a real locale rather than an empty
            // string so the column can never hold a code that will not parse.
            migrationBuilder.AddColumn<string>(
                name: "locale",
                table: "report_answers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "en-CA");

            migrationBuilder.AddColumn<bool>(
                name: "needs_translation",
                table: "report_answers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "translated_value",
                table: "report_answers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "needs_translation",
                table: "option_set_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_needs_translation",
                table: "report_answers",
                column: "needs_translation",
                filter: "needs_translation");

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_report_id_question_id",
                table: "report_answers",
                columns: new[] { "report_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_key",
                table: "questions",
                column: "key",
                unique: true,
                filter: "deleted IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_report_answers_needs_translation",
                table: "report_answers");

            migrationBuilder.DropIndex(
                name: "ix_report_answers_report_id_question_id",
                table: "report_answers");

            migrationBuilder.DropIndex(
                name: "ix_questions_key",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "locale",
                table: "report_answers");

            migrationBuilder.DropColumn(
                name: "needs_translation",
                table: "report_answers");

            migrationBuilder.DropColumn(
                name: "translated_value",
                table: "report_answers");

            migrationBuilder.DropColumn(
                name: "needs_translation",
                table: "option_set_items");

            migrationBuilder.AddColumn<string[]>(
                name: "selected_option_codes",
                table: "report_answers",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_report_id_question_id",
                table: "report_answers",
                columns: new[] { "report_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_key",
                table: "questions",
                column: "key",
                unique: true);
        }
    }
}
