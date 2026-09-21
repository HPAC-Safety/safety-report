using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStatementGroupSectionKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "section_key",
                table: "question_revisions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "section_key",
                table: "question_revisions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
