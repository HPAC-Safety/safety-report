using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddPendingImportLogic : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "pending_import_logic",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					import_batch_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					field_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					field_title = table.Column<string>(type: "text", nullable: false),
					raw_logic_json = table.Column<string>(type: "text", nullable: false),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_pending_import_logic", x => x.id);
				});

			migrationBuilder.CreateIndex(
				name: "ix_pending_import_logic_import_batch_id",
				table: "pending_import_logic",
				column: "import_batch_id");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "pending_import_logic");
		}
	}
}
