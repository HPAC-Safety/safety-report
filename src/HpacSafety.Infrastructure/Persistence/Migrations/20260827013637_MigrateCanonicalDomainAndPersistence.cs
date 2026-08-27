using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Aligns current-main's schema with the canonical target in
    /// <c>docs/data-and-persistence.md</c>: complete immutable bilingual
    /// question revisions, a consent-only report projection, one bilingual
    /// summary row per report, attachment kinds, universal soft deletion, and
    /// removal of application-side field encryption.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data-preservation rules applied here, explicit and tested (see
    /// <c>MigrationDataTransformTests</c>):
    /// </para>
    /// <list type="bullet">
    /// <item><description>Each <c>question_versions</c> + its
    /// <c>question_translations</c> row becomes one <c>question_revisions</c>
    /// row. A revision missing its French counterpart on upgrade inherits the
    /// English wording until an administrator supplies a real translation,
    /// rather than leaving a null in a column the target model requires to be
    /// complete.</description></item>
    /// <item><description>Each <c>question_options</c> + its
    /// <c>question_option_translations</c> row becomes one
    /// <c>question_revision_options</c> row, under the same fallback rule.</description></item>
    /// <item><description>The two per-language <c>summaries</c> rows for a
    /// report become one row: English text from the <c>en-CA</c> row (or the
    /// only row, if just one language was ever generated), French text from
    /// the <c>fr-CA</c> row (or the only row). The pair is approved only if
    /// every existing language row for that report was individually approved;
    /// otherwise the merged row starts unapproved, since editing either
    /// language always clears approval going forward.</description></item>
    /// <item><description><c>reports.province</c>, <c>pilot_injury</c>,
    /// <c>passenger_injury</c>, <c>occurred_on</c>, and
    /// <c>occurred_at_local</c> are dropped: the target report keeps only the
    /// consent projection, and every other answer already exists, unchanged,
    /// in <c>report_answers</c>. <c>report_aircraft</c> is dropped for the
    /// same reason — an aircraft answer is an ordinary revision-bound answer
    /// now, not a specialized record.</description></item>
    /// <item><description>Any <c>questions.role</c> value other than
    /// <c>consent_publish</c> is reset to <c>none</c>, matching the reduced
    /// <c>QuestionRole</c> enum: consent is the only answer a report still
    /// reads by name.</description></item>
    /// </list>
    /// </remarks>
    public partial class MigrateCanonicalDomainAndPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----------------------------------------------------------------
            // Report answers: current main stores every non-null
            // report_answers.value as v1 AES-GCM ciphertext (see the
            // now-deleted EncryptedStringConverter/AesGcmFieldCipher). The new
            // mapping reads this column directly with no converter, so
            // existing rows must be decrypted to plaintext here, before the
            // application-side cipher is gone. The key comes from the same
            // configuration the outgoing cipher used
            // ("HpacSafety:FieldEncryption:Key", i.e. env var
            // HpacSafety__FieldEncryption__Key) — it must still be present in
            // the environment that runs this migration. A database with no
            // encrypted answers (a fresh install, or a test database) does not
            // require the key at all.
            // ----------------------------------------------------------------
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            var fieldEncryptionKeyBase64 = Environment.GetEnvironmentVariable("HpacSafety__FieldEncryption__Key");
            var keyExpression = string.IsNullOrWhiteSpace(fieldEncryptionKeyBase64)
                ? "NULL"
                : $"decode('{fieldEncryptionKeyBase64}', 'base64')";

            migrationBuilder.Sql(
                $"""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM report_answers WHERE value IS NOT NULL)
                       AND {keyExpression} IS NULL THEN
                        RAISE EXCEPTION
                            'HpacSafety__FieldEncryption__Key must be set to migrate existing encrypted report_answers.value rows to plaintext.';
                    END IF;
                END
                $guard$;
                """);

            // Reimplements AES-256-GCM decryption (as AesGcmFieldCipher wrote
            // it: "v1." + base64(nonce[12] || tag[16] || ciphertext)) in pure
            // SQL via pgcrypto's raw AES-ECB primitive, used only to generate
            // the GCM counter-mode keystream. The authentication tag is not
            // re-verified: this reads data our own application already wrote
            // and already trusted, one time, during an upgrade.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION pg_temp.hpac_decrypt_v1_aesgcm(ciphertext text, key bytea)
                RETURNS text
                LANGUAGE plpgsql
                AS $func$
                DECLARE
                    envelope bytea;
                    nonce bytea;
                    ct bytea;
                    block_count integer;
                    i integer;
                    j integer;
                    counter bytea;
                    ks_block bytea;
                    ct_block bytea;
                    plain_block bytea;
                    block_len integer;
                    plain bytea := ''::bytea;
                BEGIN
                    IF ciphertext IS NULL THEN
                        RETURN NULL;
                    END IF;

                    IF left(ciphertext, 3) <> 'v1.' THEN
                        RAISE EXCEPTION 'report_answers.value is not in the expected v1 AES-GCM format.';
                    END IF;

                    envelope := decode(substring(ciphertext from 4), 'base64');
                    nonce := substring(envelope from 1 for 12);
                    ct := substring(envelope from 29);
                    block_count := ceil(octet_length(ct)::numeric / 16);

                    FOR i IN 0..block_count - 1 LOOP
                        counter := nonce || int4send(i + 2);
                        ks_block := encrypt(counter, key, 'aes-ecb/pad:none');
                        ct_block := substring(ct from (i * 16) + 1 for 16);
                        block_len := octet_length(ct_block);
                        plain_block := ct_block;
                        FOR j IN 0..block_len - 1 LOOP
                            plain_block := set_byte(plain_block, j, get_byte(ct_block, j) # get_byte(ks_block, j));
                        END LOOP;
                        plain := plain || plain_block;
                    END LOOP;

                    RETURN convert_from(plain, 'UTF8');
                END;
                $func$;
                """);

            migrationBuilder.Sql(
                $"""
                UPDATE report_answers
                SET value = pg_temp.hpac_decrypt_v1_aesgcm(value, {keyExpression})
                WHERE value IS NOT NULL;
                """);

            migrationBuilder.Sql("DROP FUNCTION pg_temp.hpac_decrypt_v1_aesgcm(text, bytea);");

            // ----------------------------------------------------------------
            // Question bank: create the new complete-revision tables first, so
            // the transform below can read the old ones before they are
            // dropped.
            // ----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "question_revisions",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_private = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    section_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    label_en = table.Column<string>(type: "text", nullable: false),
                    label_fr = table.Column<string>(type: "text", nullable: false),
                    help_text_en = table.Column<string>(type: "text", nullable: true),
                    help_text_fr = table.Column<string>(type: "text", nullable: true),
                    placeholder_en = table.Column<string>(type: "text", nullable: true),
                    placeholder_fr = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_revisions", x => x.id);
                    table.CheckConstraint(
                        "ck_question_revisions_type",
                        "type IN ('short_text', 'long_text', 'email', 'phone', 'date', 'number', 'single_select', " +
                        "'multi_select', 'yes_no', 'checkbox', 'file_upload', 'statement', 'group')");
                    table.ForeignKey(
                        name: "fk_question_revisions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_revision_options",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_revision_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    label_en = table.Column<string>(type: "text", nullable: false),
                    label_fr = table.Column<string>(type: "text", nullable: false),
                    deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_revision_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_revision_options_question_revisions_question_revis~",
                        column: x => x.question_revision_id,
                        principalTable: "question_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Every version + its source (and, where it exists, generated)
            // translation becomes one complete bilingual revision. A revision
            // missing its French counterpart inherits the English wording
            // rather than leaving a null in a column the target model requires
            // to be complete — see MigrationDataTransformTests. Order,
            // section, privacy, and active state were question-scoped, not
            // versioned, in the old schema, so every historical revision of a
            // question inherits that question's current values — there is no
            // per-version history of them to preserve. is_required and
            // is_system are NOT copied from the old per-version/per-question
            // flags: is_required is derived from is_system here exactly as
            // Question/QuestionRevision now derive it in code, so a legacy row
            // that incorrectly marked an ordinary question required does not
            // survive the upgrade — see product invariant #1.
            migrationBuilder.Sql(
                """
                INSERT INTO question_revisions
                    (id, question_id, revision_number, type, is_system, is_required, is_private, is_active,
                     display_order, section_key, label_en, label_fr, help_text_en, help_text_fr, placeholder_en, placeholder_fr,
                     created_at, deleted)
                SELECT
                    v.id,
                    v.question_id,
                    v.version_number,
                    v.type,
                    q.is_system,
                    q.is_system,
                    q.is_private,
                    q.is_active,
                    q.display_order,
                    q.section_key,
                    en.label,
                    COALESCE(fr.label, en.label),
                    en.help_text,
                    COALESCE(fr.help_text, en.help_text),
                    en.placeholder,
                    COALESCE(fr.placeholder, en.placeholder),
                    v.created_at,
                    NULL
                FROM question_versions v
                JOIN questions q ON q.id = v.question_id
                JOIN question_translations en ON en.question_version_id = v.id AND en.locale = 'en-CA'
                LEFT JOIN question_translations fr ON fr.question_version_id = v.id AND fr.locale = 'fr-CA';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO question_revision_options
                    (id, question_revision_id, code, display_order, label_en, label_fr, deleted)
                SELECT
                    o.id,
                    o.question_version_id,
                    o.code,
                    o.display_order,
                    en.label,
                    COALESCE(fr.label, en.label),
                    NULL
                FROM question_options o
                JOIN question_option_translations en ON en.question_option_id = o.id AND en.locale = 'en-CA'
                LEFT JOIN question_option_translations fr ON fr.question_option_id = o.id AND fr.locale = 'fr-CA';
                """);

            // ----------------------------------------------------------------
            // Summaries: two per-language rows become one bilingual row.
            // ----------------------------------------------------------------
            migrationBuilder.AddColumn<string>(
                name: "ai_summary_en",
                table: "summaries",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE summaries s
                SET ai_summary_en = COALESCE(
                    (SELECT en.text FROM summaries en WHERE en.report_id = s.report_id AND en.language = 'en-CA' LIMIT 1),
                    s.text)
                """);

            migrationBuilder.Sql(
                """
                UPDATE summaries s
                SET text = COALESCE(
                    (SELECT fr.text FROM summaries fr WHERE fr.report_id = s.report_id AND fr.language = 'fr-CA' LIMIT 1),
                    s.text)
                """);

            // The pair is approved only if BOTH the en-CA and fr-CA legacy
            // rows existed for the report and both were individually
            // approved. A report that only ever had one language row never
            // had its second language produced or reviewed by anyone, even
            // though the earlier UPDATEs above duplicate that one text into
            // the missing language column — so it must not inherit that one
            // row's approval. Otherwise the merged row starts unapproved
            // rather than guessing which language's approval should stand for
            // the whole pair.
            migrationBuilder.Sql(
                """
                UPDATE summaries s
                SET approved_by = CASE
                        WHEN (SELECT COUNT(*) FROM summaries x WHERE x.report_id = s.report_id) = 2
                             AND NOT EXISTS (SELECT 1 FROM summaries x WHERE x.report_id = s.report_id AND x.approved_at IS NULL)
                            THEN (SELECT x.approved_by FROM summaries x WHERE x.report_id = s.report_id ORDER BY x.approved_at DESC LIMIT 1)
                        ELSE NULL
                    END,
                    approved_at = CASE
                        WHEN (SELECT COUNT(*) FROM summaries x WHERE x.report_id = s.report_id) = 2
                             AND NOT EXISTS (SELECT 1 FROM summaries x WHERE x.report_id = s.report_id AND x.approved_at IS NULL)
                            THEN (SELECT MAX(x.approved_at) FROM summaries x WHERE x.report_id = s.report_id)
                        ELSE NULL
                    END
                """);

            // Keep exactly one row per report, preferring the source row.
            migrationBuilder.Sql(
                """
                DELETE FROM summaries a
                USING summaries b
                WHERE a.report_id = b.report_id
                  AND (a.is_source, a.id) < (b.is_source, b.id)
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ai_summary_en",
                table: "summaries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_summaries_summaries_translated_from_summary_id",
                table: "summaries");

            migrationBuilder.DropIndex(
                name: "ix_summaries_report_id_language",
                table: "summaries");

            migrationBuilder.DropIndex(
                name: "ix_summaries_translated_from_summary_id",
                table: "summaries");

            migrationBuilder.DropColumn(
                name: "is_source",
                table: "summaries");

            migrationBuilder.DropColumn(
                name: "language",
                table: "summaries");

            migrationBuilder.DropColumn(
                name: "translated_from_summary_id",
                table: "summaries");

            migrationBuilder.RenameColumn(
                name: "text",
                table: "summaries",
                newName: "ai_summary_fr");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "summaries",
                newName: "generated_at");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "summaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE summaries SET updated_at = generated_at;");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "summaries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted",
                table: "summaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_summaries_report_id",
                table: "summaries",
                column: "report_id",
                unique: true);

            // IsApproved reads ApprovedAt alone, but a row with one of the
            // pair set and not the other is not a state the domain can
            // represent — Approve()/ClearApproval() always set or clear both
            // together.
            migrationBuilder.AddCheckConstraint(
                name: "ck_summaries_approval_coherence",
                table: "summaries",
                sql: "(approved_by IS NULL) = (approved_at IS NULL)");

            // ----------------------------------------------------------------
            // Question bank: retire the superseded tables and reduce
            // questions.role to the values QuestionRole still defines.
            // ----------------------------------------------------------------
            migrationBuilder.Sql("UPDATE questions SET role = 'none' WHERE role <> 'consent_publish';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_role",
                table: "questions",
                sql: "role IN ('none', 'consent_publish')");

            migrationBuilder.DropForeignKey(
                name: "fk_report_answers_question_versions_question_version_id",
                table: "report_answers");

            migrationBuilder.DropTable(
                name: "question_option_translations");

            migrationBuilder.DropTable(
                name: "question_translations");

            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "question_versions");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "questions",
                newName: "deleted");

            // Order, section, privacy, and active state are now revision
            // fields (copied onto every question_revisions row above) rather
            // than question fields — a referenced revision has to preserve
            // the complete question exactly as it was shown, which a mutable
            // column on questions cannot do. Question.IsSystem stays: it
            // never varies across a question's revisions, so keeping one copy
            // here alongside the per-revision snapshot is not redundant data
            // drift the way the others would be.
            migrationBuilder.DropIndex(
                name: "ix_questions_is_active_display_order",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "section_key",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "is_private",
                table: "questions");

            migrationBuilder.RenameColumn(
                name: "question_version_id",
                table: "report_answers",
                newName: "question_revision_id");

            migrationBuilder.RenameIndex(
                name: "ix_report_answers_question_version_id",
                table: "report_answers",
                newName: "ix_report_answers_question_revision_id");

            migrationBuilder.AddForeignKey(
                name: "fk_report_answers_question_revisions_question_revision_id",
                table: "report_answers",
                column: "question_revision_id",
                principalTable: "question_revisions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "ix_question_revision_options_question_revision_id_code",
                table: "question_revision_options",
                columns: new[] { "question_revision_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_revisions_question_id_revision_number",
                table: "question_revisions",
                columns: new[] { "question_id", "revision_number" },
                unique: true);

            // Current-form lookup: latest active revision, ordered for display.
            migrationBuilder.CreateIndex(
                name: "ix_question_revisions_is_active_display_order",
                table: "question_revisions",
                columns: new[] { "is_active", "display_order" });

            // ----------------------------------------------------------------
            // Reports: only the consent projection remains typed. Every other
            // answer already exists, unchanged, in report_answers.
            // ----------------------------------------------------------------
            migrationBuilder.DropColumn(
                name: "occurred_at_local",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "occurred_on",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "passenger_injury",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "pilot_injury",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "province",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "time_of_day",
                table: "reports");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_language",
                table: "reports",
                sql: "language IN ('en-CA', 'fr-CA')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_status",
                table: "reports",
                sql: "status IN ('submitted', 'summarizing', 'pending_review', 'summary_failed', 'approved', 'rejected', 'published')");

            // An aircraft answer is an ordinary revision-bound answer now, not
            // a specialized record. No target column exists to carry these
            // rows forward; documented data loss on upgrade.
            migrationBuilder.DropTable(
                name: "report_aircraft");

            // ----------------------------------------------------------------
            // Report files: attachment kind and the file-upload answer a file
            // belongs to.
            // ----------------------------------------------------------------
            migrationBuilder.DropIndex(
                name: "ix_report_answers_report_id",
                table: "report_answers");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "report_files",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "image");

            migrationBuilder.AddColumn<string>(
                name: "report_answer_id",
                table: "report_files",
                type: "char(11)",
                fixedLength: true,
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_error_code",
                table: "report_files",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted",
                table: "report_files",
                type: "timestamp with time zone",
                nullable: true);

            // The composite FK below needs only this one index: a lookup by
            // report_answer_id alone is never a real access pattern (files
            // are always read by report first), and EF's own model does not
            // ask for a second, single-column index here.
            migrationBuilder.CreateIndex(
                name: "ix_report_files_report_id_report_answer_id",
                table: "report_files",
                columns: new[] { "report_id", "report_answer_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_files_kind",
                table: "report_files",
                sql: "kind IN ('image', 'video', 'document')");

            // AwaitsStripping treats these two as one fact: a stripped-at
            // time with no key, or a key with no stripped-at time, would read
            // as a half-finished stripping nobody can act on.
            migrationBuilder.AddCheckConstraint(
                name: "ck_report_files_exif_stripped_coherence",
                table: "report_files",
                sql: "(exif_stripped_at IS NULL) = (stripped_blob_key IS NULL)");

            // A file belongs to exactly one file-upload answer on the same
            // report — never one on a different report. An independent FK on
            // report_answer_id alone cannot express that: it happily accepts
            // report_id = A with an answer that belongs to report B. The
            // composite FK below, against the compound alternate key just
            // added on report_answers, is what actually enforces it. A NULL
            // report_answer_id still satisfies the constraint (Postgres
            // MATCH SIMPLE), so the not-yet-linked window during submission
            // processing is unaffected.
            migrationBuilder.AddUniqueConstraint(
                name: "ak_report_answers_report_id_id",
                table: "report_answers",
                columns: new[] { "report_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_report_files_report_answers_report_id_report_answer_id",
                table: "report_files",
                columns: new[] { "report_id", "report_answer_id" },
                principalTable: "report_answers",
                principalColumns: new[] { "report_id", "id" },
                onDelete: ReferentialAction.Restrict);

            // ----------------------------------------------------------------
            // Report answers: at most one revision of the same stable key per
            // report, plus universal soft deletion.
            // ----------------------------------------------------------------
            migrationBuilder.CreateIndex(
                name: "ix_report_answers_report_id_question_id",
                table: "report_answers",
                columns: new[] { "report_id", "question_id" },
                unique: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted",
                table: "report_answers",
                type: "timestamp with time zone",
                nullable: true);

            // ----------------------------------------------------------------
            // Outbox: a typed message type instead of a free-text one.
            // ----------------------------------------------------------------
            migrationBuilder.Sql("UPDATE outbox_messages SET type = 'summarize_report' WHERE type = 'report.submitted';");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "outbox_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_outbox_messages_type",
                table: "outbox_messages",
                sql: "type IN ('summarize_report')");

            // ----------------------------------------------------------------
            // Admin users: universal soft deletion, and a member identifier
            // unique among live rows only — a deleted administrator must not
            // permanently block re-adding the same upstream member
            // identifier. The query filter already hides deleted rows from
            // ordinary reads, but a plain unique index is still checked
            // against them by the database, so the old index has to be
            // replaced rather than left in place.
            // ----------------------------------------------------------------
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted",
                table: "admin_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "ix_admin_users_member_identifier",
                table: "admin_users");

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_member_identifier",
                table: "admin_users",
                column: "member_identifier",
                unique: true,
                filter: "deleted IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admin_users_role",
                table: "admin_users",
                sql: "role IN ('safety_officer', 'administrator')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "This migration collapses per-language summaries and question translations into single bilingual " +
                "rows. Reversing it would have to re-invent language rows this schema no longer has anywhere to " +
                "put, so it is not supported. Restore from a backup taken before this migration instead.");
        }
    }
}
