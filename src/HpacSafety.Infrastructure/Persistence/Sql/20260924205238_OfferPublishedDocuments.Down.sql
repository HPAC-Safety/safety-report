-- Reverses 20260924205238_OfferPublishedDocuments.sql before its columns are
-- dropped: public_report_media goes back to images and video only. The
-- reworded consent_media revision is kept, because a revision is never
-- physically deleted and reports may have answered it (AGENTS.md invariant 8).
DROP VIEW IF EXISTS public_report_media;

CREATE VIEW public_report_media AS
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
