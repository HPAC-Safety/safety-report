-- public_reports gains language: the locale the reporter wrote the report in
-- (issue #682, ADR-0176). A report's own page reads it to say a summary was
-- translated from that language; the feed never selects it. A column can only
-- be added at the end of a replaced view.
CREATE OR REPLACE VIEW public_reports AS
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
          AND file.deleted IS NULL) AS full_attachment_count,
       report.language
FROM reports AS report
         JOIN summaries AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status = 'published'
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';
