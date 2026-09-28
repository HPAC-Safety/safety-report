-- Fuzzy public search (#574, ADR-0157). pg_trgm gives typo tolerance;
-- Postgres full-text search gives language-appropriate stemming. Both engines
-- are shared with the admin search (#573, ADR-0156); IF NOT EXISTS lets either
-- pull request create the extension first.
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Trigram indexes speed up the typo-tolerant half of the match. They are a
-- best-effort addition, not a guarantee the planner uses them for every
-- shape of query the function below issues — perf tuning is deferred to a
-- follow-up once real query volume exists.
CREATE INDEX IF NOT EXISTS idx_summaries_ai_summary_en_trgm ON summaries USING gin (ai_summary_en gin_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_summaries_ai_summary_fr_trgm ON summaries USING gin (ai_summary_fr gin_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_report_comment_revisions_text_trgm ON report_comment_revisions USING gin (text gin_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_report_comment_revisions_translated_text_trgm ON report_comment_revisions USING gin (translated_text gin_trgm_ops);

-- search_public_reports(query, locale, after_id, limit) reads only
-- public_reports and public_report_comments — the two views that already hold
-- the whole publication and visibility invariant — so a non-publishable
-- report, a private answer, a hidden or deleted comment, or a name can never
-- reach it (REQ-MOD-140 to REQ-MOD-149).
--
-- Matching is scoped to the requested site locale (en-CA or fr-CA): a
-- report's summary is read in that language only, and a comment is read as it
-- is shown in that language — its own text when it was written in that
-- locale, otherwise its Worker translation once one exists, otherwise (not
-- yet translated) its original text, exactly the fallback the UI itself uses
-- (ReportComments.tsx "shownText"). Nothing from the other language, and
-- nothing private, is ever compared against the query.
--
-- Matching combines full-text rank (language-appropriate stemming) and
-- trigram word-similarity (typo tolerance) with GREATEST, matching a row when
-- either engine considers it a hit; ranking uses the same combined score, so
-- "best match first" orders by it, descending, tied by report ID descending
-- — the same tie-break the plain feed already uses.
--
-- Paging carries only the report ID in its cursor (ADR-0153, extended by
-- ADR-0157): never a score, never a timestamp. The caller resolves a cursor's
-- position by calling this same function for that one ID and reading the
-- score back off its own row — after_row below — never by decoding one out of
-- an opaque token. A cursor naming a report that no longer matches, is no
-- longer public, or never existed resolves to nothing, and the results start
-- from the top, the same rule the plain feed's cursor already follows.
CREATE OR REPLACE FUNCTION search_public_reports(
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
term AS (
    -- plainto_tsquery AND-joins every word of the query; OR-joining instead is
    -- what makes "best match" ranking meaningful across a multi-word query,
    -- rather than requiring every word to appear on the same row.
    SELECT to_tsquery((SELECT ts_config FROM config),
                       regexp_replace(plainto_tsquery((SELECT ts_config FROM config), p_query)::text, ' & ', ' | ', 'g')) AS tsquery
),
comment_best AS (
    SELECT comment.report_id                                                                     AS report_id,
           max(greatest(
                   ts_rank(to_tsvector((SELECT ts_config FROM config), shown.text), (SELECT tsquery FROM term)),
                   word_similarity(p_query, shown.text)
               ))                                                                                 AS score,
           bool_or(
                   to_tsvector((SELECT ts_config FROM config), shown.text) @@ (SELECT tsquery FROM term)
                   OR word_similarity(p_query, shown.text) > 0.4
           )                                                                                       AS matched
    FROM public_report_comments AS comment
             CROSS JOIN LATERAL (
        SELECT CASE WHEN comment.locale = p_locale THEN comment.text
                    WHEN comment.translated_text IS NOT NULL THEN comment.translated_text
                    ELSE comment.text END AS text
        ) AS shown
    GROUP BY comment.report_id
),
scored AS (
    SELECT report.id,
           report.ai_summary_en,
           report.ai_summary_fr,
           report.published_at,
           report.comment_count,
           greatest(
                   ts_rank(to_tsvector((SELECT ts_config FROM config), summary.text), (SELECT tsquery FROM term)),
                   word_similarity(p_query, summary.text),
                   coalesce(comment_best.score, 0)
           ) AS score,
           (
               to_tsvector((SELECT ts_config FROM config), summary.text) @@ (SELECT tsquery FROM term)
                   OR word_similarity(p_query, summary.text) > 0.4
                   OR coalesce(comment_best.matched, false)
               ) AS matched
    FROM public_reports AS report
             CROSS JOIN LATERAL (
        SELECT CASE WHEN p_locale = 'fr-CA' THEN report.ai_summary_fr ELSE report.ai_summary_en END AS text
        ) AS summary
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
