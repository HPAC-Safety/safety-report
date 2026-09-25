using System;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class ReviewTypeAheadValues : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "created_at",
				table: "question_choices",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "needs_review",
				table: "question_choices",
				type: "boolean",
				nullable: false,
				defaultValue: false);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "reviewed_at",
				table: "question_choices",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "reviewed_by",
				table: "question_choices",
				type: "character varying(256)",
				maxLength: 256,
				nullable: true);

			migrationBuilder.Sql(SqlScript.Read("20260925235946_ReviewTypeAheadValues.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260925235946_ReviewTypeAheadValues.Down.sql"));

			migrationBuilder.DropColumn(
				name: "created_at",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "needs_review",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "reviewed_at",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "reviewed_by",
				table: "question_choices");
		}
	}
}
