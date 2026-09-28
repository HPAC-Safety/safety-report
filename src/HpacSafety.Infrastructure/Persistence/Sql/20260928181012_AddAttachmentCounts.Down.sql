-- Reverses 20260928181012_AddAttachmentCounts.sql: restores public_reports
-- (as of 20260927223122_SortPublicFeedBySubmittedAt.sql), admin_report_queue
-- (as of 20260927231919_ShowReporterAndPilotNames.sql), and
-- search_public_reports (as of 20260928012335_AddPublicReportSearch.sql),
-- none of which carry an attachment count. A view cannot lose a trailing
-- column in place (CREATE OR REPLACE only ever appends), so every view that
-- reads public_reports or admin_report_queue is dropped and restored with
-- them, in dependency order, the same pattern
-- 20260927172950_ListReportVersions.Down.sql uses.
DROP FUNCTION IF EXISTS search_public_reports(text, text, text, integer);
DROP VIEW public_report_media;
DROP VIEW admin_pending_counts;
DROP VIEW admin_report_queue;
DROP VIEW public_reports;

CREATE VIEW public_reports AS
SELECT report.id COLLATE "C" AS id,
       summary.ai_summary_en,
       summary.ai_summary_fr,
       COALESCE(report.published_at, summary.approved_at) AS published_at,
       (SELECT count(*)::integer
        FROM report_comments AS comment
        WHERE comment.report_id = report.id
          AND comment.deleted IS NULL
          AND comment.hidden_at IS NULL) AS comment_count,
       report.submitted_at
FROM reports AS report
         JOIN summaries AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status = 'published'
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';

CREATE VIEW admin_report_queue AS
SELECT report.id,
       report.submitted_at,
       report.status,
       report.language,
       report.consent_publish,
       stuck.is_stuck,
       stuck.is_stuck OR report.status IN ('pending', 'summary_failed') AS needs_action,
       report.xmin::text || '.' || COALESCE(summary.xmin::text, '0') AS version,
       NULLIF(TRIM(BOTH ' ' FROM CONCAT_WS(' ', names.reporter_first_name, names.reporter_last_name)), '') AS reporter_name,
       NULLIF(TRIM(BOTH ' ' FROM CONCAT_WS(' ', names.pilot_first_name, names.pilot_last_name)), '') AS pilot_name
FROM reports AS report
         CROSS JOIN LATERAL (
    SELECT report.status IN ('submitted', 'summarizing')
               AND report.submitted_at < now() - interval '24 hours' AS is_stuck
    ) AS stuck
         LEFT JOIN summaries AS summary
                   ON summary.report_id = report.id
                       AND summary.deleted IS NULL
         LEFT JOIN LATERAL (
    SELECT MAX(answer.value) FILTER (WHERE question.role = 'reporter_first_name') AS reporter_first_name,
           MAX(answer.value) FILTER (WHERE question.role = 'reporter_last_name')  AS reporter_last_name,
           MAX(answer.value) FILTER (WHERE question.role = 'pilot_first_name')    AS pilot_first_name,
           MAX(answer.value) FILTER (WHERE question.role = 'pilot_last_name')     AS pilot_last_name
    FROM report_answers AS answer
             JOIN questions AS question ON question.id = answer.question_id
    WHERE answer.report_id = report.id
      AND question.role IN ('reporter_first_name', 'reporter_last_name', 'pilot_first_name', 'pilot_last_name')
    ) AS names ON true
WHERE report.deleted IS NULL;

CREATE VIEW admin_pending_counts AS
SELECT (SELECT count(*) FROM admin_report_queue WHERE needs_action)::integer AS reports_needing_action,
       (SELECT count(*) FROM answers_awaiting_translation)::integer          AS answers_awaiting_translation,
       (SELECT count(*)
        FROM question_choices AS choice
                 JOIN questions AS question ON question.id = choice.question_id
        WHERE choice.needs_review
          AND question.deleted IS NULL)::integer                             AS type_ahead_values_awaiting_review;

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

CREATE FUNCTION search_public_reports(
    p_query text,
    p_locale text,
    p_after_id text,
    p_limit integer
)
RETURNS TABLE
(
    id             text,
    ai_summary_en  text,
    ai_summary_fr  text,
    published_at   timestamptz,
    comment_count  integer
)
LANGUAGE sql
STABLE
AS
$$
WITH config AS (
    SELECT CASE WHEN p_locale = 'fr-CA' THEN 'french'::regconfig ELSE 'english'::regconfig END AS ts_config
),
words AS (
    SELECT DISTINCT lower(word) AS word
    FROM regexp_split_to_table(btrim(p_query), '\s+') AS word
    WHERE word <> ''
),
word_queries AS (
    SELECT plainto_tsquery((SELECT ts_config FROM config), unaccent(word)) AS tsquery
    FROM words
),
comment_best AS (
    SELECT comment.report_id                          AS report_id,
           max(greatest(fts.fts_rank, trgm.trgm_rank)) AS score,
           bool_or(fts.fts_hit OR trgm.trgm_rank > 0.4) AS matched
    FROM public_report_comments AS comment
             CROSS JOIN LATERAL (
        SELECT CASE WHEN comment.locale = p_locale THEN comment.text
                    WHEN comment.translated_text IS NOT NULL THEN comment.translated_text
                    ELSE comment.text END AS text
        ) AS shown
             CROSS JOIN LATERAL (
        SELECT coalesce(max(ts_rank(to_tsvector((SELECT ts_config FROM config), unaccent(shown.text)), wq.tsquery)), 0) AS fts_rank,
               coalesce(bool_or(to_tsvector((SELECT ts_config FROM config), unaccent(shown.text)) @@ wq.tsquery), false) AS fts_hit
        FROM word_queries wq
        ) AS fts
             CROSS JOIN LATERAL (
        SELECT word_similarity(unaccent(p_query), unaccent(shown.text)) AS trgm_rank
        ) AS trgm
    GROUP BY comment.report_id
),
scored AS (
    SELECT report.id,
           report.ai_summary_en,
           report.ai_summary_fr,
           report.published_at,
           report.comment_count,
           greatest(fts.fts_rank, trgm.trgm_rank, coalesce(comment_best.score, 0))       AS score,
           (fts.fts_hit OR trgm.trgm_rank > 0.4 OR coalesce(comment_best.matched, false)) AS matched
    FROM public_reports AS report
             CROSS JOIN LATERAL (
        SELECT CASE WHEN p_locale = 'fr-CA' THEN report.ai_summary_fr ELSE report.ai_summary_en END AS text
        ) AS summary
             CROSS JOIN LATERAL (
        SELECT coalesce(max(ts_rank(to_tsvector((SELECT ts_config FROM config), unaccent(summary.text)), wq.tsquery)), 0) AS fts_rank,
               coalesce(bool_or(to_tsvector((SELECT ts_config FROM config), unaccent(summary.text)) @@ wq.tsquery), false) AS fts_hit
        FROM word_queries wq
        ) AS fts
             CROSS JOIN LATERAL (
        SELECT word_similarity(unaccent(p_query), unaccent(summary.text)) AS trgm_rank
        ) AS trgm
             LEFT JOIN comment_best ON comment_best.report_id = report.id
),
matched AS (
    SELECT * FROM scored WHERE matched
),
after_row AS (
    SELECT score, id FROM matched WHERE id = p_after_id
)
SELECT matched.id, matched.ai_summary_en, matched.ai_summary_fr, matched.published_at, matched.comment_count
FROM matched
WHERE p_after_id IS NULL
   OR NOT EXISTS (SELECT 1 FROM after_row)
   OR (matched.score, matched.id) < (SELECT score, id FROM after_row)
ORDER BY matched.score DESC, matched.id DESC
LIMIT p_limit;
$$;
