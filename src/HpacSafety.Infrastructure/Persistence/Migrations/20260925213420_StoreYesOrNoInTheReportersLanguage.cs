using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class StoreYesOrNoInTheReportersLanguage : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers",
				sql: "translation_mode IN ('none', 'choice', 'machine', 'fixed')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers");

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_translation_mode",
				table: "report_answers",
				sql: "translation_mode IN ('none', 'choice', 'machine')");
		}
	}
}
