using System;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Role assignments (#909, ADR-0199): creates <c>role_assignments</c>, then the
	///     trigger that refuses a delete, a truncate, and any update but one revoke, and
	///     the <c>active_role_assignments</c> view, from its <c>.sql</c> file. No row is changed.
	/// </summary>
	public partial class AddRoleAssignments : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "role_assignments",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
					role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
					granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					granted_by_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					revoked_by_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_role_assignments", x => x.id);
					table.CheckConstraint("ck_role_assignments_email", "email = lower(btrim(email)) AND email !~ '\\s' AND char_length(email) BETWEEN 3 AND 320 AND position('@' in email) > 1");
					table.CheckConstraint("ck_role_assignments_revoked_together", "(revoked_at IS NULL) = (revoked_by_subject IS NULL)");
					table.CheckConstraint("ck_role_assignments_role", "role IN ('safety_officer', 'administrator')");
				});

			migrationBuilder.CreateIndex(
				name: "ux_role_assignments_active_email",
				table: "role_assignments",
				column: "email",
				unique: true,
				filter: "revoked_at IS NULL");

			migrationBuilder.Sql(SqlScript.Read("20261017001446_AddRoleAssignments.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20261017001446_AddRoleAssignments.Down.sql"));

			migrationBuilder.DropTable(
				name: "role_assignments");
		}
	}
}
