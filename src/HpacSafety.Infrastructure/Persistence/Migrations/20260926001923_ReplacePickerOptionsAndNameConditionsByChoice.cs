using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class ReplacePickerOptionsAndNameConditionsByChoice : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "depends_on_choice_id",
				table: "question_revisions",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "replaced_by_choice_id",
				table: "question_choices",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			// Backfilled and checked before the code column goes (ADR-0128).
			migrationBuilder.Sql(SqlScript.Read("20260926001923_ReplacePickerOptionsAndNameConditionsByChoice.sql"));

			migrationBuilder.DropColumn(
				name: "depends_on_option_code",
				table: "question_revisions");

			migrationBuilder.CreateIndex(
				name: "ix_question_revisions_depends_on_choice_id",
				table: "question_revisions",
				column: "depends_on_choice_id");

			migrationBuilder.CreateIndex(
				name: "ix_question_choices_replaced_by_choice_id",
				table: "question_choices",
				column: "replaced_by_choice_id");

			migrationBuilder.AddForeignKey(
				name: "fk_question_choices_question_choices_replaced_by_choice_id",
				table: "question_choices",
				column: "replaced_by_choice_id",
				principalTable: "question_choices",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "fk_question_revisions_question_choices_depends_on_choice_id",
				table: "question_revisions",
				column: "depends_on_choice_id",
				principalTable: "question_choices",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "depends_on_option_code",
				table: "question_revisions",
				type: "character varying(128)",
				maxLength: 128,
				nullable: true);

			migrationBuilder.Sql(SqlScript.Read("20260926001923_ReplacePickerOptionsAndNameConditionsByChoice.Down.sql"));

			migrationBuilder.DropForeignKey(
				name: "fk_question_choices_question_choices_replaced_by_choice_id",
				table: "question_choices");

			migrationBuilder.DropForeignKey(
				name: "fk_question_revisions_question_choices_depends_on_choice_id",
				table: "question_revisions");

			migrationBuilder.DropIndex(
				name: "ix_question_revisions_depends_on_choice_id",
				table: "question_revisions");

			migrationBuilder.DropIndex(
				name: "ix_question_choices_replaced_by_choice_id",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "depends_on_choice_id",
				table: "question_revisions");

			migrationBuilder.DropColumn(
				name: "replaced_by_choice_id",
				table: "question_choices");
		}
	}
}
