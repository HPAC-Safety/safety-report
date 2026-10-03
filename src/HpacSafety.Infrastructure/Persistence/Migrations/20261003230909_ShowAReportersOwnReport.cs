using System;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     A reporter's browser sees its own report before it is published (#820,
	///     ADR-0196). Adds <c>reports.receipt_hash</c> and <c>reports.first_published_at</c>,
	///     then the views and the trigger lock in its <c>.sql</c> file. No row is
	///     changed except the backfill of <c>first_published_at</c>.
	/// </summary>
	public partial class ShowAReportersOwnReport : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "first_published_at",
				table: "reports",
				type: "timestamp with time zone",
				nullable: true);

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

			migrationBuilder.Sql(SqlScript.Read("20261003230909_ShowAReportersOwnReport.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20261003230909_ShowAReportersOwnReport.Down.sql"));

			migrationBuilder.DropIndex(
				name: "ix_reports_receipt_hash",
				table: "reports");

			migrationBuilder.DropColumn(
				name: "first_published_at",
				table: "reports");

			migrationBuilder.DropColumn(
				name: "receipt_hash",
				table: "reports");
		}
	}
}
