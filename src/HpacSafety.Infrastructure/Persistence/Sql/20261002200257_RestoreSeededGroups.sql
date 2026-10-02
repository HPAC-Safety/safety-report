-- Restores the group links of the seeded questions on a database created from
-- scratch (issue #754, ADR-0187).
--
-- InitialSchema writes QuestionBankSeed through the legacy table shape, which has
-- no grouping column. On a database created from scratch since the seed was filled
-- (2026-09-22), every seeded question is written there first without its group,
-- and SeedQuestionBankFromTypeformFixtures then skips each one as already present.
-- So "From", "Pilot", and "Aircraft" arrive with no questions under them, and each
-- of their questions is asked on a page of its own. A database created before the
-- seed was filled got the full seed, groups included, and is left alone below.
--
-- Each pair below is a seeded question and the group QuestionBankSeed names for it,
-- frozen here. A question is grouped under its group only when:
--   * it is live;
--   * no revision of any question with its key ever had a group, so an
--     Administrator's own grouping, or ungrouping, is never overridden; and
--   * its group is live and its current revision is still a group (ADR-0076).
--
-- It applies the rule an Administrator's edit does (ADR-0071):
--   * a question no answer references gets a new revision and keeps its id;
--   * an answered question is stamped deleted and replaced by a new question with
--     the same key and every choice (ADR-0095), so each old answer keeps the
--     revision it was given under. No answer is created, changed, or deleted.
-- Answers on deleted reports count, as they do for an Administrator's edit.
-- Each question grouped is reported by RAISE NOTICE.
--
-- A re-run is a no-op: a grouped question has a revision with a group. Nothing is
-- deleted (AGENTS.md invariant 8).
--
-- New ids are derived from the key in the TinyId alphabet (base64url), so the same
-- database state always produces the same rows.
CREATE FUNCTION pg_temp.tiny_id(seed text) RETURNS char(11)
LANGUAGE sql IMMUTABLE AS $$
	SELECT left(translate(encode(sha256(convert_to(seed, 'UTF8')), 'base64'), '+/', '-_'), 11)::char(11)
$$;

-- Groups one question under a group: a new revision when no answer references it,
-- otherwise a fork with the same key, carrying every choice, replaced-by and
-- merged-into link, and parent link, and taking over the questions whose choices
-- depend on it (ADR-0151).
CREATE FUNCTION pg_temp.group_under(target_id char(11), group_id char(11), changed_at timestamptz)
RETURNS void
LANGUAGE plpgsql AS $$
DECLARE
	target questions%ROWTYPE;
	current_revision question_revisions%ROWTYPE;
	replacement_id char(11);
BEGIN
	SELECT * INTO target FROM questions WHERE id = target_id;

	SELECT * INTO current_revision
	FROM question_revisions
	WHERE question_id = target_id
	ORDER BY revision_number DESC
	LIMIT 1;

	IF NOT EXISTS (SELECT 1 FROM report_answers WHERE question_id = target_id) THEN
		INSERT INTO question_revisions
			(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
			 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_active, display_order,
			 depends_on_question_id, depends_on_choice_id, grouped_under_question_id, is_translatable,
			 allow_future_dates, created_at, deleted)
		VALUES
			(pg_temp.tiny_id('question_revision:' || target.key || ':seeded_group:' || (current_revision.revision_number + 1)),
			 target_id, current_revision.revision_number + 1, current_revision.type,
			 current_revision.label_en, current_revision.label_fr, current_revision.help_text_en,
			 current_revision.help_text_fr, current_revision.placeholder_en, current_revision.placeholder_fr,
			 current_revision.is_system, current_revision.is_required, current_revision.is_private,
			 current_revision.is_active, current_revision.display_order, current_revision.depends_on_question_id,
			 current_revision.depends_on_choice_id, group_id, current_revision.is_translatable,
			 current_revision.allow_future_dates, changed_at, NULL);
		RETURN;
	END IF;

	replacement_id := pg_temp.tiny_id('question:' || target.key || ':seeded_group');

	UPDATE questions SET deleted = changed_at WHERE id = target_id;

	INSERT INTO questions (id, key, is_system, role, created_at, deleted, choices_depend_on_question_id)
	VALUES (replacement_id, target.key, target.is_system, target.role, changed_at, NULL, target.choices_depend_on_question_id);

	INSERT INTO question_revisions
		(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
		 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_active, display_order,
		 depends_on_question_id, depends_on_choice_id, grouped_under_question_id, is_translatable,
		 allow_future_dates, created_at, deleted)
	VALUES
		(pg_temp.tiny_id('question_revision:' || target.key || ':seeded_group:1'),
		 replacement_id, 1, current_revision.type, current_revision.label_en, current_revision.label_fr,
		 current_revision.help_text_en, current_revision.help_text_fr, current_revision.placeholder_en,
		 current_revision.placeholder_fr, current_revision.is_system, current_revision.is_required,
		 current_revision.is_private, current_revision.is_active, current_revision.display_order,
		 current_revision.depends_on_question_id, current_revision.depends_on_choice_id, group_id,
		 current_revision.is_translatable, current_revision.allow_future_dates, changed_at, NULL);

	-- Every choice crosses, removed ones included. The copies are new rows, so a
	-- replaced-by or merged-into link names the copy of its target (ADR-0128, ADR-0129).
	INSERT INTO question_choices
		(id, question_id, code, display_order, label_en, label_fr, label_en_source, label_fr_source,
		 added_by_reporter, reporter_locale, pin, created_at, needs_review, reviewed_at, reviewed_by,
		 replaced_by_choice_id, merged_into_choice_id, deleted)
	SELECT
		pg_temp.tiny_id('question_choice:' || replacement_id || ':' || o.code),
		replacement_id, o.code, o.display_order, o.label_en, o.label_fr, o.label_en_source, o.label_fr_source,
		o.added_by_reporter, o.reporter_locale, o.pin, o.created_at, o.needs_review, o.reviewed_at, o.reviewed_by,
		(SELECT pg_temp.tiny_id('question_choice:' || replacement_id || ':' || r.code)
		 FROM question_choices r WHERE r.id = o.replaced_by_choice_id),
		(SELECT pg_temp.tiny_id('question_choice:' || replacement_id || ':' || m.code)
		 FROM question_choices m WHERE m.id = o.merged_into_choice_id),
		o.deleted
	FROM question_choices o
	WHERE o.question_id = target_id;

	-- Every link to a parent choice crosses, stamped ones included.
	INSERT INTO question_choice_parents (id, choice_id, parent_choice_id, deleted)
	SELECT
		pg_temp.tiny_id('question_choice_parent:' || cp.id || ':' || link.parent_choice_id),
		cp.id, link.parent_choice_id, link.deleted
	FROM question_choice_parents link
	JOIN question_choices o ON o.id = link.choice_id AND o.question_id = target_id
	JOIN question_choices cp ON cp.question_id = replacement_id AND cp.code = o.code;

	-- Questions whose choices depend on this one now depend on the replacement, and
	-- each live link to one of its choices passes on to that choice's copy, followed
	-- through any replacement or merge (ADR-0151).
	UPDATE questions SET choices_depend_on_question_id = replacement_id
	WHERE choices_depend_on_question_id = target_id AND deleted IS NULL;

	WITH RECURSIVE today(link_id, child_id, from_parent_id, step_id) AS (
		SELECT link.id, link.choice_id, link.parent_choice_id, cp.id
		FROM question_choice_parents link
		JOIN questions child_question ON child_question.choices_depend_on_question_id = replacement_id
											AND child_question.deleted IS NULL
		JOIN question_choices child ON child.question_id = child_question.id AND child.id = link.choice_id
		JOIN question_choices o ON o.id = link.parent_choice_id AND o.question_id = target_id
		JOIN question_choices cp ON cp.question_id = replacement_id AND cp.code = o.code
		WHERE link.deleted IS NULL
		UNION ALL
		SELECT today.link_id, today.child_id, today.from_parent_id, nxt.id
		FROM today
		JOIN question_choices step ON step.id = today.step_id AND step.deleted IS NOT NULL
		JOIN question_choices nxt ON nxt.id = coalesce(step.replaced_by_choice_id, step.merged_into_choice_id)
	),
	resolved AS (
		SELECT link_id, child_id, step_id AS to_parent_id
		FROM today
		JOIN question_choices fin ON fin.id = today.step_id AND fin.deleted IS NULL
	),
	stamped AS (
		UPDATE question_choice_parents SET deleted = changed_at
		WHERE id IN (SELECT link_id FROM resolved)
		RETURNING id
	)
	INSERT INTO question_choice_parents (id, choice_id, parent_choice_id, deleted)
	SELECT pg_temp.tiny_id('question_choice_parent:' || child_id || ':' || to_parent_id), child_id, to_parent_id, NULL
	FROM resolved
	ON CONFLICT (choice_id, parent_choice_id) DO UPDATE SET deleted = NULL;
END $$;

DO $$
DECLARE
	changed_at constant timestamptz := now();
	pair record;
	child questions%ROWTYPE;
	parent questions%ROWTYPE;
	parent_type text;
BEGIN
	FOR pair IN
		SELECT * FROM (VALUES
			-- From
			('da89ae06_f229_4f38_8faa_e9c5bafef2f3', '01hd6vk528fxpra3wcnsadr5qq'),
			('3d662189_41cb_4430_9db8_7b2e4861df53', '01hd6vk528fxpra3wcnsadr5qq'),
			('65ed41c8_5fbd_4fb0_9ec3_5dabdb42d7be', '01hd6vk528fxpra3wcnsadr5qq'),
			('d4cded5e_1393_47e1_b703_d44e2865ccd8', '01hd6vk528fxpra3wcnsadr5qq'),
			-- Pilot
			('52afac6c_b30c_4bd2_a052_212fa9249a45', 'f9a22e64_b121_4dd0_b28a_6d30b9d1f7da'),
			('41c4d104_82c5_4f31_9d86_cb95e35622e4', 'f9a22e64_b121_4dd0_b28a_6d30b9d1f7da'),
			-- Aircraft
			('7590a371_c9cd_4e19_9954_be5137a669d2', 'b86820e5_114e_4266_b971_03b959e74167'),
			('7018ad9b_bc9c_4f06_9a36_5fb3eed0368c', 'b86820e5_114e_4266_b971_03b959e74167'),
			('762e8ce8_ccd3_4d1f_be09_68eb33136aa5', 'b86820e5_114e_4266_b971_03b959e74167'),
			('6879f513_fe00_4bdc_acda_138947650b3c', 'b86820e5_114e_4266_b971_03b959e74167')
		) AS seeded(child_key, group_key)
	LOOP
		SELECT * INTO child FROM questions WHERE key = pair.child_key AND deleted IS NULL;
		CONTINUE WHEN NOT FOUND;

		CONTINUE WHEN EXISTS (
			SELECT 1
			FROM question_revisions r
			JOIN questions q ON q.id = r.question_id
			WHERE q.key = pair.child_key AND r.grouped_under_question_id IS NOT NULL);

		SELECT * INTO parent FROM questions WHERE key = pair.group_key AND deleted IS NULL;
		CONTINUE WHEN NOT FOUND;

		SELECT type INTO parent_type
		FROM question_revisions
		WHERE question_id = parent.id
		ORDER BY revision_number DESC
		LIMIT 1;
		CONTINUE WHEN parent_type IS DISTINCT FROM 'group';

		PERFORM pg_temp.group_under(child.id, parent.id, changed_at);
		RAISE NOTICE 'Question % is grouped under %.', pair.child_key, pair.group_key;
	END LOOP;
END $$;
