using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class TranslateReporterAddedValues : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages");

			migrationBuilder.AddColumn<string>(
				name: "label_en_source",
				table: "question_choices",
				type: "character varying(16)",
				maxLength: 16,
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "label_fr_source",
				table: "question_choices",
				type: "character varying(16)",
				maxLength: 16,
				nullable: true);

			migrationBuilder.Sql(SqlScript.Read("20260925233040_TranslateReporterAddedValues.sql"));

			migrationBuilder.AddCheckConstraint(
				name: "ck_question_choices_label_source",
				table: "question_choices",
				sql: "(label_en_source IS NULL OR label_en_source IN ('human', 'auto')) AND (label_fr_source IS NULL OR label_fr_source IN ('human', 'auto')) AND (label_en IS NULL) = (label_en_source IS NULL) AND (label_fr IS NULL) = (label_fr_source IS NULL)");

			migrationBuilder.AddCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages",
				sql: "type IN ('summarize_report', 'process_attachment', 'translate_answers', 'translate_comment', 'translate_choice')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_question_choices_label_source",
				table: "question_choices");

			migrationBuilder.DropCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages");

			migrationBuilder.DropColumn(
				name: "label_en_source",
				table: "question_choices");

			migrationBuilder.DropColumn(
				name: "label_fr_source",
				table: "question_choices");

			migrationBuilder.AddCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages",
				sql: "type IN ('summarize_report', 'process_attachment', 'translate_answers', 'translate_comment')");
		}
	}
}
