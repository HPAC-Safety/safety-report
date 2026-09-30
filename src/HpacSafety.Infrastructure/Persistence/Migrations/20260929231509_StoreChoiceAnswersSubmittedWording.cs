using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class StoreChoiceAnswersSubmittedWording : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "submitted_wording",
				table: "report_answers",
				type: "text",
				nullable: true);

			migrationBuilder.Sql(SqlScript.Read("20260929231509_StoreChoiceAnswersSubmittedWording.sql"));

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_answers_submitted_wording_needs_choice",
				table: "report_answers",
				sql: "(choice_id IS NULL) = (submitted_wording IS NULL)");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_report_answers_submitted_wording_needs_choice",
				table: "report_answers");

			migrationBuilder.DropColumn(
				name: "submitted_wording",
				table: "report_answers");
		}
	}
}
