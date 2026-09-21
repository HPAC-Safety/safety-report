using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_question_revisions_type",
                table: "question_revisions");

            migrationBuilder.AddColumn<string>(
                name: "depends_on_question_id",
                table: "question_revisions",
                type: "char(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "option_set_id",
                table: "question_revisions",
                type: "char(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_item_id",
                table: "question_revision_options",
                type: "char(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "option_sets",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_fr = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    option_set_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    label_en = table.Column<string>(type: "text", nullable: false),
                    label_fr = table.Column<string>(type: "text", nullable: false),
                    deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "ix_question_revisions_depends_on_question_id",
                table: "question_revisions",
                column: "depends_on_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_question_revisions_option_set_id",
                table: "question_revisions",
                column: "option_set_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_question_revisions_type",
                table: "question_revisions",
                sql: "type IN ('short_text', 'long_text', 'email', 'phone', 'date', 'number', 'single_select', 'multi_select', 'yes_no', 'checkbox', 'file_upload', 'statement', 'group', 'time', 'autocomplete')");

            migrationBuilder.CreateIndex(
                name: "ix_question_revision_options_source_item_id",
                table: "question_revision_options",
                column: "source_item_id");

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

            migrationBuilder.AddForeignKey(
                name: "fk_question_revision_options_option_set_items_source_item_id",
                table: "question_revision_options",
                column: "source_item_id",
                principalTable: "option_set_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_question_revisions_option_sets_option_set_id",
                table: "question_revisions",
                column: "option_set_id",
                principalTable: "option_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_question_revisions_questions_depends_on_question_id",
                table: "question_revisions",
                column: "depends_on_question_id",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_question_revision_options_option_set_items_source_item_id",
                table: "question_revision_options");

            migrationBuilder.DropForeignKey(
                name: "fk_question_revisions_option_sets_option_set_id",
                table: "question_revisions");

            migrationBuilder.DropForeignKey(
                name: "fk_question_revisions_questions_depends_on_question_id",
                table: "question_revisions");

            migrationBuilder.DropTable(
                name: "option_set_items");

            migrationBuilder.DropTable(
                name: "option_sets");

            migrationBuilder.DropIndex(
                name: "ix_question_revisions_depends_on_question_id",
                table: "question_revisions");

            migrationBuilder.DropIndex(
                name: "ix_question_revisions_option_set_id",
                table: "question_revisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_question_revisions_type",
                table: "question_revisions");

            migrationBuilder.DropIndex(
                name: "ix_question_revision_options_source_item_id",
                table: "question_revision_options");

            migrationBuilder.DropColumn(
                name: "depends_on_question_id",
                table: "question_revisions");

            migrationBuilder.DropColumn(
                name: "option_set_id",
                table: "question_revisions");

            migrationBuilder.DropColumn(
                name: "source_item_id",
                table: "question_revision_options");

            migrationBuilder.AddCheckConstraint(
                name: "ck_question_revisions_type",
                table: "question_revisions",
                sql: "type IN ('short_text', 'long_text', 'email', 'phone', 'date', 'number', 'single_select', 'multi_select', 'yes_no', 'checkbox', 'file_upload', 'statement', 'group')");
        }
    }
}
