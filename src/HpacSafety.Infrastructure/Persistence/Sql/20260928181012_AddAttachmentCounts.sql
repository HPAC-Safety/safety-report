-- A viewer-scoped attachment count on the public feed and the admin list
-- (issue #427, decisions 1-2, 15-16, amending ADR-0117). Computed here, not
-- in C#: the public count is how many rows a report has in
-- public_report_media (image, video, or document the public may see); the
-- staff count is every non-deleted report_files row, whatever its kind,
-- state, or visibility. report_private_attachments (ADR-0135) is never
-- counted or shown either way.
--
-- public_reports gains public_attachment_count and full_attachment_count.
-- Both travel on every row; the API picks which one a viewer sees. A column
-- can only be added at the end of a replaced view.
CREATE OR REPLACE VIEW public_reports AS
SELECT report.id COLLATE "C" AS id,
       summary.ai_summary_en,
       summary.ai_summary_fr,
       COALESCE(report.published_at, summary.approved_at) AS published_at,
       (SELECT count(*)::integer
        FROM report_comments AS comment
        WHERE comment.report_id = report.id
          AND comment.deleted IS NULL
          AND comment.hidden_at IS NULL) AS comment_count,
       report.submitted_at,
       -- Mirrors public_report_media's own predicate rather than joining that
       -- view: public_report_media joins public_reports, so joining it back
       -- here would be a circular view reference ("infinite recursion
       -- detected in rules"). Kept in sync by hand; both read the same six
       -- columns off the same two tables.
       (SELECT count(*)::integer
        FROM report_files AS file
        WHERE file.report_id = report.id
          AND file.deleted IS NULL
          AND file.hidden_at IS NULL
          AND file.processing_error_code IS NULL
          AND ((file.kind IN ('image', 'video')
                    AND report.consent_media IS TRUE
                    AND file.stripped_blob_key IS NOT NULL
                    AND file.exif_stripped_at IS NOT NULL)
               OR (file.kind = 'document'
                    AND report.consent_documents IS TRUE
                    AND file.validated_at IS NOT NULL))) AS public_attachment_count,
       (SELECT count(*)::integer
        FROM report_files AS file
        WHERE file.report_id = report.id
          AND file.deleted IS NULL) AS full_attachment_count
FROM reports AS report
         JOIN summaries AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status = 'published'
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';

-- admin_report_queue gains attachment_count: every report on the admin list
-- is read by staff, so it carries only the one, full count.
CREATE OR REPLACE VIEW admin_report_queue AS
SELECT report.id,
       report.submitted_at,
       report.status,
       report.language,
       report.consent_publish,
       stuck.is_stuck,
       stuck.is_stuck OR report.status IN ('pending', 'summary_failed') AS needs_action,
       report.xmin::text || '.' || COALESCE(summary.xmin::text, '0') AS version,
       NULLIF(TRIM(BOTH ' ' FROM CONCAT_WS(' ', names.reporter_first_name, names.reporter_last_name)), '') AS reporter_name,
       NULLIF(TRIM(BOTH ' ' FROM CONCAT_WS(' ', names.pilot_first_name, names.pilot_last_name)), '') AS pilot_name,
       (SELECT count(*)::integer
        FROM report_files AS file
        WHERE file.report_id = report.id
          AND file.deleted IS NULL) AS attachment_count
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

-- search_public_reports gains the same two counts, read off public_reports
-- (already joined) and carried through every CTE unchanged, so a search
-- result shows the same count a plain feed row would. The return shape
-- changes, so the function is dropped and recreated rather than replaced.
DROP FUNCTION IF EXISTS search_public_reports(text, text, text, integer);

CREATE FUNCTION search_public_reports(
    p_query text,
    p_locale text,
    p_after_id text,
    p_limit integer
)
RETURNS TABLE
(
    id                       text,
    ai_summary_en            text,
    ai_summary_fr            text,
    published_at             timestamptz,
    comment_count            integer,
    public_attachment_count  integer,
    full_attachment_count    integer
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
           report.public_attachment_count,
           report.full_attachment_count,
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
SELECT matched.id, matched.ai_summary_en, matched.ai_summary_fr, matched.published_at, matched.comment_count,
       matched.public_attachment_count, matched.full_attachment_count
FROM matched
WHERE p_after_id IS NULL
   OR NOT EXISTS (SELECT 1 FROM after_row)
   OR (matched.score, matched.id) < (SELECT score, id FROM after_row)
ORDER BY matched.score DESC, matched.id DESC
LIMIT p_limit;
$$;
