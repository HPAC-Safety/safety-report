-- ADR-0129: a reporter-added type-ahead value waits for a Safety Officer or
-- Administrator to review it. Before this, "awaiting review" meant a
-- reporter-added value still missing a language, so those are the live ones
-- flagged now; one an Administrator already completed was curated.
UPDATE question_choices
SET needs_review = TRUE
WHERE added_by_reporter
  AND deleted IS NULL
  AND (label_en IS NULL OR label_fr IS NULL);

-- One row: how much admin work is waiting (REQ-MOD-084), now with the
-- type-ahead values waiting for review on live questions (REQ-MOD-093).
CREATE OR REPLACE VIEW admin_pending_counts AS
SELECT (SELECT count(*) FROM admin_report_queue WHERE needs_action)::integer AS reports_needing_action,
       (SELECT count(*) FROM answers_awaiting_translation)::integer          AS answers_awaiting_translation,
       (SELECT count(*)
        FROM question_choices AS choice
                 JOIN questions AS question ON question.id = choice.question_id
        WHERE choice.needs_review
          AND question.deleted IS NULL)::integer                             AS type_ahead_values_awaiting_review;
