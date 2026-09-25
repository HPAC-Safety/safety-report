-- ADR-0128: a conditional question names its single-select parent's required
-- choice by identifier, not by code, so it can follow that choice when an
-- Administrator replaces it.
--
-- Every revision's required option code becomes the identifier of the parent's
-- choice with that code. A choice is never erased and its code never changes,
-- so every code a revision names has its row; one that does not is a fault in
-- the data, and the migration stops rather than drop a condition silently.
-- depends_on_option_code is dropped only after this succeeds, so nothing it
-- held is lost.

UPDATE question_revisions AS revision
SET depends_on_choice_id = choice.id
FROM question_choices AS choice
WHERE revision.depends_on_option_code IS NOT NULL
  AND choice.question_id = revision.depends_on_question_id
  AND choice.code = revision.depends_on_option_code;

DO $$
DECLARE
	unresolved integer;
BEGIN
	SELECT count(*)
	INTO unresolved
	FROM question_revisions
	WHERE depends_on_option_code IS NOT NULL
	  AND depends_on_choice_id IS NULL;

	IF unresolved > 0 THEN
		RAISE EXCEPTION
			'% question revision(s) name a required option code their parent question has no choice for. The migration was rolled back; nothing changed.',
			unresolved;
	END IF;
END
$$;
