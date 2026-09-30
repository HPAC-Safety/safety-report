using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Summaries become append-only revisions (#668, ADR-0177). Creates
	///     <c>summary_revisions</c>, folds every existing <c>summaries</c> row into
	///     it as revision 1 (text, sources, provenance, and approval kept; author
	///     unknown), adds the <c>latest_summary_revisions</c> and
	///     <c>latest_approved_summary_revisions</c> views, points
	///     <c>public_reports</c>, <c>admin_report_queue</c>, and
	///     <c>admin_report_search_document</c> at them, and only then drops the
	///     columns of <c>summaries</c> that revision 1 now holds. No row is
	///     deleted; the table stays as the one-row-per-report identity the
	///     revisions hang from.
	/// </summary>
	public partial class AddSummaryRevisions : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "summary_revisions",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					summary_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					sequence = table.Column<int>(type: "integer", nullable: false),
					ai_summary_en = table.Column<string>(type: "text", nullable: false),
					ai_summary_fr = table.Column<string>(type: "text", nullable: false),
					source_en = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
					source_fr = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
					model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
					prompt_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
					author_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					restored_from_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: true),
					approved_by_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
					approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_summary_revisions", x => x.id);
					table.CheckConstraint("ck_summary_revisions_approval_coherence", "(approved_by_subject IS NULL) = (approved_at IS NULL)");
					table.CheckConstraint("ck_summary_revisions_sequence", "sequence >= 1");
					table.CheckConstraint("ck_summary_revisions_source_en", "source_en IN ('generated', 'human', 'machine')");
					table.CheckConstraint("ck_summary_revisions_source_fr", "source_fr IN ('generated', 'human', 'machine')");
					table.ForeignKey(
						name: "fk_summary_revisions_summaries_summary_id",
						column: x => x.summary_id,
						principalTable: "summaries",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
					table.ForeignKey(
						name: "fk_summary_revisions_summary_revisions_restored_from_id",
						column: x => x.restored_from_id,
						principalTable: "summary_revisions",
						principalColumn: "id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "ix_summary_revisions_restored_from_id",
				table: "summary_revisions",
				column: "restored_from_id");

			migrationBuilder.CreateIndex(
				name: "ix_summary_revisions_summary_id_sequence",
				table: "summary_revisions",
				columns: new[] { "summary_id", "sequence" },
				unique: true);

			// Fold first, drop after: the views below stop reading these columns
			// before the columns go.
			migrationBuilder.Sql(SqlScript.Read("20260930014051_AddSummaryRevisions.sql"));

			migrationBuilder.DropCheckConstraint(
				name: "ck_summaries_approval_coherence",
				table: "summaries");

			migrationBuilder.DropCheckConstraint(
				name: "ck_summaries_source_en",
				table: "summaries");

			migrationBuilder.DropCheckConstraint(
				name: "ck_summaries_source_fr",
				table: "summaries");

			migrationBuilder.DropColumn(name: "ai_summary_en", table: "summaries");
			migrationBuilder.DropColumn(name: "ai_summary_fr", table: "summaries");
			migrationBuilder.DropColumn(name: "approved_at", table: "summaries");
			migrationBuilder.DropColumn(name: "approved_by_subject", table: "summaries");
			migrationBuilder.DropColumn(name: "generated_at", table: "summaries");
			migrationBuilder.DropColumn(name: "model", table: "summaries");
			migrationBuilder.DropColumn(name: "prompt_version", table: "summaries");
			migrationBuilder.DropColumn(name: "source_en", table: "summaries");
			migrationBuilder.DropColumn(name: "source_fr", table: "summaries");
			migrationBuilder.DropColumn(name: "updated_at", table: "summaries");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(name: "ai_summary_en", table: "summaries", type: "text", nullable: false, defaultValue: "");
			migrationBuilder.AddColumn<string>(name: "ai_summary_fr", table: "summaries", type: "text", nullable: false, defaultValue: "");
			migrationBuilder.AddColumn<DateTimeOffset>(name: "approved_at", table: "summaries", type: "timestamp with time zone", nullable: true);
			migrationBuilder.AddColumn<string>(name: "approved_by_subject", table: "summaries", type: "character varying(256)", maxLength: 256, nullable: true);
			migrationBuilder.AddColumn<DateTimeOffset>(name: "generated_at", table: "summaries", type: "timestamp with time zone", nullable: false, defaultValueSql: "now()");
			migrationBuilder.AddColumn<string>(name: "model", table: "summaries", type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: "");
			migrationBuilder.AddColumn<string>(name: "prompt_version", table: "summaries", type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "");
			migrationBuilder.AddColumn<string>(name: "source_en", table: "summaries", type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "generated");
			migrationBuilder.AddColumn<string>(name: "source_fr", table: "summaries", type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "generated");
			migrationBuilder.AddColumn<DateTimeOffset>(name: "updated_at", table: "summaries", type: "timestamp with time zone", nullable: false, defaultValueSql: "now()");

			migrationBuilder.Sql(SqlScript.Read("20260930014051_AddSummaryRevisions.Down.sql"));

			migrationBuilder.AddCheckConstraint(
				name: "ck_summaries_approval_coherence",
				table: "summaries",
				sql: "(approved_by_subject IS NULL) = (approved_at IS NULL)");

			migrationBuilder.AddCheckConstraint(
				name: "ck_summaries_source_en",
				table: "summaries",
				sql: "source_en IN ('generated', 'human', 'machine')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_summaries_source_fr",
				table: "summaries",
				sql: "source_fr IN ('generated', 'human', 'machine')");

			migrationBuilder.DropTable(
				name: "summary_revisions");
		}
	}
}
