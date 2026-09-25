-- A report is Pending, Published, or Unpublished once the Worker is done with
-- it; Approved and Rejected are gone (ADR-0125). No production data exists, so
-- this only maps development rows onto the new codes before the new status
-- constraint is added. A report without publication consent is Unpublished
-- for good, whatever it was before.
UPDATE reports
SET status = CASE
                 WHEN consent_publish IS NOT TRUE AND status NOT IN ('submitted', 'summarizing') THEN 'unpublished'
                 WHEN status = 'pending_review' THEN 'pending'
                 WHEN status = 'rejected' THEN 'unpublished'
                 WHEN status = 'approved' THEN 'published'
                 ELSE status
    END
WHERE status IN ('pending_review', 'rejected', 'approved', 'summary_failed', 'published');

-- Only a Published report is public. Everything else about the view is
-- unchanged from 20260924170847_AddReportComments.sql.
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
  AND report.status = 'published'
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';

-- A report needs action when it is stuck, pending, or its summary failed
-- (REQ-MOD-049). An Unpublished report never does (REQ-MOD-090). Everything
-- else is unchanged from 20260924180636_CreateAdminQueueViews.sql.
CREATE OR REPLACE VIEW admin_report_queue AS
SELECT report.id,
       report.submitted_at,
       report.status,
       report.language,
       report.consent_publish,
       stuck.is_stuck,
       stuck.is_stuck OR report.status IN ('pending', 'summary_failed') AS needs_action
FROM reports AS report
         CROSS JOIN LATERAL (
    SELECT report.status IN ('submitted', 'summarizing')
               AND report.submitted_at < now() - interval '24 hours' AS is_stuck
    ) AS stuck
WHERE report.deleted IS NULL;
