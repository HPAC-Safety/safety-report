using System;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     A published report's documents (ADR-0119): document consent on the report,
	///     the Worker's validation stamp on each document, the reworded consent_media
	///     question, and public_report_media extended to documents.
	/// </summary>
	public partial class OfferPublishedDocuments : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<bool>(
				name: "consent_documents",
				table: "reports",
				type: "boolean",
				nullable: true);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "validated_at",
				table: "report_files",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_files_validated_document",
				table: "report_files",
				sql: "validated_at IS NULL OR kind = 'document'");

			migrationBuilder.Sql(SqlScript.Read("20260924205238_OfferPublishedDocuments.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260924205238_OfferPublishedDocuments.Down.sql"));

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_files_validated_document",
				table: "report_files");

			migrationBuilder.DropColumn(
				name: "consent_documents",
				table: "reports");

			migrationBuilder.DropColumn(
				name: "validated_at",
				table: "report_files");
		}
	}
}
