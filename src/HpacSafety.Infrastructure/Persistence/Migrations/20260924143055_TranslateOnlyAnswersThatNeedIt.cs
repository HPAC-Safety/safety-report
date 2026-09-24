using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class TranslateOnlyAnswersThatNeedIt : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_report_answers_answered_at",
				table: "report_answers");

			migrationBuilder.AddColumn<string>(
				name: "translation_mode",
				table: "report_answers",
				type: "character varying(64)",
				maxLength: 64,
				nullable: false,
				defaultValue: "none");

			migrationBuilder.AddColumn<bool>(
				name: "is_translatable",
				table: "question_revisions",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.Sql(SqlScript.Read("20260924143055_TranslateOnlyAnswersThatNeedIt.sql"));

			migrationBuilder.CreateIndex(
				name: "ix_report_answers_answered_at",
				table: "report_answers",
				column: "answered_at",
				filter: "value IS NOT NULL AND translated_value IS NULL AND translation_mode = 'machine'");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers",
				sql: "translation_mode IN ('none', 'choice', 'machine')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_question_revisions_translatable_text",
				table: "question_revisions",
				sql: "NOT is_translatable OR type IN ('short_text', 'long_text')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_report_answers_answered_at",
				table: "report_answers");

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers");

			migrationBuilder.DropCheckConstraint(
				name: "ck_question_revisions_translatable_text",
				table: "question_revisions");

			migrationBuilder.DropColumn(
				name: "translation_mode",
				table: "report_answers");

			migrationBuilder.DropColumn(
				name: "is_translatable",
				table: "question_revisions");

			migrationBuilder.CreateIndex(
				name: "ix_report_answers_answered_at",
				table: "report_answers",
				column: "answered_at",
				filter: "value IS NOT NULL AND translated_value IS NULL");
		}
	}
}
