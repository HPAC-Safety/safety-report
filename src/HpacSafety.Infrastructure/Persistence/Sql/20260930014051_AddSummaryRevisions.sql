-- Summaries become append-only revisions (issue #668, ADR-0177).
--
-- summary_revisions has just been created by the migration. Each existing
-- summaries row becomes revision 1, keeping its text, its per-language sources,
-- its model and prompt version, and its approval. Its author is unknown, so it
-- is null: the audit log never held the text, so earlier edits cannot be
-- recovered. Its created_at is the row's generated_at, so the history reads from
-- when the pair first existed; the row's updated_at is not kept as a second
-- revision, because there is nothing to put in it. A deleted summary's revision
-- is stamped deleted with it.
--
-- The id is derived from the summary, in the TinyId alphabet (base64url), the
-- same way CopyChoicesOntoQuestions derives its own, so a re-run cannot mint a
-- second revision 1.
INSERT INTO summary_revisions
    (id, summary_id, sequence, ai_summary_en, ai_summary_fr, source_en, source_fr, model, prompt_version,
     author_subject, created_at, restored_from_id, approved_by_subject, approved_at, deleted)
SELECT left(translate(encode(sha256(convert_to('summary_revision:' || summary.id, 'UTF8')), 'base64'), '+/', '-_'), 11),
       summary.id,
       1,
       summary.ai_summary_en,
       summary.ai_summary_fr,
       summary.source_en,
       summary.source_fr,
       summary.model,
       summary.prompt_version,
       NULL,
       summary.generated_at,
       NULL,
       summary.approved_by_subject,
       summary.approved_at,
       summary.deleted
FROM summaries AS summary;

-- The latest live revision of each live summary: what a reviewer sees and edits
-- from. row_version is the revision row's xmin, as text, so a view that reports
-- a report's version reads it the same way the API's concurrency token does
-- (ADR-0105).
CREATE VIEW latest_summary_revisions AS
SELECT DISTINCT ON (revision.summary_id)
       revision.id,
       revision.summary_id,
       summary.report_id,
       revision.sequence,
       revision.ai_summary_en,
       revision.ai_summary_fr,
       revision.source_en,
       revision.source_fr,
       revision.model,
       revision.prompt_version,
       revision.author_subject,
       revision.created_at,
       revision.restored_from_id,
       revision.approved_by_subject,
       revision.approved_at,
       revision.xmin::text AS row_version
FROM summary_revisions AS revision
         JOIN summaries AS summary ON summary.id = revision.summary_id
WHERE revision.deleted IS NULL
  AND summary.deleted IS NULL
ORDER BY revision.summary_id, revision.sequence DESC;

-- The latest live revision of each live summary that a reviewer has approved:
-- the only text the public reads of a Published report. An edit to a Published
-- report is approved as it is saved, so it is this view's newest row the moment
-- it exists.
CREATE VIEW latest_approved_summary_revisions AS
SELECT DISTINCT ON (revision.summary_id)
       revision.id,
       revision.summary_id,
       summary.report_id,
       revision.sequence,
       revision.ai_summary_en,
       revision.ai_summary_fr,
       revision.source_en,
       revision.source_fr,
       revision.model,
       revision.prompt_version,
       revision.author_subject,
       revision.created_at,
       revision.restored_from_id,
       revision.approved_by_subject,
       revision.approved_at,
       revision.xmin::text AS row_version
FROM summary_revisions AS revision
         JOIN summaries AS summary ON summary.id = revision.summary_id
WHERE revision.deleted IS NULL
  AND summary.deleted IS NULL
  AND revision.approved_at IS NOT NULL
ORDER BY revision.summary_id, revision.sequence DESC;

-- public_reports reads the latest approved revision. Its columns are unchanged
-- (language, added by ShowReportLanguageOnPublicReports, stays last),
-- so every view and function built on it (public_report_media,
-- public_report_comments, search_public_reports) is untouched.
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
          AND file.deleted IS NULL) AS full_attachment_count,
       report.language
FROM reports AS report
         JOIN latest_approved_summary_revisions AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status = 'published'
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';

-- admin_report_queue's version now ends in the latest revision's xmin: the token
-- a review command carries names the report and the revision it was loaded at.
CREATE OR REPLACE VIEW admin_report_queue AS
SELECT report.id,
       report.submitted_at,
       report.status,
       report.language,
       report.consent_publish,
       stuck.is_stuck,
       stuck.is_stuck OR report.status IN ('pending', 'summary_failed') AS needs_action,
       report.xmin::text || '.' || COALESCE(summary.row_version, '0') AS version,
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
         LEFT JOIN latest_summary_revisions AS summary
                   ON summary.report_id = report.id
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

-- The admin search reads the latest revision's pair, approved or not: a reviewer
-- searching for a draft they just wrote should find it.
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
    FROM latest_summary_revisions AS summary
    WHERE summary.report_id = report.id
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
