-- Reverses ADR-0151's fold: each choice's one live link goes back into
-- question_choices.parent_choice_id. A choice offered under several parent
-- choices has no single link to go back to, so the reversal stops rather than
-- keep one and lose the rest. The duplicates the migration merged stay merged:
-- their answers still read the survivor's wording.
DO $$
DECLARE
	several integer;
BEGIN
	SELECT count(*)
	INTO several
	FROM (
		SELECT link.choice_id
		FROM question_choice_parents AS link
		WHERE link.deleted IS NULL
		GROUP BY link.choice_id
		HAVING count(*) > 1) AS shared;

	IF several > 0 THEN
		RAISE EXCEPTION
			'% choice(s) are offered under several parent choices, which one parent_choice_id cannot hold. The reversal was rolled back; nothing changed.',
			several;
	END IF;
END
$$;

UPDATE question_choices AS choice
SET parent_choice_id = link.parent_choice_id
FROM question_choice_parents AS link
WHERE link.choice_id = choice.id
  AND link.deleted IS NULL;
