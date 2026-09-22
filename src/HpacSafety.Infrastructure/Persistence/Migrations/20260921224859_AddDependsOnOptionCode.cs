using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddDependsOnOptionCode : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "depends_on_option_code",
				table: "question_revisions",
				type: "character varying(128)",
				maxLength: 128,
				nullable: true);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
				name: "depends_on_option_code",
				table: "question_revisions");
		}
	}
}
