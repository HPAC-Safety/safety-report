-- Makes the seeded Country question a pick list of countries, with Canada and the
-- United States pinned first, and makes Province follow it (issue #750, ADR-0186).
--
-- Country was a yes/no question, "Did the occurrence happen in Canada?". It becomes
-- an optional single-select offering every ISO 3166-1 country (249 entries). Each
-- choice's code is its alpha-2 code in the lowercase form QuestionKey.Normalize
-- gives every choice code (`ca`, `us`). The wording is CLDR's region names in
-- en-CA and fr-CA, with the first letter of each name capitalised, frozen below:
--   source:   Node v26.8.2 `new Intl.DisplayNames([locale], { type: 'region' })`,
--             ICU 78.3, CLDR 48.0, Unicode 17.0, locales en-CA and fr-CA
--   ordering: display_order follows the English names; it is not read (ADR-0136).
-- Canada and the United States are pinned first; every other choice is not pinned.
-- Both labels are recorded as written by a person ('human'): CLDR names are not
-- machine translation by the Worker.
--
-- It applies the rule an Administrator's edit does (ADR-0071):
--   * a question no answer references gets a new revision and keeps its id;
--   * an answered question is stamped deleted and replaced by a new question with
--     the same key and every choice (ADR-0095), so each old yes/no answer keeps
--     the wording it was given under. Old answers are left exactly as given; none
--     is back-filled or rewritten.
-- Answers on deleted reports count, as they do for an Administrator's edit.
--
-- Province is made conditional on Country = Canada (ADR-0074, ADR-0128), by the
-- same two paths, when it is live, single-select, and unconditional. Any other
-- live question whose current revision is conditional on the old yes/no Country
-- ("answered yes") is moved to the same condition: "Canada" is exactly what a yes
-- meant, and a yes/no condition left on a single-select parent would never be met.
-- Each one is reported by RAISE NOTICE.
-- A reporter who leaves Country blank is never asked Province.
--
-- It changes nothing unless the live Country question is still a yes/no question
-- with the seeded wording, in both languages. A database seeded with the new shape,
-- or one an Administrator has already changed, is left alone, so a re-run is a
-- no-op. Nothing is deleted (AGENTS.md invariant 8).
--
-- New ids are derived from the key in the TinyId alphabet (base64url), so the same
-- database state always produces the same rows.
CREATE FUNCTION pg_temp.tiny_id(seed text) RETURNS char(11)
LANGUAGE sql IMMUTABLE AS $$
	SELECT left(translate(encode(sha256(convert_to(seed, 'UTF8')), 'base64'), '+/', '-_'), 11)::char(11)
$$;

-- Makes one question conditional on Canada: a new revision when no answer references
-- it, otherwise a fork with the same key, carrying every choice, replaced-by and
-- merged-into link, and parent link, and taking over the questions whose choices
-- depend on it (ADR-0151).
CREATE FUNCTION pg_temp.condition_on_canada(
	target_id char(11), country_id char(11), canada_id char(11), changed_at timestamptz)
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
			(pg_temp.tiny_id('question_revision:' || target.key || ':country_condition:' || (current_revision.revision_number + 1)),
			 target_id, current_revision.revision_number + 1, current_revision.type,
			 current_revision.label_en, current_revision.label_fr, current_revision.help_text_en,
			 current_revision.help_text_fr, current_revision.placeholder_en, current_revision.placeholder_fr,
			 current_revision.is_system, current_revision.is_required, current_revision.is_private,
			 current_revision.is_active, current_revision.display_order, country_id, canada_id,
			 current_revision.grouped_under_question_id, current_revision.is_translatable,
			 current_revision.allow_future_dates, changed_at, NULL);
		RETURN;
	END IF;

	replacement_id := pg_temp.tiny_id('question:' || target.key || ':country_condition');

	UPDATE questions SET deleted = changed_at WHERE id = target_id;

	INSERT INTO questions (id, key, is_system, role, created_at, deleted, choices_depend_on_question_id)
	VALUES (replacement_id, target.key, FALSE, target.role, changed_at, NULL, target.choices_depend_on_question_id);

	INSERT INTO question_revisions
		(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
		 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_active, display_order,
		 depends_on_question_id, depends_on_choice_id, grouped_under_question_id, is_translatable,
		 allow_future_dates, created_at, deleted)
	VALUES
		(pg_temp.tiny_id('question_revision:' || target.key || ':country_condition:1'),
		 replacement_id, 1, current_revision.type, current_revision.label_en, current_revision.label_fr,
		 current_revision.help_text_en, current_revision.help_text_fr, current_revision.placeholder_en,
		 current_revision.placeholder_fr, FALSE, current_revision.is_required, current_revision.is_private,
		 current_revision.is_active, current_revision.display_order, country_id, canada_id,
		 current_revision.grouped_under_question_id, current_revision.is_translatable,
		 current_revision.allow_future_dates, changed_at, NULL);

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
	country_key constant text := '2a401575_8cf4_4c87_b8df_95798d07a772';
	province_key constant text := '849ea0c4_b36e_44a7_936e_e578967907a3';
	changed_at constant timestamptz := now();
	live questions%ROWTYPE;
	current_revision question_revisions%ROWTYPE;
	country_id char(11);
	canada_id char(11);
	dependent record;
BEGIN
	SELECT * INTO live FROM questions WHERE key = country_key AND deleted IS NULL;
	IF NOT FOUND THEN
		RAISE NOTICE 'Country question is not live; nothing to convert.';
		RETURN;
	END IF;

	SELECT * INTO current_revision
	FROM question_revisions
	WHERE question_id = live.id
	ORDER BY revision_number DESC
	LIMIT 1;

	IF current_revision.type IS DISTINCT FROM 'yes_no'
		OR current_revision.label_en IS DISTINCT FROM 'Country'
		OR current_revision.label_fr IS DISTINCT FROM 'Pays'
		OR current_revision.help_text_en IS DISTINCT FROM 'Did the occurrence happen in Canada?'
		OR current_revision.help_text_fr IS DISTINCT FROM 'L''évènement a-t-il eu lieu au Canada?'
	THEN
		RAISE NOTICE 'Country question no longer reads as seeded; left alone.';
		RETURN;
	END IF;

	IF NOT EXISTS (SELECT 1 FROM report_answers WHERE question_id = live.id) THEN
		country_id := live.id;

		INSERT INTO question_revisions
			(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
			 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_active, display_order,
			 depends_on_question_id, depends_on_choice_id, grouped_under_question_id, is_translatable,
			 allow_future_dates, created_at, deleted)
		VALUES
			(pg_temp.tiny_id('question_revision:' || country_key || ':country_pick_list'),
			 live.id, current_revision.revision_number + 1, 'single_select',
			 'Country', 'Pays', 'Country where the occurrence happened.', 'Pays où l''évènement a eu lieu.',
			 current_revision.placeholder_en, current_revision.placeholder_fr, current_revision.is_system,
			 current_revision.is_required, current_revision.is_private, current_revision.is_active,
			 current_revision.display_order, current_revision.depends_on_question_id,
			 current_revision.depends_on_choice_id, current_revision.grouped_under_question_id, FALSE, FALSE,
			 changed_at, NULL);
	ELSE
		country_id := pg_temp.tiny_id('question:' || country_key || ':country_pick_list');

		UPDATE questions SET deleted = changed_at WHERE id = live.id;

		INSERT INTO questions (id, key, is_system, role, created_at, deleted, choices_depend_on_question_id)
		VALUES (country_id, live.key, FALSE, live.role, changed_at, NULL, live.choices_depend_on_question_id);

		INSERT INTO question_revisions
			(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
			 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_active, display_order,
			 depends_on_question_id, depends_on_choice_id, grouped_under_question_id, is_translatable,
			 allow_future_dates, created_at, deleted)
		VALUES
			(pg_temp.tiny_id('question_revision:' || country_key || ':country_pick_list:1'),
			 country_id, 1, 'single_select',
			 'Country', 'Pays', 'Country where the occurrence happened.', 'Pays où l''évènement a eu lieu.',
			 current_revision.placeholder_en, current_revision.placeholder_fr, FALSE,
			 current_revision.is_required, current_revision.is_private, current_revision.is_active,
			 current_revision.display_order, current_revision.depends_on_question_id,
			 current_revision.depends_on_choice_id, current_revision.grouped_under_question_id, FALSE, FALSE,
			 changed_at, NULL);
	END IF;

	INSERT INTO question_choices
		(id, question_id, code, display_order, label_en, label_fr, label_en_source, label_fr_source,
		 added_by_reporter, reporter_locale, pin, created_at, needs_review, deleted)
	SELECT
		pg_temp.tiny_id('question_choice:' || country_id || ':' || country.code),
		country_id, country.code, country.display_order, country.label_en, country.label_fr, 'human', 'human',
		FALSE, NULL, country.pin, changed_at, FALSE, NULL
	FROM (VALUES
		('af', 0, 'Afghanistan', 'Afghanistan', 'none'),
		('ax', 1, 'Åland Islands', 'Îles d’Åland', 'none'),
		('al', 2, 'Albania', 'Albanie', 'none'),
		('dz', 3, 'Algeria', 'Algérie', 'none'),
		('as', 4, 'American Samoa', 'Samoa américaines', 'none'),
		('ad', 5, 'Andorra', 'Andorre', 'none'),
		('ao', 6, 'Angola', 'Angola', 'none'),
		('ai', 7, 'Anguilla', 'Anguilla', 'none'),
		('aq', 8, 'Antarctica', 'Antarctique', 'none'),
		('ag', 9, 'Antigua and Barbuda', 'Antigua-et-Barbuda', 'none'),
		('ar', 10, 'Argentina', 'Argentine', 'none'),
		('am', 11, 'Armenia', 'Arménie', 'none'),
		('aw', 12, 'Aruba', 'Aruba', 'none'),
		('au', 13, 'Australia', 'Australie', 'none'),
		('at', 14, 'Austria', 'Autriche', 'none'),
		('az', 15, 'Azerbaijan', 'Azerbaïdjan', 'none'),
		('bs', 16, 'Bahamas', 'Bahamas', 'none'),
		('bh', 17, 'Bahrain', 'Bahreïn', 'none'),
		('bd', 18, 'Bangladesh', 'Bangladesh', 'none'),
		('bb', 19, 'Barbados', 'Barbade', 'none'),
		('by', 20, 'Belarus', 'Bélarus', 'none'),
		('be', 21, 'Belgium', 'Belgique', 'none'),
		('bz', 22, 'Belize', 'Bélize', 'none'),
		('bj', 23, 'Benin', 'Bénin', 'none'),
		('bm', 24, 'Bermuda', 'Bermudes', 'none'),
		('bt', 25, 'Bhutan', 'Bhoutan', 'none'),
		('bo', 26, 'Bolivia', 'Bolivie', 'none'),
		('ba', 27, 'Bosnia and Herzegovina', 'Bosnie-Herzégovine', 'none'),
		('bw', 28, 'Botswana', 'Botswana', 'none'),
		('bv', 29, 'Bouvet Island', 'Île Bouvet', 'none'),
		('br', 30, 'Brazil', 'Brésil', 'none'),
		('io', 31, 'British Indian Ocean Territory', 'Territoire britannique de l’océan Indien', 'none'),
		('vg', 32, 'British Virgin Islands', 'Îles Vierges britanniques', 'none'),
		('bn', 33, 'Brunei', 'Brunéi', 'none'),
		('bg', 34, 'Bulgaria', 'Bulgarie', 'none'),
		('bf', 35, 'Burkina Faso', 'Burkina Faso', 'none'),
		('bi', 36, 'Burundi', 'Burundi', 'none'),
		('kh', 37, 'Cambodia', 'Cambodge', 'none'),
		('cm', 38, 'Cameroon', 'Cameroun', 'none'),
		('ca', 39, 'Canada', 'Canada', 'first'),
		('cv', 40, 'Cape Verde', 'Cap-Vert', 'none'),
		('bq', 41, 'Caribbean Netherlands', 'Pays-Bas caribéens', 'none'),
		('ky', 42, 'Cayman Islands', 'Îles Caïmans', 'none'),
		('cf', 43, 'Central African Republic', 'République centrafricaine', 'none'),
		('td', 44, 'Chad', 'Tchad', 'none'),
		('cl', 45, 'Chile', 'Chili', 'none'),
		('cn', 46, 'China', 'Chine', 'none'),
		('cx', 47, 'Christmas Island', 'Île Christmas', 'none'),
		('cc', 48, 'Cocos (Keeling) Islands', 'Îles Cocos (Keeling)', 'none'),
		('co', 49, 'Colombia', 'Colombie', 'none'),
		('km', 50, 'Comoros', 'Comores', 'none'),
		('cg', 51, 'Congo - Brazzaville', 'Congo-Brazzaville', 'none'),
		('cd', 52, 'Congo - Kinshasa', 'Congo-Kinshasa', 'none'),
		('ck', 53, 'Cook Islands', 'Îles Cook', 'none'),
		('cr', 54, 'Costa Rica', 'Costa Rica', 'none'),
		('ci', 55, 'Côte d’Ivoire', 'Côte d’Ivoire', 'none'),
		('hr', 56, 'Croatia', 'Croatie', 'none'),
		('cu', 57, 'Cuba', 'Cuba', 'none'),
		('cw', 58, 'Curaçao', 'Curaçao', 'none'),
		('cy', 59, 'Cyprus', 'Chypre', 'none'),
		('cz', 60, 'Czechia', 'Tchéquie', 'none'),
		('dk', 61, 'Denmark', 'Danemark', 'none'),
		('dj', 62, 'Djibouti', 'Djibouti', 'none'),
		('dm', 63, 'Dominica', 'Dominique', 'none'),
		('do', 64, 'Dominican Republic', 'République dominicaine', 'none'),
		('ec', 65, 'Ecuador', 'Équateur', 'none'),
		('eg', 66, 'Egypt', 'Égypte', 'none'),
		('sv', 67, 'El Salvador', 'Salvador', 'none'),
		('gq', 68, 'Equatorial Guinea', 'Guinée équatoriale', 'none'),
		('er', 69, 'Eritrea', 'Érythrée', 'none'),
		('ee', 70, 'Estonia', 'Estonie', 'none'),
		('sz', 71, 'Eswatini', 'Eswatini', 'none'),
		('et', 72, 'Ethiopia', 'Éthiopie', 'none'),
		('fk', 73, 'Falkland Islands', 'Îles Malouines', 'none'),
		('fo', 74, 'Faroe Islands', 'Îles Féroé', 'none'),
		('fj', 75, 'Fiji', 'Fidji', 'none'),
		('fi', 76, 'Finland', 'Finlande', 'none'),
		('fr', 77, 'France', 'France', 'none'),
		('gf', 78, 'French Guiana', 'Guyane française', 'none'),
		('pf', 79, 'French Polynesia', 'Polynésie française', 'none'),
		('tf', 80, 'French Southern Territories', 'Terres australes françaises', 'none'),
		('ga', 81, 'Gabon', 'Gabon', 'none'),
		('gm', 82, 'Gambia', 'Gambie', 'none'),
		('ge', 83, 'Georgia', 'Géorgie', 'none'),
		('de', 84, 'Germany', 'Allemagne', 'none'),
		('gh', 85, 'Ghana', 'Ghana', 'none'),
		('gi', 86, 'Gibraltar', 'Gibraltar', 'none'),
		('gr', 87, 'Greece', 'Grèce', 'none'),
		('gl', 88, 'Greenland', 'Groenland', 'none'),
		('gd', 89, 'Grenada', 'Grenade', 'none'),
		('gp', 90, 'Guadeloupe', 'Guadeloupe', 'none'),
		('gu', 91, 'Guam', 'Guam', 'none'),
		('gt', 92, 'Guatemala', 'Guatemala', 'none'),
		('gg', 93, 'Guernsey', 'Guernesey', 'none'),
		('gn', 94, 'Guinea', 'Guinée', 'none'),
		('gw', 95, 'Guinea-Bissau', 'Guinée-Bissau', 'none'),
		('gy', 96, 'Guyana', 'Guyana', 'none'),
		('ht', 97, 'Haiti', 'Haïti', 'none'),
		('hm', 98, 'Heard and McDonald Islands', 'Îles Heard et McDonald', 'none'),
		('hn', 99, 'Honduras', 'Honduras', 'none'),
		('hk', 100, 'Hong Kong SAR China', 'R.A.S. chinoise de Hong Kong', 'none'),
		('hu', 101, 'Hungary', 'Hongrie', 'none'),
		('is', 102, 'Iceland', 'Islande', 'none'),
		('in', 103, 'India', 'Inde', 'none'),
		('id', 104, 'Indonesia', 'Indonésie', 'none'),
		('ir', 105, 'Iran', 'Iran', 'none'),
		('iq', 106, 'Iraq', 'Irak', 'none'),
		('ie', 107, 'Ireland', 'Irlande', 'none'),
		('im', 108, 'Isle of Man', 'Île de Man', 'none'),
		('il', 109, 'Israel', 'Israël', 'none'),
		('it', 110, 'Italy', 'Italie', 'none'),
		('jm', 111, 'Jamaica', 'Jamaïque', 'none'),
		('jp', 112, 'Japan', 'Japon', 'none'),
		('je', 113, 'Jersey', 'Jersey', 'none'),
		('jo', 114, 'Jordan', 'Jordanie', 'none'),
		('kz', 115, 'Kazakhstan', 'Kazakhstan', 'none'),
		('ke', 116, 'Kenya', 'Kenya', 'none'),
		('ki', 117, 'Kiribati', 'Kiribati', 'none'),
		('kw', 118, 'Kuwait', 'Koweït', 'none'),
		('kg', 119, 'Kyrgyzstan', 'Kirghizistan', 'none'),
		('la', 120, 'Laos', 'Laos', 'none'),
		('lv', 121, 'Latvia', 'Lettonie', 'none'),
		('lb', 122, 'Lebanon', 'Liban', 'none'),
		('ls', 123, 'Lesotho', 'Lesotho', 'none'),
		('lr', 124, 'Liberia', 'Libéria', 'none'),
		('ly', 125, 'Libya', 'Libye', 'none'),
		('li', 126, 'Liechtenstein', 'Liechtenstein', 'none'),
		('lt', 127, 'Lithuania', 'Lituanie', 'none'),
		('lu', 128, 'Luxembourg', 'Luxembourg', 'none'),
		('mo', 129, 'Macao SAR China', 'R.A.S. chinoise de Macao', 'none'),
		('mg', 130, 'Madagascar', 'Madagascar', 'none'),
		('mw', 131, 'Malawi', 'Malawi', 'none'),
		('my', 132, 'Malaysia', 'Malaisie', 'none'),
		('mv', 133, 'Maldives', 'Maldives', 'none'),
		('ml', 134, 'Mali', 'Mali', 'none'),
		('mt', 135, 'Malta', 'Malte', 'none'),
		('mh', 136, 'Marshall Islands', 'Îles Marshall', 'none'),
		('mq', 137, 'Martinique', 'Martinique', 'none'),
		('mr', 138, 'Mauritania', 'Mauritanie', 'none'),
		('mu', 139, 'Mauritius', 'Maurice', 'none'),
		('yt', 140, 'Mayotte', 'Mayotte', 'none'),
		('mx', 141, 'Mexico', 'Mexique', 'none'),
		('fm', 142, 'Micronesia', 'Micronésie', 'none'),
		('md', 143, 'Moldova', 'Moldavie', 'none'),
		('mc', 144, 'Monaco', 'Monaco', 'none'),
		('mn', 145, 'Mongolia', 'Mongolie', 'none'),
		('me', 146, 'Montenegro', 'Monténégro', 'none'),
		('ms', 147, 'Montserrat', 'Montserrat', 'none'),
		('ma', 148, 'Morocco', 'Maroc', 'none'),
		('mz', 149, 'Mozambique', 'Mozambique', 'none'),
		('mm', 150, 'Myanmar (Burma)', 'Myanmar', 'none'),
		('na', 151, 'Namibia', 'Namibie', 'none'),
		('nr', 152, 'Nauru', 'Nauru', 'none'),
		('np', 153, 'Nepal', 'Népal', 'none'),
		('nl', 154, 'Netherlands', 'Pays-Bas', 'none'),
		('nc', 155, 'New Caledonia', 'Nouvelle-Calédonie', 'none'),
		('nz', 156, 'New Zealand', 'Nouvelle-Zélande', 'none'),
		('ni', 157, 'Nicaragua', 'Nicaragua', 'none'),
		('ne', 158, 'Niger', 'Niger', 'none'),
		('ng', 159, 'Nigeria', 'Nigéria', 'none'),
		('nu', 160, 'Niue', 'Niue', 'none'),
		('nf', 161, 'Norfolk Island', 'Île Norfolk', 'none'),
		('kp', 162, 'North Korea', 'Corée du Nord', 'none'),
		('mk', 163, 'North Macedonia', 'Macédoine du Nord', 'none'),
		('mp', 164, 'Northern Mariana Islands', 'Mariannes du Nord', 'none'),
		('no', 165, 'Norway', 'Norvège', 'none'),
		('om', 166, 'Oman', 'Oman', 'none'),
		('pk', 167, 'Pakistan', 'Pakistan', 'none'),
		('pw', 168, 'Palau', 'Palaos', 'none'),
		('ps', 169, 'Palestinian territories', 'Territoires palestiniens', 'none'),
		('pa', 170, 'Panama', 'Panama', 'none'),
		('pg', 171, 'Papua New Guinea', 'Papouasie-Nouvelle-Guinée', 'none'),
		('py', 172, 'Paraguay', 'Paraguay', 'none'),
		('pe', 173, 'Peru', 'Pérou', 'none'),
		('ph', 174, 'Philippines', 'Philippines', 'none'),
		('pn', 175, 'Pitcairn Islands', 'Îles Pitcairn', 'none'),
		('pl', 176, 'Poland', 'Pologne', 'none'),
		('pt', 177, 'Portugal', 'Portugal', 'none'),
		('pr', 178, 'Puerto Rico', 'Porto Rico', 'none'),
		('qa', 179, 'Qatar', 'Qatar', 'none'),
		('re', 180, 'Réunion', 'La Réunion', 'none'),
		('ro', 181, 'Romania', 'Roumanie', 'none'),
		('ru', 182, 'Russia', 'Russie', 'none'),
		('rw', 183, 'Rwanda', 'Rwanda', 'none'),
		('sh', 184, 'Saint Helena', 'Sainte-Hélène', 'none'),
		('kn', 185, 'Saint Kitts and Nevis', 'Saint‑Kitts‑et‑Nevis', 'none'),
		('lc', 186, 'Saint Lucia', 'Sainte-Lucie', 'none'),
		('mf', 187, 'Saint Martin', 'Saint-Martin (France)', 'none'),
		('vc', 188, 'Saint Vincent and the Grenadines', 'Saint-Vincent-et-les Grenadines', 'none'),
		('bl', 189, 'Saint-Barthélemy', 'Saint-Barthélemy', 'none'),
		('pm', 190, 'Saint-Pierre-et-Miquelon', 'Saint-Pierre-et-Miquelon', 'none'),
		('ws', 191, 'Samoa', 'Samoa', 'none'),
		('sm', 192, 'San Marino', 'Saint-Marin', 'none'),
		('st', 193, 'São Tomé and Príncipe', 'Sao Tomé-et-Principe', 'none'),
		('sa', 194, 'Saudi Arabia', 'Arabie saoudite', 'none'),
		('sn', 195, 'Senegal', 'Sénégal', 'none'),
		('rs', 196, 'Serbia', 'Serbie', 'none'),
		('sc', 197, 'Seychelles', 'Seychelles', 'none'),
		('sl', 198, 'Sierra Leone', 'Sierra Leone', 'none'),
		('sg', 199, 'Singapore', 'Singapour', 'none'),
		('sx', 200, 'Sint Maarten', 'Saint-Martin (Pays-Bas)', 'none'),
		('sk', 201, 'Slovakia', 'Slovaquie', 'none'),
		('si', 202, 'Slovenia', 'Slovénie', 'none'),
		('sb', 203, 'Solomon Islands', 'Îles Salomon', 'none'),
		('so', 204, 'Somalia', 'Somalie', 'none'),
		('za', 205, 'South Africa', 'Afrique du Sud', 'none'),
		('gs', 206, 'South Georgia and South Sandwich Islands', 'Géorgie du Sud-et-les Îles Sandwich du Sud', 'none'),
		('kr', 207, 'South Korea', 'Corée du Sud', 'none'),
		('ss', 208, 'South Sudan', 'Soudan du Sud', 'none'),
		('es', 209, 'Spain', 'Espagne', 'none'),
		('lk', 210, 'Sri Lanka', 'Sri Lanka', 'none'),
		('sd', 211, 'Sudan', 'Soudan', 'none'),
		('sr', 212, 'Suriname', 'Suriname', 'none'),
		('sj', 213, 'Svalbard and Jan Mayen', 'Svalbard et Jan Mayen', 'none'),
		('se', 214, 'Sweden', 'Suède', 'none'),
		('ch', 215, 'Switzerland', 'Suisse', 'none'),
		('sy', 216, 'Syria', 'Syrie', 'none'),
		('tw', 217, 'Taiwan', 'Taïwan', 'none'),
		('tj', 218, 'Tajikistan', 'Tadjikistan', 'none'),
		('tz', 219, 'Tanzania', 'Tanzanie', 'none'),
		('th', 220, 'Thailand', 'Thaïlande', 'none'),
		('tl', 221, 'Timor-Leste', 'Timor-Leste', 'none'),
		('tg', 222, 'Togo', 'Togo', 'none'),
		('tk', 223, 'Tokelau', 'Tokelau', 'none'),
		('to', 224, 'Tonga', 'Tonga', 'none'),
		('tt', 225, 'Trinidad and Tobago', 'Trinité-et-Tobago', 'none'),
		('tn', 226, 'Tunisia', 'Tunisie', 'none'),
		('tr', 227, 'Türkiye', 'Turquie', 'none'),
		('tm', 228, 'Turkmenistan', 'Turkménistan', 'none'),
		('tc', 229, 'Turks and Caicos Islands', 'Îles Turques-et-Caïques', 'none'),
		('tv', 230, 'Tuvalu', 'Tuvalu', 'none'),
		('ug', 231, 'Uganda', 'Ouganda', 'none'),
		('ua', 232, 'Ukraine', 'Ukraine', 'none'),
		('ae', 233, 'United Arab Emirates', 'Émirats arabes unis', 'none'),
		('gb', 234, 'United Kingdom', 'Royaume-Uni', 'none'),
		('us', 235, 'United States', 'États-Unis', 'first'),
		('uy', 236, 'Uruguay', 'Uruguay', 'none'),
		('um', 237, 'US Outlying Islands', 'Îles mineures éloignées des États-Unis', 'none'),
		('vi', 238, 'US Virgin Islands', 'Îles Vierges américaines', 'none'),
		('uz', 239, 'Uzbekistan', 'Ouzbékistan', 'none'),
		('vu', 240, 'Vanuatu', 'Vanuatu', 'none'),
		('va', 241, 'Vatican City', 'Cité du Vatican', 'none'),
		('ve', 242, 'Venezuela', 'Vénézuéla', 'none'),
		('vn', 243, 'Vietnam', 'Vietnam', 'none'),
		('wf', 244, 'Wallis and Futuna', 'Wallis-et-Futuna', 'none'),
		('eh', 245, 'Western Sahara', 'Sahara occidental', 'none'),
		('ye', 246, 'Yemen', 'Yémen', 'none'),
		('zm', 247, 'Zambia', 'Zambie', 'none'),
		('zw', 248, 'Zimbabwe', 'Zimbabwe', 'none')
	) AS country(code, display_order, label_en, label_fr, pin);

	SELECT id INTO canada_id FROM question_choices WHERE question_id = country_id AND code = 'ca';

	-- Province, and any question the old yes/no Country enabled, now needs Canada.
	FOR dependent IN
		SELECT q.id, q.key
		FROM questions q
		JOIN LATERAL (
			SELECT * FROM question_revisions r
			WHERE r.question_id = q.id
			ORDER BY r.revision_number DESC
			LIMIT 1) latest ON TRUE
		WHERE q.deleted IS NULL
			AND ((q.key = province_key AND latest.type = 'single_select' AND latest.depends_on_question_id IS NULL)
				OR (latest.depends_on_question_id = live.id AND latest.depends_on_choice_id IS NULL))
		ORDER BY q.key
	LOOP
		PERFORM pg_temp.condition_on_canada(dependent.id, country_id, canada_id, changed_at);
		RAISE NOTICE 'Question % is now shown only when Country is Canada.', dependent.key;
	END LOOP;
END $$;

DROP FUNCTION pg_temp.condition_on_canada(char, char, char, timestamptz);
DROP FUNCTION pg_temp.tiny_id(text);
