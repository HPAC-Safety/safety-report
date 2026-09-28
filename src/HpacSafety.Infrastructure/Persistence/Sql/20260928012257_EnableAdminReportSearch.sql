-- #573 / ADR-0156: fuzzy-search every part of a live report from Manage
-- reports. No new service — full-text search (English and French stemming)
-- plus pg_trgm trigram similarity, inside the one Postgres database that
-- already holds the private data these matches come from.
--
-- Guarded with IF NOT EXISTS: #574 (public search, ADR-0157) enables the same
-- two extensions independently, and either migration may land first.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

-- Every live report's searchable text, gathered from every source the
-- decision names: every answer including private ones, a choice answer's
-- label in both languages, the summary pair, staff-only private notes
-- (current text only), members' comments (current text and its
-- translation), and attachment file names, reporter-uploaded and
-- staff-only. HPAC receives dozens of reports a year, so aggregating this at
-- query time — rather than a materialized view or a maintained tsvector
-- column with a GIN index — costs nothing worth optimizing away; see
-- ADR-0156.
CREATE OR REPLACE VIEW admin_report_search_document AS
SELECT report.id AS report_id,
       TRIM(
           COALESCE(answers.text, '') || ' ' ||
           COALESCE(choices.text, '') || ' ' ||
           COALESCE(summary.text, '') || ' ' ||
           COALESCE(notes.text, '') || ' ' ||
           COALESCE(comments.text, '') || ' ' ||
           COALESCE(public_files.text, '') || ' ' ||
           COALESCE(private_files.text, '')
       ) AS plain_text
FROM reports AS report
         LEFT JOIN LATERAL (
    SELECT string_agg(
                   DISTINCT NULLIF(TRIM(COALESCE(answer.value, '') || ' ' || COALESCE(answer.translated_value, '')), ''),
                   ' ') AS text
    FROM report_answers AS answer
    WHERE answer.report_id = report.id
      AND answer.deleted IS NULL
    ) AS answers ON true
         LEFT JOIN LATERAL (
    SELECT string_agg(
                   DISTINCT NULLIF(TRIM(COALESCE(choice.label_en, '') || ' ' || COALESCE(choice.label_fr, '')), ''),
                   ' ') AS text
    FROM report_answers AS answer
             JOIN question_choices AS choice ON choice.id = answer.choice_id
    WHERE answer.report_id = report.id
      AND answer.deleted IS NULL
    ) AS choices ON true
         LEFT JOIN LATERAL (
    SELECT NULLIF(TRIM(COALESCE(summary.ai_summary_en, '') || ' ' || COALESCE(summary.ai_summary_fr, '')), '') AS text
    FROM summaries AS summary
    WHERE summary.report_id = report.id
      AND summary.deleted IS NULL
    ) AS summary ON true
         LEFT JOIN LATERAL (
    -- Only a note's current revision: an edited-away wording is not what a
    -- reviewer reads today, the same reason the report view shows only it.
    SELECT string_agg(DISTINCT revision.text, ' ') AS text
    FROM report_private_notes AS note
             JOIN report_private_note_revisions AS revision
                  ON revision.note_id = note.id
                      AND revision.deleted IS NULL
                      AND revision.number = (
                          SELECT max(latest.number)
                          FROM report_private_note_revisions AS latest
                          WHERE latest.note_id = note.id
                          )
    WHERE note.report_id = report.id
      AND note.deleted IS NULL
    ) AS notes ON true
         LEFT JOIN LATERAL (
    -- Only a comment's current revision and translation; hidden comments are
    -- still staff-searchable, only a deleted one is dropped.
    SELECT string_agg(
                   DISTINCT NULLIF(TRIM(COALESCE(revision.text, '') || ' ' || COALESCE(revision.translated_text, '')), ''),
                   ' ') AS text
    FROM report_comments AS comment
             JOIN report_comment_revisions AS revision
                  ON revision.comment_id = comment.id
                      AND revision.deleted IS NULL
                      AND revision.number = (
                          SELECT max(latest.number)
                          FROM report_comment_revisions AS latest
                          WHERE latest.comment_id = comment.id
                          )
    WHERE comment.report_id = report.id
      AND comment.deleted IS NULL
    ) AS comments ON true
         LEFT JOIN LATERAL (
    SELECT string_agg(DISTINCT file.original_file_name, ' ') AS text
    FROM report_files AS file
    WHERE file.report_id = report.id
      AND file.deleted IS NULL
      AND file.original_file_name IS NOT NULL
    ) AS public_files ON true
         LEFT JOIN LATERAL (
    SELECT string_agg(DISTINCT attachment.original_file_name, ' ') AS text
    FROM report_private_attachments AS attachment
    WHERE attachment.report_id = report.id
      AND attachment.deleted IS NULL
    ) AS private_files ON true
WHERE report.deleted IS NULL;

-- Ranks the live reports whose gathered text matches a reviewer's query:
-- full-text search under English and French stemming (accent-insensitive via
-- unaccent), or a word-similarity match for a typo or a partial word.
-- word_similarity (not plain similarity/`%`) is the one that fits a search
-- box: it looks for the query inside the best-matching run of words in the
-- report's whole gathered blob, rather than comparing the query's length
-- against the blob's entire length — the latter would dilute a short query
-- against dozens of unrelated words gathered from every other source and
-- almost never cross the threshold. GREATEST picks whichever signal matched
-- best, so a clean English phrase, a clean French phrase, and a near-miss
-- typo are all ranked on the same scale. The query text is a bound
-- parameter, never interpolated or logged.
CREATE OR REPLACE FUNCTION search_admin_reports(query text)
    RETURNS TABLE
            (
                report_id text,
                rank      double precision
            )
    LANGUAGE sql
    STABLE
AS
$$
SELECT doc.report_id::text,
       GREATEST(
               ts_rank(to_tsvector('english', unaccent(coalesce(doc.plain_text, ''))),
                       websearch_to_tsquery('english', unaccent(query))),
               ts_rank(to_tsvector('french', unaccent(coalesce(doc.plain_text, ''))),
                       websearch_to_tsquery('french', unaccent(query))),
               word_similarity(unaccent(query), unaccent(coalesce(doc.plain_text, '')))
       ) AS rank
FROM admin_report_search_document AS doc
WHERE to_tsvector('english', unaccent(coalesce(doc.plain_text, '')))
          @@ websearch_to_tsquery('english', unaccent(query))
   OR to_tsvector('french', unaccent(coalesce(doc.plain_text, '')))
          @@ websearch_to_tsquery('french', unaccent(query))
   OR unaccent(query) <% unaccent(coalesce(doc.plain_text, ''))
$$;
