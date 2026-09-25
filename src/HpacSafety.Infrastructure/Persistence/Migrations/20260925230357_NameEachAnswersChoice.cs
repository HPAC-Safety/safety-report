using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NameEachAnswersChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_report_answers_answered_at",
                table: "report_answers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_question_choices_label",
                table: "question_choices");

            migrationBuilder.AddColumn<string>(
                name: "choice_id",
                table: "report_answers",
                type: "char(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_answered_at",
                table: "report_answers",
                column: "answered_at",
                filter: "value IS NOT NULL AND translated_value IS NULL AND translation_mode = 'machine' AND choice_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_choice_id",
                table: "report_answers",
                column: "choice_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_question_choices_label",
                table: "question_choices",
                sql: "label_en IS NOT NULL AND label_fr IS NOT NULL OR (added_by_reporter OR deleted IS NOT NULL) AND (label_en IS NOT NULL OR label_fr IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_report_answers_question_choices_choice_id",
                table: "report_answers",
                column: "choice_id",
                principalTable: "question_choices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(SqlScript.Read("20260925230357_NameEachAnswersChoice.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScript.Read("20260925230357_NameEachAnswersChoice.Down.sql"));

            migrationBuilder.DropForeignKey(
                name: "fk_report_answers_question_choices_choice_id",
                table: "report_answers");

            migrationBuilder.DropIndex(
                name: "ix_report_answers_answered_at",
                table: "report_answers");

            migrationBuilder.DropIndex(
                name: "ix_report_answers_choice_id",
                table: "report_answers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_question_choices_label",
                table: "question_choices");

            migrationBuilder.DropColumn(
                name: "choice_id",
                table: "report_answers");

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_answered_at",
                table: "report_answers",
                column: "answered_at",
                filter: "value IS NOT NULL AND translated_value IS NULL AND translation_mode = 'machine'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_question_choices_label",
                table: "question_choices",
                sql: "label_en IS NOT NULL AND label_fr IS NOT NULL OR added_by_reporter AND (label_en IS NOT NULL OR label_fr IS NOT NULL)");
        }
    }
}
