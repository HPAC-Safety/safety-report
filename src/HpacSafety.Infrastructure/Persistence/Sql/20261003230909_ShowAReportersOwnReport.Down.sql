-- Reverses 20261003204235_ShowAReportersOwnReport.sql before receipt_hash is
-- dropped. No row is touched: public_report_media goes back to its own
-- definition (ADR-0119), and the reports trigger back to its earlier column list.
DROP VIEW own_report_media;
DROP VIEW own_reports;

CREATE OR REPLACE VIEW public_report_media AS
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

DROP VIEW consented_report_media;

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
        'id', 'language', 'submitted_at', 'consent_publish', 'consent_media', 'consent_documents']
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
    BEFORE UPDATE OF id, language, submitted_at, consent_publish, consent_media, consent_documents
        OR DELETE
    ON reports
    FOR EACH ROW
EXECUTE FUNCTION enforce_reports_immutability();
