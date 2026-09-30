-- Reverses 20260930011550_ShowReportLanguageOnPublicReports.sql: restores
-- public_reports without language (as of 20260928181012_AddAttachmentCounts.sql).
-- A view cannot lose a trailing column in place, so the two views that read it,
-- public_report_media and public_report_comments, are dropped and restored with
-- it. search_public_reports is a SQL function whose body is not a tracked
-- dependency, so it survives untouched.
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
          AND comment.hidden_at IS NULL) AS comment_count,
       report.submitted_at,
       -- Mirrors public_report_media's own predicate rather than joining that
       -- view: public_report_media joins public_reports, so joining it back
       -- here would be a circular view reference ("infinite recursion
       -- detected in rules"). Kept in sync by hand; both read the same six
       -- columns off the same two tables.
       (SELECT count(*)::integer
        FROM report_files AS file
        WHERE file.report_id = report.id
          AND file.deleted IS NULL
          AND file.hidden_at IS NULL
          AND file.processing_error_code IS NULL
          AND ((file.kind IN ('image', 'video')
                    AND report.consent_media IS TRUE
                    AND file.stripped_blob_key IS NOT NULL
                    AND file.exif_stripped_at IS NOT NULL)
               OR (file.kind = 'document'
                    AND report.consent_documents IS TRUE
                    AND file.validated_at IS NOT NULL))) AS public_attachment_count,
       (SELECT count(*)::integer
        FROM report_files AS file
        WHERE file.report_id = report.id
          AND file.deleted IS NULL) AS full_attachment_count
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
SELECT comment.id COLLATE "C" AS id,
       comment.report_id COLLATE "C" AS report_id,
       comment.author_subject,
       latest.text,
       latest.locale,
       latest.translated_text,
       comment.created_at,
       latest.created_at AS updated_at,
       latest.number > 1 AS edited
FROM report_comments comment
         JOIN public_reports report ON report.id = comment.report_id
         JOIN LATERAL (
    SELECT revision.text,
           revision.locale,
           revision.translated_text,
           revision.created_at,
           revision.number
    FROM report_comment_revisions revision
    WHERE revision.comment_id = comment.id AND revision.deleted IS NULL
    ORDER BY revision.number DESC
    LIMIT 1) latest ON true
WHERE comment.deleted IS NULL AND comment.hidden_at IS NULL;

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
