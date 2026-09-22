using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddGroupedUnderQuestionId : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "grouped_under_question_id",
				table: "question_revisions",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_question_revisions_grouped_under_question_id",
				table: "question_revisions",
				column: "grouped_under_question_id");

			migrationBuilder.AddForeignKey(
				name: "fk_question_revisions_questions_grouped_under_question_id",
				table: "question_revisions",
				column: "grouped_under_question_id",
				principalTable: "questions",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "fk_question_revisions_questions_grouped_under_question_id",
				table: "question_revisions");

			migrationBuilder.DropIndex(
				name: "ix_question_revisions_grouped_under_question_id",
				table: "question_revisions");

			migrationBuilder.DropColumn(
				name: "grouped_under_question_id",
				table: "question_revisions");
		}
	}
}
