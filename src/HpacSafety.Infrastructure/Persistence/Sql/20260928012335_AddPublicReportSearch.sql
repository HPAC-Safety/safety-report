-- Fuzzy public search (#574, ADR-0157). pg_trgm gives typo tolerance;
-- Postgres full-text search gives language-appropriate stemming; unaccent
-- gives accent-insensitive matching. All three are shared with the admin
-- search (#573, ADR-0156); IF NOT EXISTS lets either pull request create an
-- extension first. ADR-0156 decided against search indexes at HPAC's
-- volume, so none are added here either.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

-- search_public_reports(query, locale, after_id, limit) reads only
-- public_reports and public_report_comments — the two views that already hold
-- the whole publication and visibility invariant — so a non-publishable
-- report, a private answer, a hidden or deleted comment, or a name can never
-- reach it. It takes no identity of its own kind, so it cannot widen by
-- caller role either (REQ-MOD-140 to REQ-MOD-149).
--
-- Matching is scoped to the requested site locale (en-CA or fr-CA): a
-- report's summary is read in that language only, and a comment is read as it
-- is shown in that language — its own text when it was written in that
-- locale, otherwise its Worker translation once one exists, otherwise (not
-- yet translated) its original text, exactly the fallback the UI itself uses
-- (ReportComments.tsx "shownText"). Nothing from the other language, and
-- nothing private, is ever compared against the query.
--
-- Every comparison wraps both the query and the candidate text in
-- unaccent(), so a visitor who types "securite" still finds "sécurité" and
-- vice versa (ADR-0157).
--
-- "Best match" needs OR semantics across a multi-word query — searching
-- "landing gear failure" should not require every word on the same row —
-- without ever parsing or rewriting a tsquery's own rendered text (that
-- text is not a stable, re-parseable grammar to string-munge). Instead, the
-- query is split into words in SQL, each word becomes its own
-- plainto_tsquery (already safe against any input: punctuation, hyphens,
-- quotes, apostrophes — plainto_tsquery only ever tokenizes text, it never
-- throws), and a row's full-text hit and rank are the OR/MAX of its words'
-- individual hits and ranks. No tsquery is ever combined, concatenated, or
-- rewritten as text.
--
-- Matching combines that full-text signal and trigram word-similarity
-- (typo and accent tolerance together) with GREATEST, matching a row when
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
words AS (
    -- The query, split on whitespace in SQL — never by re-parsing a
    -- tsquery's text. Each word becomes its own safe plainto_tsquery below.
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
        -- OR across every word: a hit or the best rank on any one of them,
        -- not all of them on the same text.
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
