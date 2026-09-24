using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     The <c>admin_report_queue</c>, <c>answers_awaiting_translation</c>, and
	///     <c>admin_pending_counts</c> views the admin list, the translation queue,
	///     and the Admin menu's counts read (#418). It adds no table and changes no row.
	/// </summary>
	public partial class CreateAdminQueueViews : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260924180636_CreateAdminQueueViews.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260924180636_CreateAdminQueueViews.Down.sql"));
		}
	}
}
