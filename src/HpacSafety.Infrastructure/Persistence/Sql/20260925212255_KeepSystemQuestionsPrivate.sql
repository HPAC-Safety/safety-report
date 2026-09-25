-- Makes every system question private (#450).
--
-- The publication-consent question was seeded from the Typeform form as not
-- private. Its answer is not an occurrence fact: the Worker now leaves every
-- consent answer out of summary input by its question's role, and privacy is
-- the second guard. The domain now refuses an edit that makes a system question
-- non-private, so this brings a database seeded before that rule in line.
--
-- A system question revises in place, answered or not, because it can never be
-- deleted: the change is a new revision of the same question, carrying every
-- other field forward, and every answer keeps the revision it was given to. A
-- question whose current revision is already private is left alone, so a
-- re-run is a no-op. The new revision's id is derived from the key in the
-- TinyId alphabet (base64url), so the same database state always produces the
-- same row.
DO $$
DECLARE
	made_private_at constant timestamptz := now();
	live questions%ROWTYPE;
	current_revision question_revisions%ROWTYPE;
BEGIN
	FOR live IN SELECT * FROM questions WHERE is_system AND deleted IS NULL
	LOOP
		SELECT * INTO current_revision
		FROM question_revisions
		WHERE question_id = live.id
		  AND deleted IS NULL
		ORDER BY revision_number DESC
		LIMIT 1;

		IF NOT FOUND OR current_revision.is_private THEN
			CONTINUE;
		END IF;

		INSERT INTO question_revisions
			(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
			 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_translatable, is_active,
			 display_order, depends_on_question_id, depends_on_option_code, grouped_under_question_id, created_at, deleted)
		VALUES
			(left(translate(encode(sha256(convert_to('question_revision:' || live.key || ':private', 'UTF8')), 'base64'), '+/', '-_'), 11),
			 live.id, current_revision.revision_number + 1, current_revision.type,
			 current_revision.label_en, current_revision.label_fr, current_revision.help_text_en, current_revision.help_text_fr,
			 current_revision.placeholder_en, current_revision.placeholder_fr, current_revision.is_system,
			 current_revision.is_required, TRUE, current_revision.is_translatable,
			 current_revision.is_active, current_revision.display_order, current_revision.depends_on_question_id,
			 current_revision.depends_on_option_code, current_revision.grouped_under_question_id, made_private_at, NULL);
	END LOOP;
END
$$;
