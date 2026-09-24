using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class ReviewActionsRejectionNote : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// The report and summary concurrency tokens map to PostgreSQL's
			// xmin system column (ADR-0105). It already exists on every row, so
			// there is nothing to add; the model snapshot records the mapping.
			migrationBuilder.AddColumn<string>(
				name: "rejection_note",
				table: "reports",
				type: "character varying(2000)",
				maxLength: 2000,
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "rejection_note",
				table: "reports");
		}
	}
}
