using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class StoreYesOrNoAsABoolean : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers");

			migrationBuilder.AddColumn<bool>(
				name: "value_boolean",
				table: "report_answers",
				type: "boolean",
				nullable: true);

			// Converts every stored yes/no and checkbox word to a boolean, once, before
			// the constraints below forbid the word form (ADR-0130). Stops on any other
			// value, and the whole migration rolls back with it.
			migrationBuilder.Sql(SqlScript.Read("20260925223046_StoreYesOrNoAsABoolean.sql"));

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_boolean_has_no_words",
				table: "report_answers",
				sql: "value_boolean IS NULL OR (translated_value IS NULL AND translation_source IS NULL AND translation_mode = 'none')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_text_or_boolean",
				table: "report_answers",
				sql: "value IS NULL OR value_boolean IS NULL");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers",
				sql: "translation_mode IN ('none', 'choice', 'machine')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_boolean_has_no_words",
				table: "report_answers");

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_text_or_boolean",
				table: "report_answers");

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers");

			migrationBuilder.Sql(SqlScript.Read("20260925223046_StoreYesOrNoAsABoolean.Down.sql"));

			migrationBuilder.DropColumn(
				name: "value_boolean",
				table: "report_answers");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers",
				sql: "translation_mode IN ('none', 'choice', 'machine', 'fixed')");
		}
	}
}
