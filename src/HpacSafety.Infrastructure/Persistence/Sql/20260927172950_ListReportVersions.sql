-- #568: each row of the admin report list carries the version a review command
-- sends back (ADR-0105), so a reviewer can act on a row without the audited
-- detail read. It is ConcurrencyToken.Of's format: the report's xmin, a dot,
-- and its live summary's xmin, or 0 when it has none. A new column goes last,
-- so CREATE OR REPLACE keeps admin_pending_counts, which reads this view.
CREATE OR REPLACE VIEW admin_report_queue AS
SELECT report.id,
       report.submitted_at,
       report.status,
       report.language,
       report.consent_publish,
       stuck.is_stuck,
       stuck.is_stuck OR report.status IN ('pending', 'summary_failed') AS needs_action,
       report.xmin::text || '.' || COALESCE(summary.xmin::text, '0') AS version
FROM reports AS report
         CROSS JOIN LATERAL (
    SELECT report.status IN ('submitted', 'summarizing')
               AND report.submitted_at < now() - interval '24 hours' AS is_stuck
    ) AS stuck
         LEFT JOIN summaries AS summary
                   ON summary.report_id = report.id
                       AND summary.deleted IS NULL
WHERE report.deleted IS NULL;
