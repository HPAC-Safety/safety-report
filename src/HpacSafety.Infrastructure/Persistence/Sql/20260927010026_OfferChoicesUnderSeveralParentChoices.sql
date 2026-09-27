-- ADR-0151: a dependent question's choice is offered under one or more parent
-- choices, through question_choice_parents, and its wording is unique on the
-- question. This runs between creating the join table and dropping
-- question_choices.parent_choice_id, in the migration's one transaction.
--
-- A link row's id is derived from its pair, so a re-run mints nothing twice.

-- 1. Fold every single link into the join table.
INSERT INTO question_choice_parents (id, choice_id, parent_choice_id, deleted)
SELECT left(translate(encode(sha256(convert_to('question_choice_parent:' || choice.id || ':' || choice.parent_choice_id, 'UTF8')), 'base64'), '+/', '-_'), 11),
	   choice.id,
	   choice.parent_choice_id,
	   NULL
FROM question_choices AS choice
WHERE choice.parent_choice_id IS NOT NULL
ON CONFLICT DO NOTHING;

-- 2. Verify the fold before anything is dropped: every link the column held has
--    a live row. Otherwise stop, and the whole migration rolls back.
DO $$
DECLARE
	missing integer;
BEGIN
	SELECT count(*)
	INTO missing
	FROM question_choices AS choice
	WHERE choice.parent_choice_id IS NOT NULL
	  AND NOT EXISTS (
		  SELECT 1
		  FROM question_choice_parents AS link
		  WHERE link.choice_id = choice.id
			AND link.parent_choice_id = choice.parent_choice_id
			AND link.deleted IS NULL);

	IF missing > 0 THEN
		RAISE EXCEPTION
			'% choice link(s) were not copied into question_choice_parents. The migration was rolled back; nothing changed.',
			missing;
	END IF;
END
$$;

-- 3. The duplicates to merge: within one live dependent question, live choices
--    whose English and French wording both match, after trimming, collapsing
--    whitespace, and ignoring case. The oldest survives: an Administrator's
--    choice records no creation time, and copies of one wording were coded
--    stem, stem_2, stem_3 in the order they were made, so the oldest is the
--    shortest code, then the lowest code, then the id. A pair matching in one
--    language only is left for an Administrator.
CREATE TEMPORARY TABLE choice_duplicate ON COMMIT DROP AS
WITH dependent AS (
	SELECT question.id AS question_id, current_revision.type
	FROM questions AS question
	CROSS JOIN LATERAL (
		SELECT revision.type
		FROM question_revisions AS revision
		WHERE revision.question_id = question.id
		ORDER BY revision.revision_number DESC
		LIMIT 1) AS current_revision
	WHERE question.deleted IS NULL
	  AND question.choices_depend_on_question_id IS NOT NULL
),
worded AS (
	SELECT choice.id,
		   choice.question_id,
		   choice.code,
		   dependent.type,
		   coalesce(lower(btrim(regexp_replace(choice.label_en, '\s+', ' ', 'g'))), '') AS wording_en,
		   coalesce(lower(btrim(regexp_replace(choice.label_fr, '\s+', ' ', 'g'))), '') AS wording_fr
	FROM question_choices AS choice
	JOIN dependent ON dependent.question_id = choice.question_id
	WHERE choice.deleted IS NULL
),
ranked AS (
	SELECT worded.id,
		   worded.type,
		   row_number() OVER copies AS rank,
		   first_value(worded.id) OVER copies AS survivor_id
	FROM worded
	WINDOW copies AS (PARTITION BY worded.question_id, worded.wording_en, worded.wording_fr
					  ORDER BY length(worded.code), worded.code, worded.id)
)
SELECT ranked.id AS duplicate_id, ranked.survivor_id, ranked.type
FROM ranked
WHERE ranked.rank > 1;

-- 4. The survivor is offered under every parent choice any copy was: a stamped
--    link it has is restored, and a missing one added.
UPDATE question_choice_parents AS survivor_link
SET deleted = NULL
FROM choice_duplicate AS duplicate
JOIN question_choice_parents AS copy_link
	ON copy_link.choice_id = duplicate.duplicate_id
	AND copy_link.deleted IS NULL
WHERE survivor_link.choice_id = duplicate.survivor_id
  AND survivor_link.parent_choice_id = copy_link.parent_choice_id
  AND survivor_link.deleted IS NOT NULL;

INSERT INTO question_choice_parents (id, choice_id, parent_choice_id, deleted)
SELECT DISTINCT
	   left(translate(encode(sha256(convert_to('question_choice_parent:' || duplicate.survivor_id || ':' || copy_link.parent_choice_id, 'UTF8')), 'base64'), '+/', '-_'), 11),
	   duplicate.survivor_id,
	   copy_link.parent_choice_id,
	   NULL::timestamptz
FROM choice_duplicate AS duplicate
JOIN question_choice_parents AS copy_link
	ON copy_link.choice_id = duplicate.duplicate_id
	AND copy_link.deleted IS NULL
ON CONFLICT DO NOTHING;

-- 5. Each other copy is retired into the survivor, and no answer is rewritten:
--    a picker option is replaced by it (ADR-0128), so a condition naming the
--    copy follows it there on read; a type-ahead value is merged into it
--    (ADR-0129), and a value merged into the copy is re-pointed so merges stay
--    flat.
UPDATE question_choices AS choice
SET deleted = now(),
	replaced_by_choice_id = duplicate.survivor_id
FROM choice_duplicate AS duplicate
WHERE choice.id = duplicate.duplicate_id
  AND duplicate.type <> 'autocomplete';

UPDATE question_choices AS choice
SET deleted = now(),
	merged_into_choice_id = duplicate.survivor_id
FROM choice_duplicate AS duplicate
WHERE choice.id = duplicate.duplicate_id
  AND duplicate.type = 'autocomplete';

UPDATE question_choices AS choice
SET merged_into_choice_id = duplicate.survivor_id
FROM choice_duplicate AS duplicate
WHERE choice.merged_into_choice_id = duplicate.duplicate_id;

-- 6. A link naming a retired copy as its parent follows the survivor: the old
--    link is stamped and the child offered under the survivor. A child is
--    nobody's parent, so there are none today; this keeps the rule true.
UPDATE question_choice_parents AS child_link
SET deleted = NULL
FROM choice_duplicate AS duplicate
JOIN question_choice_parents AS old_link
	ON old_link.parent_choice_id = duplicate.duplicate_id
	AND old_link.deleted IS NULL
WHERE child_link.choice_id = old_link.choice_id
  AND child_link.parent_choice_id = duplicate.survivor_id
  AND child_link.deleted IS NOT NULL;

INSERT INTO question_choice_parents (id, choice_id, parent_choice_id, deleted)
SELECT DISTINCT
	   left(translate(encode(sha256(convert_to('question_choice_parent:' || old_link.choice_id || ':' || duplicate.survivor_id, 'UTF8')), 'base64'), '+/', '-_'), 11),
	   old_link.choice_id,
	   duplicate.survivor_id,
	   NULL::timestamptz
FROM choice_duplicate AS duplicate
JOIN question_choice_parents AS old_link
	ON old_link.parent_choice_id = duplicate.duplicate_id
	AND old_link.deleted IS NULL
ON CONFLICT DO NOTHING;

UPDATE question_choice_parents AS old_link
SET deleted = now()
FROM choice_duplicate AS duplicate
WHERE old_link.parent_choice_id = duplicate.duplicate_id
  AND old_link.deleted IS NULL;
