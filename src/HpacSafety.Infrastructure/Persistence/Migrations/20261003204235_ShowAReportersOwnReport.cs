using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     A reporter's browser sees its own report before it is published (#820,
	///     ADR-0196). Adds <c>reports.receipt_hash</c>, then the views and the
	///     trigger lock in its <c>.sql</c> file. No row is changed.
	/// </summary>
	public partial class ShowAReportersOwnReport : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "receipt_hash",
				table: "reports",
				type: "character varying(43)",
				maxLength: 43,
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "ix_reports_receipt_hash",
				table: "reports",
				column: "receipt_hash",
				unique: true);

			migrationBuilder.Sql(SqlScript.Read("20261003204235_ShowAReportersOwnReport.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20261003204235_ShowAReportersOwnReport.Down.sql"));

			migrationBuilder.DropIndex(
				name: "ix_reports_receipt_hash",
				table: "reports");

			migrationBuilder.DropColumn(
				name: "receipt_hash",
				table: "reports");
		}
	}
}
