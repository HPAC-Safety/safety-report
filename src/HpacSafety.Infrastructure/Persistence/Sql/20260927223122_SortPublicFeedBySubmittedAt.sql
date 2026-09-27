-- The public feed sorts newest submitted first, a tie broken by report ID
-- (#570, ADR-0153, amends REQ-MOD-037). public_reports gains submitted_at:
-- the API reads it only to order the feed and build its keyset cursor, and
-- never returns it — the feed still displays published_at. Everything else
-- about the view is unchanged from
-- 20260925164215_ReportIsPendingPublishedOrUnpublished.sql. A column can only
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
       report.submitted_at
FROM reports AS report
         JOIN summaries AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status = 'published'
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';
