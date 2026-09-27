-- Restores admin_report_queue without its version column. A view cannot lose
-- a column in place, so admin_pending_counts, which reads it, is dropped and
-- restored with it, as 20260925235946_ReviewTypeAheadValues.sql defined it.
DROP VIEW admin_pending_counts;
DROP VIEW admin_report_queue;

CREATE VIEW admin_report_queue AS
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

CREATE VIEW admin_pending_counts AS
SELECT (SELECT count(*) FROM admin_report_queue WHERE needs_action)::integer AS reports_needing_action,
       (SELECT count(*) FROM answers_awaiting_translation)::integer          AS answers_awaiting_translation,
       (SELECT count(*)
        FROM question_choices AS choice
                 JOIN questions AS question ON question.id = choice.question_id
        WHERE choice.needs_review
          AND question.deleted IS NULL)::integer                             AS type_ahead_values_awaiting_review;
