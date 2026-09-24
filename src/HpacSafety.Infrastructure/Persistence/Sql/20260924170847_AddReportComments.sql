-- Comments reach the public side only through views (ADR-0114, ADR-0055).
--
-- public_reports gains comment_count: comments neither deleted nor hidden.
-- Everything else about the view is unchanged from
-- 20260924161944_CreatePublicReportsView.sql. A column can only be added at
-- the end of a replaced view.
CREATE OR REPLACE VIEW public_reports AS
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
  AND report.status IN ('approved', 'published')
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';

-- Each visible comment's current revision, only while its report is public, so
-- unpublishing a report hides its comments with no further step.
-- author_subject is here for the API to compute isMine; it is never returned.
CREATE OR REPLACE VIEW public_report_comments AS
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
