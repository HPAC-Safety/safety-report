using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddAttachmentAndTranslationOutboxTypes : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages");

			migrationBuilder.AddCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages",
				sql: "type IN ('summarize_report', 'process_attachment', 'translate_answers')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages");

			migrationBuilder.AddCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages",
				sql: "type IN ('summarize_report')");
		}
	}
}
