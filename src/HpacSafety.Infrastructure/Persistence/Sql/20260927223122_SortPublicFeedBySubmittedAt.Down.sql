-- Restores the view of 20260925164215_ReportIsPendingPublishedOrUnpublished.sql,
-- without submitted_at. A view cannot lose a trailing column in place
-- (CREATE OR REPLACE only ever appends), so public_report_media and
-- public_report_comments — both still live at this point in the migration
-- history and both reading public_reports — are dropped and restored with
-- it, in dependency order, the same pattern
-- 20260927172950_ListReportVersions.Down.sql and
-- 20260928181012_AddAttachmentCounts.Down.sql use.
DROP VIEW public_report_media;
DROP VIEW public_report_comments;
DROP VIEW public_reports;

CREATE VIEW public_reports AS
SELECT report.id COLLATE "C" AS id,
       summary.ai_summary_en,
       summary.ai_summary_fr,
       COALESCE(report.published_at, summary.approved_at) AS published_at,
       (SELECT count(*)::integer
        FROM report_comments AS comment
        WHERE comment.report_id = report.id
          AND comment.deleted IS NULL
          AND comment.hidden_at IS NULL) AS comment_count
FROM reports AS report
         JOIN summaries AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status = 'published'
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';

CREATE VIEW public_report_comments AS
SELECT comment.id COLLATE "C"        AS id,
       comment.report_id COLLATE "C" AS report_id,
       comment.author_subject,
       latest.text,
       latest.locale,
       latest.translated_text,
       comment.created_at,
       latest.created_at             AS updated_at,
       latest.number > 1             AS edited
FROM report_comments AS comment
         JOIN public_reports AS report ON report.id = comment.report_id
         JOIN LATERAL (SELECT revision.text,
                              revision.locale,
                              revision.translated_text,
                              revision.created_at,
                              revision.number
                       FROM report_comment_revisions AS revision
                       WHERE revision.comment_id = comment.id
                         AND revision.deleted IS NULL
                       ORDER BY revision.number DESC
                       LIMIT 1) AS latest ON TRUE
WHERE comment.deleted IS NULL
  AND comment.hidden_at IS NULL;

CREATE VIEW public_report_media AS
SELECT file.id COLLATE "C"        AS id,
       file.report_id COLLATE "C" AS report_id,
       file.kind,
       file.content_type,
       file.stripped_blob_key,
       NULL::varchar(512)         AS document_blob_key,
       file.uploaded_at
FROM report_files AS file
         JOIN public_reports AS report ON report.id = file.report_id
         JOIN reports AS source ON source.id = file.report_id
WHERE source.consent_media IS TRUE
  AND file.kind IN ('image', 'video')
  AND file.deleted IS NULL
  AND file.hidden_at IS NULL
  AND file.processing_error_code IS NULL
  AND file.stripped_blob_key IS NOT NULL
  AND file.exif_stripped_at IS NOT NULL
UNION ALL
SELECT file.id COLLATE "C"        AS id,
       file.report_id COLLATE "C" AS report_id,
       file.kind,
       file.content_type,
       NULL                       AS stripped_blob_key,
       file.blob_key              AS document_blob_key,
       file.uploaded_at
FROM report_files AS file
         JOIN public_reports AS report ON report.id = file.report_id
         JOIN reports AS source ON source.id = file.report_id
WHERE source.consent_documents IS TRUE
  AND file.kind = 'document'
  AND file.deleted IS NULL
  AND file.hidden_at IS NULL
  AND file.processing_error_code IS NULL
  AND file.validated_at IS NOT NULL;
