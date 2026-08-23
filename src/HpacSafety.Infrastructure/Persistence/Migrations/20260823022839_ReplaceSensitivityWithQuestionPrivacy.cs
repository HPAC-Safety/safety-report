using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSensitivityWithQuestionPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_private",
                table: "report_answers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_private",
                table: "questions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "UPDATE report_answers SET is_private = (sensitivity <> 'publishable')");
            migrationBuilder.Sql(
                "UPDATE questions SET is_private = (sensitivity <> 'publishable')");

            migrationBuilder.DropColumn(
                name: "sensitivity",
                table: "report_answers");

            migrationBuilder.DropColumn(
                name: "sensitivity",
                table: "questions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sensitivity",
                table: "report_answers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "restricted");

            migrationBuilder.AddColumn<string>(
                name: "sensitivity",
                table: "questions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "restricted");

            migrationBuilder.Sql(
                "UPDATE report_answers SET sensitivity = CASE WHEN is_private THEN 'restricted' ELSE 'publishable' END");
            migrationBuilder.Sql(
                "UPDATE questions SET sensitivity = CASE WHEN is_private THEN 'restricted' ELSE 'publishable' END");

            migrationBuilder.DropColumn(
                name: "is_private",
                table: "report_answers");

            migrationBuilder.DropColumn(
                name: "is_private",
                table: "questions");
        }
    }
}
