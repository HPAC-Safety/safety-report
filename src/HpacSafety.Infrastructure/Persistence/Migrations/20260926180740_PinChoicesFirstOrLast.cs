using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class PinChoicesFirstOrLast : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "pin",
				table: "question_choices",
				type: "character varying(16)",
				maxLength: 16,
				nullable: false,
				defaultValue: "none");

			migrationBuilder.AddCheckConstraint(
				name: "ck_question_choices_pin",
				table: "question_choices",
				sql: "pin IN ('none', 'first', 'last')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_question_choices_pin",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "pin",
				table: "question_choices");
		}
	}
}
