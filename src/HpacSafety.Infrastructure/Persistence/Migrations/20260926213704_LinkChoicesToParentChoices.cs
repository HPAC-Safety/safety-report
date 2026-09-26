using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class LinkChoicesToParentChoices : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "choices_depend_on_question_id",
				table: "questions",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "parent_choice_id",
				table: "question_choices",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_questions_choices_depend_on_question_id",
				table: "questions",
				column: "choices_depend_on_question_id");

			migrationBuilder.AddCheckConstraint(
				name: "ck_questions_choices_depend_on_other",
				table: "questions",
				sql: "choices_depend_on_question_id IS NULL OR choices_depend_on_question_id <> id");

			migrationBuilder.CreateIndex(
				name: "ix_question_choices_parent_choice_id",
				table: "question_choices",
				column: "parent_choice_id");

			migrationBuilder.AddCheckConstraint(
				name: "ck_question_choices_parent_other",
				table: "question_choices",
				sql: "parent_choice_id IS NULL OR parent_choice_id <> id");

			migrationBuilder.AddForeignKey(
				name: "fk_question_choices_question_choices_parent_choice_id",
				table: "question_choices",
				column: "parent_choice_id",
				principalTable: "question_choices",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "fk_questions_questions_choices_depend_on_question_id",
				table: "questions",
				column: "choices_depend_on_question_id",
				principalTable: "questions",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "fk_question_choices_question_choices_parent_choice_id",
				table: "question_choices");

			migrationBuilder.DropForeignKey(
				name: "fk_questions_questions_choices_depend_on_question_id",
				table: "questions");

			migrationBuilder.DropIndex(
				name: "ix_questions_choices_depend_on_question_id",
				table: "questions");

			migrationBuilder.DropCheckConstraint(
				name: "ck_questions_choices_depend_on_other",
				table: "questions");

			migrationBuilder.DropIndex(
				name: "ix_question_choices_parent_choice_id",
				table: "question_choices");

			migrationBuilder.DropCheckConstraint(
				name: "ck_question_choices_parent_other",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "choices_depend_on_question_id",
				table: "questions");

			migrationBuilder.DropColumn(
				name: "parent_choice_id",
				table: "question_choices");
		}
	}
}
