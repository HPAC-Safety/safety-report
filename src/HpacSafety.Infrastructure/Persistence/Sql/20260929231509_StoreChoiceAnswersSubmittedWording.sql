-- ADR-0175: a choice answer keeps the exact wording the reporter saw and
-- chose, immutable, alongside the choice it names (ADR-0128). Backfills every
-- existing choice answer's new submitted_wording column. No later fix,
-- replace, or merge is undone here; this only records what the reporter was
-- shown at the time each row was written, from the best evidence that
-- survives.

-- 1. A select answer stored before ADR-0128 (20260925230357) already copied
--    the label it was shown into `value`, and the migration that added
--    choice_id linked it without rewriting it. That copied label is exactly
--    the submitted wording.
UPDATE report_answers
SET submitted_wording = value
WHERE choice_id IS NOT NULL
  AND submitted_wording IS NULL
  AND value IS NOT NULL;

-- 2. A select answer stored under ADR-0128 or later copied no wording at
--    all, and answers are immutable, so what the reporter actually saw is
--    not recoverable. The best record left is the choice's current label in
--    the answer's own language — following one merge hop, since ADR-0129
--    flattens merge chains when they are made, so a merged-into choice is
--    never itself merged. If a fix or replace has run since, this already
--    differs from what was actually shown; nothing better is left to backfill
--    from.
UPDATE report_answers AS answer
SET submitted_wording =
	CASE WHEN answer.locale = 'fr-CA'
		 THEN coalesce(resolved.label_fr, resolved.label_en)
		 ELSE coalesce(resolved.label_en, resolved.label_fr) END
FROM question_choices AS choice
LEFT JOIN question_choices AS resolved ON resolved.id = coalesce(choice.merged_into_choice_id, choice.id)
WHERE answer.choice_id = choice.id
  AND answer.submitted_wording IS NULL
  AND answer.value IS NULL;
