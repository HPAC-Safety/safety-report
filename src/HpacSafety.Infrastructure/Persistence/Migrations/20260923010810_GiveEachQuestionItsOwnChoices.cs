using System;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class GiveEachQuestionItsOwnChoices : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "question_choices",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					question_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					display_order = table.Column<int>(type: "integer", nullable: false),
					label_en = table.Column<string>(type: "text", nullable: true),
					label_fr = table.Column<string>(type: "text", nullable: true),
					added_by_reporter = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
					reporter_locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_question_choices", x => x.id);
					table.CheckConstraint("ck_question_choices_label", "label_en IS NOT NULL AND label_fr IS NOT NULL OR added_by_reporter AND (label_en IS NOT NULL OR label_fr IS NOT NULL)");
					table.ForeignKey(
						name: "fk_question_choices_questions_question_id",
						column: x => x.question_id,
						principalTable: "questions",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "ix_question_choices_question_id_code",
				table: "question_choices",
				columns: new[] { "question_id", "code" },
				unique: true);

			// Every choice moves onto its question before anything it came from
			// is dropped. ADR-0095 argues the drop under AGENTS.md invariant 8.
			migrationBuilder.Sql(SqlScript.Read("20260923010810_CopyChoicesOntoQuestions.sql"));

			migrationBuilder.DropForeignKey(
				name: "fk_question_revisions_option_sets_option_set_id",
				table: "question_revisions");

			migrationBuilder.DropTable(
				name: "question_revision_options");

			migrationBuilder.DropTable(
				name: "option_set_items");

			migrationBuilder.DropTable(
				name: "option_sets");

			migrationBuilder.DropIndex(
				name: "ix_question_revisions_option_set_id",
				table: "question_revisions");

			migrationBuilder.DropColumn(
				name: "allows_reporter_additions",
				table: "question_revisions");

			migrationBuilder.DropColumn(
				name: "option_set_id",
				table: "question_revisions");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Restores the shape, not the data: the choices stay on their
			// questions' own rows only, and a revision's options are not rebuilt.
			migrationBuilder.DropTable(
				name: "question_choices");

			migrationBuilder.AddColumn<bool>(
				name: "allows_reporter_additions",
				table: "question_revisions",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<string>(
				name: "option_set_id",
				table: "question_revisions",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			migrationBuilder.CreateTable(
				name: "option_sets",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					name_en = table.Column<string>(type: "text", nullable: false),
					name_fr = table.Column<string>(type: "text", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_option_sets", x => x.id);
				});

			migrationBuilder.CreateTable(
				name: "option_set_items",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					added_by_reporter = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
					code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					display_order = table.Column<int>(type: "integer", nullable: false),
					label_en = table.Column<string>(type: "text", nullable: false),
					label_fr = table.Column<string>(type: "text", nullable: false),
					needs_translation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
					option_set_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					reporter_locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_option_set_items", x => x.id);
					table.ForeignKey(
						name: "fk_option_set_items_option_sets_option_set_id",
						column: x => x.option_set_id,
						principalTable: "option_sets",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "question_revision_options",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					display_order = table.Column<int>(type: "integer", nullable: false),
					label_en = table.Column<string>(type: "text", nullable: false),
					label_fr = table.Column<string>(type: "text", nullable: false),
					question_revision_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					source_item_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_question_revision_options", x => x.id);
					table.ForeignKey(
						name: "fk_question_revision_options_option_set_items_source_item_id",
						column: x => x.source_item_id,
						principalTable: "option_set_items",
						principalColumn: "id",
						onDelete: ReferentialAction.Restrict);
					table.ForeignKey(
						name: "fk_question_revision_options_question_revisions_question_revis~",
						column: x => x.question_revision_id,
						principalTable: "question_revisions",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "ix_question_revisions_option_set_id",
				table: "question_revisions",
				column: "option_set_id");

			migrationBuilder.CreateIndex(
				name: "ix_option_set_items_option_set_id_added_by_reporter",
				table: "option_set_items",
				columns: new[] { "option_set_id", "added_by_reporter" });

			migrationBuilder.CreateIndex(
				name: "ix_option_set_items_option_set_id_code",
				table: "option_set_items",
				columns: new[] { "option_set_id", "code" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_option_sets_key",
				table: "option_sets",
				column: "key",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_question_revision_options_question_revision_id_code",
				table: "question_revision_options",
				columns: new[] { "question_revision_id", "code" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_question_revision_options_source_item_id",
				table: "question_revision_options",
				column: "source_item_id");

			migrationBuilder.AddForeignKey(
				name: "fk_question_revisions_option_sets_option_set_id",
				table: "question_revisions",
				column: "option_set_id",
				principalTable: "option_sets",
				principalColumn: "id",
				onDelete: ReferentialAction.SetNull);
		}
	}
}
