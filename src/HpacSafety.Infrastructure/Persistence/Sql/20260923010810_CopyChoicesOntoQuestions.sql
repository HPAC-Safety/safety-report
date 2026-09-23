-- Copies every question's current choices onto the question itself, before
-- GiveEachQuestionItsOwnChoices drops the tables they came from (ADR-0095).
--
-- A question's choices come from its current (highest-numbered) revision:
--   * a type-ahead, or a multi-select that allowed reporter additions, whose
--     current revision named a live shared list rendered that list's live
--     items, so it takes every item of the list — removed ones stay removed,
--     reporter-added marks are kept, and a reporter-added item still waiting
--     for its other language keeps only the language the reporter typed;
--   * every other question takes its current revision's own option rows.
--
-- Ids are derived from the question and the code, in the TinyId alphabet
-- (base64url), so a re-run cannot mint a second row for one choice.
WITH current_revision AS (
	SELECT DISTINCT ON (r.question_id)
		r.question_id, r.id, r.type, r.option_set_id, r.allows_reporter_additions
	FROM question_revisions AS r
	ORDER BY r.question_id, r.revision_number DESC
),
from_shared_list AS (
	SELECT
		c.question_id,
		i.code,
		i.display_order,
		CASE WHEN i.added_by_reporter AND i.needs_translation AND i.reporter_locale = 'fr-CA'
			THEN NULL ELSE i.label_en END AS label_en,
		CASE WHEN i.added_by_reporter AND i.needs_translation AND i.reporter_locale = 'en-CA'
			THEN NULL ELSE i.label_fr END AS label_fr,
		i.added_by_reporter,
		i.reporter_locale,
		i.deleted
	FROM current_revision AS c
	JOIN option_sets AS s ON s.id = c.option_set_id AND s.deleted IS NULL
	JOIN option_set_items AS i ON i.option_set_id = s.id
	WHERE c.type = 'autocomplete' OR c.allows_reporter_additions
),
from_revision AS (
	SELECT
		c.question_id,
		o.code,
		o.display_order,
		o.label_en,
		o.label_fr,
		FALSE AS added_by_reporter,
		NULL::varchar(8) AS reporter_locale,
		NULL::timestamptz AS deleted
	FROM current_revision AS c
	JOIN question_revision_options AS o ON o.question_revision_id = c.id AND o.deleted IS NULL
	WHERE NOT EXISTS (SELECT 1 FROM from_shared_list AS f WHERE f.question_id = c.question_id)
),
choices AS (
	SELECT * FROM from_shared_list
	UNION ALL
	SELECT * FROM from_revision
)
INSERT INTO question_choices
	(id, question_id, code, display_order, label_en, label_fr, added_by_reporter, reporter_locale, deleted)
SELECT
	left(translate(encode(sha256(convert_to('question_choice:' || question_id || ':' || code, 'UTF8')), 'base64'), '+/', '-_'), 11),
	question_id, code, display_order, label_en, label_fr, added_by_reporter, reporter_locale, deleted
FROM choices
ON CONFLICT (question_id, code) DO NOTHING;
