using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AllowFutureDatesOnDateQuestions : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<bool>(
				name: "allow_future_dates",
				table: "question_revisions",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddCheckConstraint(
				name: "ck_question_revisions_future_dates_date",
				table: "question_revisions",
				sql: "NOT allow_future_dates OR type = 'date'");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_question_revisions_future_dates_date",
				table: "question_revisions");

			migrationBuilder.DropColumn(
				name: "allow_future_dates",
				table: "question_revisions");
		}
	}
}
