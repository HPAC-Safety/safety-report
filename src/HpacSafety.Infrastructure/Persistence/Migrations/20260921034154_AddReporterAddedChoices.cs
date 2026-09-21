using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReporterAddedChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "added_by_reporter",
                table: "option_set_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_option_set_items_option_set_id_added_by_reporter",
                table: "option_set_items",
                columns: new[] { "option_set_id", "added_by_reporter" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_option_set_items_option_set_id_added_by_reporter",
                table: "option_set_items");

            migrationBuilder.DropColumn(
                name: "added_by_reporter",
                table: "option_set_items");
        }
    }
}
