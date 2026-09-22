using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class WorkerTranslatedAnswers : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_report_answers_needs_translation",
				table: "report_answers");

			migrationBuilder.DropColumn(
				name: "needs_translation",
				table: "report_answers");

			migrationBuilder.AddColumn<string>(
				name: "translation_source",
				table: "report_answers",
				type: "character varying(64)",
				maxLength: 64,
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_report_answers_answered_at",
				table: "report_answers",
				column: "answered_at",
				filter: "value IS NOT NULL AND translated_value IS NULL");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_report_answers_answered_at",
				table: "report_answers");

			migrationBuilder.DropColumn(
				name: "translation_source",
				table: "report_answers");

			migrationBuilder.AddColumn<bool>(
				name: "needs_translation",
				table: "report_answers",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.CreateIndex(
				name: "ix_report_answers_needs_translation",
				table: "report_answers",
				column: "needs_translation",
				filter: "needs_translation");
		}
	}
}
