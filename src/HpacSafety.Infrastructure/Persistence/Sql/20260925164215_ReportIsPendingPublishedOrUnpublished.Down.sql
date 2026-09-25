-- Restores the views of 20260924170847_AddReportComments.sql and
-- 20260924180636_CreateAdminQueueViews.sql, and maps the new codes back onto
-- the old ones: Pending is Pending review, and Unpublished is Rejected.
CREATE OR REPLACE VIEW admin_report_queue AS
SELECT report.id,
       report.submitted_at,
       report.status,
       report.language,
       report.consent_publish,
       stuck.is_stuck,
       stuck.is_stuck OR report.status IN ('pending_review', 'summary_failed') AS needs_action
FROM reports AS report
         CROSS JOIN LATERAL (
    SELECT report.status IN ('submitted', 'summarizing')
               AND report.submitted_at < now() - interval '24 hours' AS is_stuck
    ) AS stuck
WHERE report.deleted IS NULL;

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

UPDATE reports
SET status = CASE status
                 WHEN 'pending' THEN 'pending_review'
                 WHEN 'unpublished' THEN 'rejected'
                 ELSE status
    END
WHERE status IN ('pending', 'unpublished');
