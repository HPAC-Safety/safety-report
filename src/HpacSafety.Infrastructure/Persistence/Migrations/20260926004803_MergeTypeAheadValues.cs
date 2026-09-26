using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class MergeTypeAheadValues : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "merged_into_choice_id",
				table: "question_choices",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_question_choices_merged_into_choice_id",
				table: "question_choices",
				column: "merged_into_choice_id");

			migrationBuilder.AddCheckConstraint(
				name: "ck_question_choices_merged_is_removed",
				table: "question_choices",
				sql: "merged_into_choice_id IS NULL OR (deleted IS NOT NULL AND merged_into_choice_id <> id)");

			migrationBuilder.AddForeignKey(
				name: "fk_question_choices_question_choices_merged_into_choice_id",
				table: "question_choices",
				column: "merged_into_choice_id",
				principalTable: "question_choices",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "fk_question_choices_question_choices_merged_into_choice_id",
				table: "question_choices");

			migrationBuilder.DropIndex(
				name: "ix_question_choices_merged_into_choice_id",
				table: "question_choices");

			migrationBuilder.DropCheckConstraint(
				name: "ck_question_choices_merged_is_removed",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "merged_into_choice_id",
				table: "question_choices");
		}
	}
}
