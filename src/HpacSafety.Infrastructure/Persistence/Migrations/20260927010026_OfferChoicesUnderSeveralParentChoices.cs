using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     ADR-0151: a dependent question's choice is offered under one or more parent
	///     choices. The join table is created, every single link folded into it and
	///     verified, identical duplicates merged, and only then is
	///     <c>question_choices.parent_choice_id</c> dropped — all in this migration's
	///     one transaction, so a failed fold drops nothing.
	/// </summary>
	public partial class OfferChoicesUnderSeveralParentChoices : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "question_choice_parents",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					choice_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					parent_choice_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_question_choice_parents", x => x.id);
					table.CheckConstraint("ck_question_choice_parents_other", "parent_choice_id <> choice_id");
					table.ForeignKey(
						name: "fk_question_choice_parents_question_choices_choice_id",
						column: x => x.choice_id,
						principalTable: "question_choices",
						principalColumn: "id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "fk_question_choice_parents_question_choices_parent_choice_id",
						column: x => x.parent_choice_id,
						principalTable: "question_choices",
						principalColumn: "id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "ix_question_choice_parents_choice_id_parent_choice_id",
				table: "question_choice_parents",
				columns: new[] { "choice_id", "parent_choice_id" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_question_choice_parents_parent_choice_id",
				table: "question_choice_parents",
				column: "parent_choice_id");

			// Fold, verify, and merge before the column goes (ADR-0151).
			migrationBuilder.Sql(SqlScript.Read("20260927010026_OfferChoicesUnderSeveralParentChoices.sql"));

			migrationBuilder.DropForeignKey(
				name: "fk_question_choices_question_choices_parent_choice_id",
				table: "question_choices");

			migrationBuilder.DropIndex(
				name: "ix_question_choices_parent_choice_id",
				table: "question_choices");

			migrationBuilder.DropCheckConstraint(
				name: "ck_question_choices_parent_other",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "parent_choice_id",
				table: "question_choices");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "parent_choice_id",
				table: "question_choices",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			// Refuses a choice under several parent choices rather than lose links.
			migrationBuilder.Sql(SqlScript.Read("20260927010026_OfferChoicesUnderSeveralParentChoices.Down.sql"));

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

			migrationBuilder.DropTable(
				name: "question_choice_parents");
		}
	}
}
