-- Reverses 20260930014051_AddSummaryRevisions.sql. The columns have just been
-- added back to summaries; this copies the latest live revision's text into
-- them, restores the three views to read summaries again, and drops the two new
-- views. Every earlier revision is lost with summary_revisions, which the
-- migration drops next: a rollback of this migration returns to one row per
-- report holding only the current text, as before it.
UPDATE summaries AS summary
SET ai_summary_en       = revision.ai_summary_en,
    ai_summary_fr       = revision.ai_summary_fr,
    source_en           = revision.source_en,
    source_fr           = revision.source_fr,
    model               = revision.model,
    prompt_version      = revision.prompt_version,
    approved_by_subject = revision.approved_by_subject,
    approved_at         = revision.approved_at,
    generated_at        = first.created_at,
    updated_at          = revision.created_at
FROM summary_revisions AS revision
         JOIN summary_revisions AS first ON first.summary_id = revision.summary_id AND first.sequence = 1
WHERE revision.summary_id = summary.id
  AND revision.sequence = (SELECT max(latest.sequence)
                           FROM summary_revisions AS latest
                           WHERE latest.summary_id = summary.id);

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
         JOIN summaries AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status = 'published'
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';

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

DROP VIEW latest_approved_summary_revisions;
DROP VIEW latest_summary_revisions;
