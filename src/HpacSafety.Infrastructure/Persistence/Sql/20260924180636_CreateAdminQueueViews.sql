-- The admin side's read models (#418): what the review list shows, what is
-- waiting for a second language, and how much of each there is. The rules
-- live here once, so the list, its filters, and the Admin menu's counts
-- cannot disagree.

-- Every live report, state and timing only (REQ-MOD-030). A report is stuck
-- when it has waited in Submitted or Summarizing for more than 24 hours, and
-- needs action when it is stuck, pending review, or its summary failed
-- (REQ-MOD-049).
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

-- Every live answer marked for machine translation that has no second
-- language yet (ADR-0080, ADR-0112). The predicate matches the partial index
-- on report_answers.answered_at, so the queue and its count read that index.
CREATE OR REPLACE VIEW answers_awaiting_translation AS
SELECT answer.id,
       answer.question_key,
       answer.value,
       answer.locale,
       answer.answered_at
FROM report_answers AS answer
WHERE answer.deleted IS NULL
  AND answer.value IS NOT NULL
  AND answer.translated_value IS NULL
  AND answer.translation_mode = 'machine';

-- One row: how much admin work is waiting (REQ-MOD-084).
CREATE OR REPLACE VIEW admin_pending_counts AS
SELECT (SELECT count(*) FROM admin_report_queue WHERE needs_action)::integer AS reports_needing_action,
       (SELECT count(*) FROM answers_awaiting_translation)::integer          AS answers_awaiting_translation;
