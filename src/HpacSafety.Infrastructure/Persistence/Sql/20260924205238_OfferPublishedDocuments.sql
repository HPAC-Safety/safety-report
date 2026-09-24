-- Offers a published report's validated documents for download (ADR-0119).
--
-- 1. Rewords the media-consent system question to name documents. Like
--    publication consent it revises in place, answered or not, because it can
--    never be deleted: the new wording is a new revision of the same question,
--    and every answer keeps the revision it was given to. A report's
--    consent_documents is set only by an answer to the current revision, so a
--    yes given to an older wording never covers documents. A database whose
--    current revision already reads this way is left alone, so a re-run is a
--    no-op. An administrator's own earlier rewording is superseded too: until
--    the question names documents, no yes to it may.
--
-- 2. Recreates public_report_media to list a validated document as well, on a
--    report whose consent_documents is true. document_blob_key is the unchanged
--    original's key, there only for the API to mint a forced download; it is
--    never returned, and it is null on every image and video row.
DO $$
DECLARE
	media_key constant text := 'consent_media';
	label_en constant text := 'Photo, video, and document consent';
	label_fr constant text := 'Autorisation des photos, vidéos et documents';
	help_en constant text := 'Do you agree for HPAC to show the photos and videos you attached, and to offer the documents you attached for download, on the published version of your report? Documents are published exactly as you uploaded them and may contain personal details, such as names, medical information, or details stored inside the file. Faces, aircraft registrations, and places can identify you or others. A safety officer can remove any of them.';
	help_fr constant text := 'Acceptez-vous que l''ACVL affiche les photos et vidéos que vous avez jointes, et offre au téléchargement les documents que vous avez joints, sur la version publiée de votre signalement? Les documents sont publiés exactement tels que vous les avez téléversés et peuvent contenir des renseignements personnels, comme des noms, des renseignements médicaux ou des détails enregistrés dans le fichier. Les visages, les immatriculations d''aéronefs et les lieux peuvent vous identifier ou identifier d''autres personnes. Un responsable de la sécurité peut retirer n''importe lequel d''entre eux.';
	reworded_at constant timestamptz := now();
	live questions%ROWTYPE;
	current_revision question_revisions%ROWTYPE;
BEGIN
	SELECT * INTO live FROM questions WHERE key = media_key AND deleted IS NULL;
	IF NOT FOUND THEN
		RETURN;
	END IF;

	SELECT * INTO current_revision
	FROM question_revisions
	WHERE question_id = live.id
	  AND deleted IS NULL
	ORDER BY revision_number DESC
	LIMIT 1;

	IF current_revision.label_en = label_en
		AND current_revision.label_fr = label_fr
		AND current_revision.help_text_en = help_en
		AND current_revision.help_text_fr = help_fr
	THEN
		RETURN;
	END IF;

	INSERT INTO question_revisions
		(id, question_id, revision_number, type, label_en, label_fr, help_text_en, help_text_fr,
		 placeholder_en, placeholder_fr, is_system, is_required, is_private, is_translatable, is_active,
		 display_order, depends_on_question_id, depends_on_option_code, grouped_under_question_id, created_at, deleted)
	VALUES
		(left(translate(encode(sha256(convert_to('question_revision:' || media_key || ':documents', 'UTF8')), 'base64'), '+/', '-_'), 11),
		 live.id, current_revision.revision_number + 1, current_revision.type,
		 label_en, label_fr, help_en, help_fr,
		 current_revision.placeholder_en, current_revision.placeholder_fr, current_revision.is_system,
		 current_revision.is_required, current_revision.is_private, current_revision.is_translatable,
		 current_revision.is_active, current_revision.display_order, current_revision.depends_on_question_id,
		 current_revision.depends_on_option_code, current_revision.grouped_under_question_id, reworded_at, NULL);
END
$$;

DROP VIEW IF EXISTS public_report_media;

CREATE VIEW public_report_media AS
SELECT file.id COLLATE "C"        AS id,
       file.report_id COLLATE "C" AS report_id,
       file.kind,
       file.content_type,
       file.stripped_blob_key,
       NULL::varchar(512)         AS document_blob_key,
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
  AND file.exif_stripped_at IS NOT NULL
UNION ALL
SELECT file.id COLLATE "C"        AS id,
       file.report_id COLLATE "C" AS report_id,
       file.kind,
       file.content_type,
       NULL                       AS stripped_blob_key,
       file.blob_key              AS document_blob_key,
       file.uploaded_at
FROM report_files AS file
         JOIN public_reports AS report ON report.id = file.report_id
         JOIN reports AS source ON source.id = file.report_id
WHERE source.consent_documents IS TRUE
  AND file.kind = 'document'
  AND file.deleted IS NULL
  AND file.hidden_at IS NULL
  AND file.processing_error_code IS NULL
  AND file.validated_at IS NOT NULL;
