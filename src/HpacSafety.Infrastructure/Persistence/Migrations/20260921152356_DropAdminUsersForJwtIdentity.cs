using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	/// Drops <c>admin_users</c> and turns the two columns that referenced it
	/// into opaque token subjects. See ADR-0065.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This was hand-ordered after scaffolding.</b> EF generated
	/// <c>DropColumn</c> + <c>AddColumn</c> for both identity columns, which
	/// would have discarded every existing value. `audit_log` is append-only
	/// history and its attribution is the only thing those rows carry, so the
	/// columns are <b>renamed and widened</b> instead: <c>char(11)</c> to
	/// <c>varchar(256)</c> is a widening cast, so an existing tiny id survives
	/// verbatim as a string. It simply no longer resolves to anything, which is
	/// accepted and recorded in <c>docs/data-and-persistence.md</c> rather than
	/// rewritten — editing audit rows is a destructive transform.
	/// </para>
	/// <para>
	/// The table is dropped <b>last</b>, after both foreign keys referencing it
	/// are gone. Dropping it at all is the one carved exception to "nothing is
	/// physically deleted": it never held application data in any deployed
	/// environment, because the only row any database ever received is the
	/// synthetic <c>admin@localhost</c> guarded by a setting production never
	/// sets. On a fresh database <c>InitialSchema</c> still creates the table
	/// and still runs that guarded insert; this migration drops both moments
	/// later. Both paths end at the same schema.
	/// </para>
	/// </remarks>
	public partial class DropAdminUsersForJwtIdentity : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			ArgumentNullException.ThrowIfNull(migrationBuilder);

			// 1. Release both references to admin_users before anything else.
			migrationBuilder.DropForeignKey(
				name: "fk_audit_log_admin_users_admin_user_id",
				table: "audit_log");

			migrationBuilder.DropForeignKey(
				name: "fk_summaries_admin_users_approved_by",
				table: "summaries");

			// 2. The coherence check names the column being renamed, so it has
			//    to go before the rename and come back after it.
			migrationBuilder.DropCheckConstraint(
				name: "ck_summaries_approval_coherence",
				table: "summaries");

			migrationBuilder.DropIndex(
				name: "ix_summaries_approved_by",
				table: "summaries");

			migrationBuilder.DropIndex(
				name: "ix_audit_log_admin_user_id",
				table: "audit_log");

			// 3. Rename, then widen. Never drop and re-add: these columns hold
			//    the only attribution an audit row has.
			migrationBuilder.RenameColumn(
				name: "approved_by",
				table: "summaries",
				newName: "approved_by_subject");

			migrationBuilder.RenameColumn(
				name: "admin_user_id",
				table: "audit_log",
				newName: "actor_subject");

			migrationBuilder.AlterColumn<string>(
				name: "approved_by_subject",
				table: "summaries",
				type: "character varying(256)",
				maxLength: 256,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "char(11)",
				oldFixedLength: true,
				oldMaxLength: 11,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "actor_subject",
				table: "audit_log",
				type: "character varying(256)",
				maxLength: 256,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "char(11)",
				oldFixedLength: true,
				oldMaxLength: 11,
				oldNullable: false);

			// 4. Put the constraint and the lookup index back on the new names.
			migrationBuilder.AddCheckConstraint(
				name: "ck_summaries_approval_coherence",
				table: "summaries",
				sql: "(approved_by_subject IS NULL) = (approved_at IS NULL)");

			migrationBuilder.CreateIndex(
				name: "ix_audit_log_actor_subject",
				table: "audit_log",
				column: "actor_subject");

			// 5. Nothing references it now.
			migrationBuilder.DropTable(
				name: "admin_users");
		}

		/// <inheritdoc />
		/// <remarks>
		/// Recreates the table's shape, but not its rows — there were none worth
		/// keeping. The two identity columns narrow back to <c>char(11)</c>,
		/// which truncates any subject longer than eleven characters, so this is
		/// only safe to run before real tokens have written to them.
		/// </remarks>
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			ArgumentNullException.ThrowIfNull(migrationBuilder);

			migrationBuilder.CreateTable(
				name: "admin_users",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					is_active = table.Column<bool>(type: "boolean", nullable: false),
					member_identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_admin_users", x => x.id);
					table.CheckConstraint("ck_admin_users_role", "role IN ('safety_officer', 'administrator')");
				});

			migrationBuilder.CreateIndex(
				name: "ix_admin_users_member_identifier",
				table: "admin_users",
				column: "member_identifier",
				unique: true,
				filter: "deleted IS NULL");

			migrationBuilder.DropCheckConstraint(
				name: "ck_summaries_approval_coherence",
				table: "summaries");

			migrationBuilder.DropIndex(
				name: "ix_audit_log_actor_subject",
				table: "audit_log");

			migrationBuilder.AlterColumn<string>(
				name: "approved_by_subject",
				table: "summaries",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(256)",
				oldMaxLength: 256,
				oldNullable: true);

			migrationBuilder.AlterColumn<string>(
				name: "actor_subject",
				table: "audit_log",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(256)",
				oldMaxLength: 256,
				oldNullable: false);

			migrationBuilder.RenameColumn(
				name: "approved_by_subject",
				table: "summaries",
				newName: "approved_by");

			migrationBuilder.RenameColumn(
				name: "actor_subject",
				table: "audit_log",
				newName: "admin_user_id");

			migrationBuilder.AddCheckConstraint(
				name: "ck_summaries_approval_coherence",
				table: "summaries",
				sql: "(approved_by IS NULL) = (approved_at IS NULL)");

			migrationBuilder.CreateIndex(
				name: "ix_summaries_approved_by",
				table: "summaries",
				column: "approved_by");

			migrationBuilder.CreateIndex(
				name: "ix_audit_log_admin_user_id",
				table: "audit_log",
				column: "admin_user_id");

			migrationBuilder.AddForeignKey(
				name: "fk_audit_log_admin_users_admin_user_id",
				table: "audit_log",
				column: "admin_user_id",
				principalTable: "admin_users",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);

			migrationBuilder.AddForeignKey(
				name: "fk_summaries_admin_users_approved_by",
				table: "summaries",
				column: "approved_by",
				principalTable: "admin_users",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);
		}
	}
}
