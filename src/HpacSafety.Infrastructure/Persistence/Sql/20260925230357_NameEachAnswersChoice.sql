-- ADR-0128: a single-select, multi-select, or type-ahead answer names its
-- choice. Every such answer stored before this copied the label it was given
-- as its value; this links it to the choice that label names, without
-- rewriting the answer (answers are immutable, ADR-0080).
--
-- The label a reporter saw is the choice's label in the answer's language,
-- or the one language a one-language choice has (QuestionChoice.Label). Each
-- statement reads the answers still waiting for a choice: a select answer
-- with a value and no choice yet.

-- 1. The choice the answer's label names: exactly as shown, a live choice
--    before a removed one; failing that, ignoring case, in either language.
WITH choice_answer AS (
	SELECT answer.id, answer.question_id, answer.locale, answer.value, revision.type
	FROM report_answers AS answer
	JOIN question_revisions AS revision ON revision.id = answer.question_revision_id
	WHERE revision.type IN ('single_select', 'multi_select', 'autocomplete')
	  AND answer.choice_id IS NULL
	  AND answer.value IS NOT NULL
	  AND btrim(answer.value) <> ''
)
UPDATE report_answers AS answer
SET choice_id = matched.choice_id
FROM (
	SELECT pending.id,
		   (SELECT choice.id
			FROM question_choices AS choice
			WHERE choice.question_id = pending.question_id
			  AND (CASE WHEN pending.locale = 'fr-CA'
						THEN coalesce(choice.label_fr, choice.label_en)
						ELSE coalesce(choice.label_en, choice.label_fr) END = pending.value
				   OR lower(choice.label_en) = lower(pending.value)
				   OR lower(choice.label_fr) = lower(pending.value))
			ORDER BY CASE WHEN pending.locale = 'fr-CA'
							   THEN coalesce(choice.label_fr, choice.label_en)
							   ELSE coalesce(choice.label_en, choice.label_fr) END = pending.value DESC,
					 choice.deleted IS NULL DESC,
					 choice.id
			LIMIT 1) AS choice_id
	FROM choice_answer AS pending
) AS matched
WHERE answer.id = matched.id
  AND matched.choice_id IS NOT NULL;

-- 2. A label no choice carries any more — the option was relabelled or
--    removed after the answer was given — becomes a removed choice holding
--    that label in the answer's language, so every old answer resolves. One
--    row per question, language, and label. Ids and codes are derived from
--    them, so a re-run mints nothing twice.
WITH choice_answer AS (
	SELECT answer.id, answer.question_id, answer.locale, answer.value, revision.type
	FROM report_answers AS answer
	JOIN question_revisions AS revision ON revision.id = answer.question_revision_id
	WHERE revision.type IN ('single_select', 'multi_select', 'autocomplete')
	  AND answer.choice_id IS NULL
	  AND answer.value IS NOT NULL
	  AND btrim(answer.value) <> ''
)
INSERT INTO question_choices
	(id, question_id, code, display_order, label_en, label_fr, added_by_reporter, reporter_locale, deleted)
SELECT DISTINCT ON (pending.question_id, orphan.code)
	left(translate(encode(sha256(convert_to('question_choice:' || pending.question_id || ':' || orphan.code, 'UTF8')), 'base64'), '+/', '-_'), 11),
	pending.question_id,
	orphan.code,
	1000000,
	CASE WHEN pending.locale = 'fr-CA' THEN NULL ELSE pending.value END,
	CASE WHEN pending.locale = 'fr-CA' THEN pending.value ELSE NULL END,
	pending.type = 'autocomplete',
	CASE WHEN pending.type = 'autocomplete' THEN pending.locale END,
	now()
FROM choice_answer AS pending
CROSS JOIN LATERAL (
	SELECT 'answered_' || left(md5(pending.locale || ':' || pending.value), 16) AS code
	) AS orphan
ORDER BY pending.question_id, orphan.code
ON CONFLICT (question_id, code) DO NOTHING;

-- 3. Link those answers to the rows step 2 made for them.
WITH choice_answer AS (
	SELECT answer.id, answer.question_id, answer.locale, answer.value, revision.type
	FROM report_answers AS answer
	JOIN question_revisions AS revision ON revision.id = answer.question_revision_id
	WHERE revision.type IN ('single_select', 'multi_select', 'autocomplete')
	  AND answer.choice_id IS NULL
	  AND answer.value IS NOT NULL
	  AND btrim(answer.value) <> ''
)
UPDATE report_answers AS answer
SET choice_id = choice.id
FROM choice_answer AS pending
JOIN question_choices AS choice
	ON choice.question_id = pending.question_id
	AND choice.code = 'answered_' || left(md5(pending.locale || ':' || pending.value), 16)
WHERE answer.id = pending.id
  AND answer.choice_id IS NULL;

-- A choice answer reads its second language from its choice, so the
-- translation queue leaves it out, including an older one the Worker never
-- reached (ADR-0128).
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
  AND answer.translation_mode = 'machine'
  AND answer.choice_id IS NULL;
