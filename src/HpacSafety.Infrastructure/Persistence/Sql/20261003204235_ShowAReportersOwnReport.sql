-- A reporter's browser sees its own report before it is published (issue #820,
-- ADR-0196). The migration has just added reports.receipt_hash, the SHA-256 of a
-- random receipt the submitting browser keeps; this file holds the rest.
--
-- 1. receipt_hash is locked with the reporter's other columns (ADR-0178): written
--    when the report is inserted and never changed. Both the function and the
--    trigger's column list name it.
CREATE OR REPLACE FUNCTION enforce_reports_immutability() RETURNS trigger
    LANGUAGE plpgsql
AS
$$
DECLARE
    changed_column text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'reports rows are never deleted' USING ERRCODE = '23000';
    END IF;

    FOREACH changed_column IN ARRAY ARRAY [
        'id', 'language', 'submitted_at', 'consent_publish', 'consent_media', 'consent_documents', 'receipt_hash']
        LOOP
            IF to_jsonb(NEW) -> changed_column IS DISTINCT FROM to_jsonb(OLD) -> changed_column THEN
                RAISE EXCEPTION 'reports.% cannot be changed after submission', changed_column
                    USING ERRCODE = '23000';
            END IF;
        END LOOP;

    RETURN NEW;
END
$$;

DROP TRIGGER reports_immutable ON reports;

CREATE TRIGGER reports_immutable
    BEFORE UPDATE OF id, language, submitted_at, consent_publish, consent_media, consent_documents, receipt_hash
        OR DELETE
    ON reports
    FOR EACH ROW
EXECUTE FUNCTION enforce_reports_immutability();

-- 2. The media rule, stated once. consented_report_media is every file the
--    reporter's consent allows to be shown — a verified image or video
--    derivative under media consent, a validated document under documents
--    consent — that is not deleted, hidden, or failed. It does not say whether
--    the report is public. public_report_media reads it for a published report
--    and own_report_media for the holder's own, so the two can never disagree
--    about a file. The columns of public_report_media are unchanged.
CREATE VIEW consented_report_media AS
SELECT file.id COLLATE "C"        AS id,
       file.report_id COLLATE "C" AS report_id,
       file.kind,
       file.content_type,
       file.stripped_blob_key,
       NULL::varchar(512)         AS document_blob_key,
       file.uploaded_at
FROM report_files AS file
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
         JOIN reports AS source ON source.id = file.report_id
WHERE source.consent_documents IS TRUE
  AND file.kind = 'document'
  AND file.deleted IS NULL
  AND file.hidden_at IS NULL
  AND file.processing_error_code IS NULL
  AND file.validated_at IS NOT NULL;

CREATE OR REPLACE VIEW public_report_media AS
SELECT media.id,
       media.report_id,
       media.kind,
       media.content_type,
       media.stripped_blob_key,
       media.document_blob_key,
       media.uploaded_at
FROM consented_report_media AS media
         JOIN public_reports AS report ON report.id = media.report_id;

-- 3. own_reports: what the browser holding a report's receipt may see of it. A
--    report is the holder's own when it is not deleted, was filed with a receipt,
--    and is not currently public (not a row of public_reports). The caller
--    compares receipt_hash; this view never returns a report to anyone without
--    that match. The summary is the latest live revision, approved or not, and
--    only when the reporter consented to publication — a report without consent
--    never has one. submitted_at is for the holder alone; the public feed still
--    never exposes it (ADR-0153).
CREATE VIEW own_reports AS
SELECT report.id COLLATE "C"               AS id,
       report.receipt_hash,
       report.submitted_at,
       report.consent_publish IS TRUE      AS for_publication,
       report.language,
       summary.ai_summary_en,
       summary.ai_summary_fr,
       (SELECT count(*)::integer
        FROM consented_report_media AS media
        WHERE media.report_id = report.id
          AND report.consent_publish IS TRUE) AS attachment_count
FROM reports AS report
         LEFT JOIN latest_summary_revisions AS summary
                   ON summary.report_id = report.id
                       AND report.consent_publish IS TRUE
WHERE report.deleted IS NULL
  AND report.receipt_hash IS NOT NULL
  AND NOT EXISTS (SELECT 1
                  FROM public_reports AS published
                  WHERE published.id = report.id);

-- 4. own_report_media: the same files public_report_media will list once the
--    report is public, for a report that is still the holder's own.
CREATE VIEW own_report_media AS
SELECT media.id,
       media.report_id,
       media.kind,
       media.content_type,
       media.stripped_blob_key,
       media.document_blob_key,
       media.uploaded_at
FROM consented_report_media AS media
         JOIN own_reports AS report ON report.id = media.report_id
WHERE report.for_publication;
