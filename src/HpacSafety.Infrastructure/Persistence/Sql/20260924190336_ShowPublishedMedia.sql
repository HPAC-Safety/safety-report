-- Shows a published report's photos and video (ADR-0117).
--
-- 1. Seeds the media-consent system question, consent_media, once. Its ids are
--    derived from the key in the TinyId alphabet (base64url), so every database
--    gets the same rows, and a database that already has a live consent_media
--    question is left alone. It is asked last, after every live question, and
--    the form shows it only when publication consent is yes and an image or
--    video is attached — a built-in rule, not an authored dependency.
--
-- 2. Creates public_report_media: every public image or video, only while its
--    report is in public_reports, so unpublishing or deleting a report empties
--    it with no further step (ADR-0116). stripped_blob_key and content_type are
--    here for the API to mint a link; neither is ever returned.
DO $$
DECLARE
	media_key constant text := 'consent_media';
	question_id char(11) := left(translate(encode(sha256(convert_to('question:' || media_key, 'UTF8')), 'base64'), '+/', '-_'), 11);
	revision_id char(11) := left(translate(encode(sha256(convert_to('question_revision:' || media_key || ':1', 'UTF8')), 'base64'), '+/', '-_'), 11);
	seeded_at constant timestamptz := now();
BEGIN
	IF EXISTS (SELECT 1 FROM questions WHERE key = media_key AND deleted IS NULL) THEN
		RETURN;
	END IF;

	INSERT INTO questions (id, key, is_system, role, created_at, deleted)
	VALUES (question_id, media_key, TRUE, 'consent_media', seeded_at, NULL);

	INSERT INTO question_revisions
		(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
		 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_translatable, is_active,
		 display_order, depends_on_question_id, depends_on_option_code, grouped_under_question_id, created_at, deleted)
	VALUES
		(revision_id, question_id, 1, 'yes_no',
		 'Photo and video consent',
		 'Autorisation des photos et vidéos',
		 'Do you agree for HPAC to show the photos and videos you attached on the published version of your report? Faces, aircraft registrations, and places can identify you or others. A safety officer can remove any of them.',
		 'Acceptez-vous que l''ACVL affiche les photos et vidéos que vous avez jointes sur la version publiée de votre signalement? Les visages, les immatriculations d''aéronefs et les lieux peuvent vous identifier ou identifier d''autres personnes. Un responsable de la sécurité peut retirer n''importe lequel d''entre eux.',
		 NULL, NULL, TRUE, TRUE, TRUE, FALSE, TRUE,
		 COALESCE((SELECT max(revision.display_order) + 1
		           FROM question_revisions AS revision
		                    JOIN questions AS question ON question.id = revision.question_id
		           WHERE question.deleted IS NULL
		             AND revision.deleted IS NULL), 0),
		 NULL, NULL, NULL, seeded_at, NULL);
END
$$;

CREATE OR REPLACE VIEW public_report_media AS
SELECT file.id COLLATE "C"        AS id,
       file.report_id COLLATE "C" AS report_id,
       file.kind,
       file.content_type,
       file.stripped_blob_key,
       file.uploaded_at
FROM report_files AS file
         JOIN public_reports AS report ON report.id = file.report_id
         JOIN reports AS source ON source.id = file.report_id
WHERE source.consent_media IS TRUE
  AND file.kind IN ('image', 'video')
  AND file.deleted IS NULL
  AND file.hidden_at IS NULL
  AND file.processing_error_code IS NULL
  AND file.stripped_blob_key IS NOT NULL
  AND file.exif_stripped_at IS NOT NULL;
