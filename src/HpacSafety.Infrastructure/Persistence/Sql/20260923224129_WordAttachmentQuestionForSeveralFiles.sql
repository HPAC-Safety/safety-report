-- Rewords the seeded attachment question for a form that takes several files.
-- The Typeform wording it was seeded with asked for one file and sent the rest
-- to email, which contradicts the field's own "up to 5 files" guidance.
--
-- It applies the same rule an Administrator's edit does (ADR-0071):
--   * a question no answer references gets a new revision and keeps its id;
--   * an answered question is stamped deleted and replaced by a new question
--     carrying the same key and every choice (ADR-0095), so the answers keep
--     the wording they were given under.
-- Answers on deleted reports count, as they do for an Administrator's edit.
--
-- It changes nothing unless the live question for the key still reads exactly
-- as seeded, in both languages. A database seeded with the new wording, or one
-- an Administrator has already reworded, is left alone, so a re-run is a no-op.
--
-- New ids are derived from the key in the TinyId alphabet (base64url), so the
-- same database state always produces the same rows.
DO $$
DECLARE
	seeded_key constant text := '418e72ec_1edc_4e0d_9429_af65ca564ab1';
	reworded_at constant timestamptz := now();
	live questions%ROWTYPE;
	current_revision question_revisions%ROWTYPE;
	replacement_id char(11);
BEGIN
	SELECT * INTO live FROM questions WHERE key = seeded_key AND deleted IS NULL;
	IF NOT FOUND THEN
		RETURN;
	END IF;

	SELECT * INTO current_revision
	FROM question_revisions
	WHERE question_id = live.id
	ORDER BY revision_number DESC
	LIMIT 1;

	IF current_revision.label_en IS DISTINCT FROM 'Photo or video:'
		OR current_revision.label_fr IS DISTINCT FROM 'Photo ou vidéo:'
		OR current_revision.help_text_en IS DISTINCT FROM 'Upload one photo or video of the occurrence. Please contact us directly for multiple files (safety@hpac.ca).'
		OR current_revision.help_text_fr IS DISTINCT FROM 'Téléchargez une photo ou vidéo de l''événement. Contactez-nous directement pour plusieurs fichiers (safety@hpac.ca).'
	THEN
		RETURN;
	END IF;

	IF NOT EXISTS (SELECT 1 FROM report_answers WHERE question_id = live.id) THEN
		INSERT INTO question_revisions
			(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
			 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_active, display_order,
			 depends_on_question_id, depends_on_option_code, grouped_under_question_id, created_at, deleted)
		VALUES
			(left(translate(encode(sha256(convert_to('question_revision:' || seeded_key || ':several_files', 'UTF8')), 'base64'), '+/', '-_'), 11),
			 live.id, current_revision.revision_number + 1, current_revision.type,
			 'Photos or videos:', 'Photos ou vidéos:',
			 'Upload photos, videos, or documents of the occurrence, if you have any.',
			 'Téléversez des photos, vidéos ou documents de l''événement, si vous en avez.',
			 current_revision.placeholder_en, current_revision.placeholder_fr, current_revision.is_system,
			 current_revision.is_required, current_revision.is_private, current_revision.is_active,
			 current_revision.display_order, current_revision.depends_on_question_id,
			 current_revision.depends_on_option_code, current_revision.grouped_under_question_id, reworded_at, NULL);
		RETURN;
	END IF;

	replacement_id := left(translate(encode(sha256(convert_to('question:' || seeded_key || ':several_files', 'UTF8')), 'base64'), '+/', '-_'), 11);

	UPDATE questions SET deleted = reworded_at WHERE id = live.id;

	INSERT INTO questions (id, key, is_system, role, created_at, deleted)
	VALUES (replacement_id, live.key, FALSE, live.role, reworded_at, NULL);

	INSERT INTO question_revisions
		(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
		 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_active, display_order,
		 depends_on_question_id, depends_on_option_code, grouped_under_question_id, created_at, deleted)
	VALUES
		(left(translate(encode(sha256(convert_to('question_revision:' || seeded_key || ':several_files:1', 'UTF8')), 'base64'), '+/', '-_'), 11),
		 replacement_id, 1, current_revision.type,
		 'Photos or videos:', 'Photos ou vidéos:',
		 'Upload photos, videos, or documents of the occurrence, if you have any.',
		 'Téléversez des photos, vidéos ou documents de l''événement, si vous en avez.',
		 current_revision.placeholder_en, current_revision.placeholder_fr, FALSE,
		 current_revision.is_required, current_revision.is_private, current_revision.is_active,
		 current_revision.display_order, current_revision.depends_on_question_id,
		 current_revision.depends_on_option_code, current_revision.grouped_under_question_id, reworded_at, NULL);

	INSERT INTO question_choices
		(id, question_id, code, display_order, label_en, label_fr, added_by_reporter, reporter_locale, deleted)
	SELECT
		left(translate(encode(sha256(convert_to('question_choice:' || replacement_id || ':' || code, 'UTF8')), 'base64'), '+/', '-_'), 11),
		replacement_id, code, display_order, label_en, label_fr, added_by_reporter, reporter_locale, deleted
	FROM question_choices
	WHERE question_id = live.id;
END $$;
