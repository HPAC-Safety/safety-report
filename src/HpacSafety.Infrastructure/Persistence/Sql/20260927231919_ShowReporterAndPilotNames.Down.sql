-- Reverses 20260927231919_ShowReporterAndPilotNames.sql. A view cannot lose a
-- trailing column in place (CREATE OR REPLACE only ever appends), so
-- admin_pending_counts, which reads admin_report_queue, is dropped and
-- restored with it, the same pattern
-- 20260927172950_ListReportVersions.Down.sql and
-- 20260928181012_AddAttachmentCounts.Down.sql use.
DROP VIEW admin_pending_counts;
DROP VIEW admin_report_queue;

CREATE VIEW admin_report_queue AS
SELECT report.id,
       report.submitted_at,
       report.status,
       report.language,
       report.consent_publish,
       stuck.is_stuck,
       stuck.is_stuck OR report.status IN ('pending', 'summary_failed') AS needs_action,
       report.xmin::text || '.' || COALESCE(summary.xmin::text, '0') AS version
FROM reports AS report
         CROSS JOIN LATERAL (
    SELECT report.status IN ('submitted', 'summarizing')
               AND report.submitted_at < now() - interval '24 hours' AS is_stuck
    ) AS stuck
         LEFT JOIN summaries AS summary
                   ON summary.report_id = report.id
                       AND summary.deleted IS NULL
WHERE report.deleted IS NULL;

CREATE VIEW admin_pending_counts AS
SELECT (SELECT count(*) FROM admin_report_queue WHERE needs_action)::integer AS reports_needing_action,
       (SELECT count(*) FROM answers_awaiting_translation)::integer          AS answers_awaiting_translation,
       (SELECT count(*)
        FROM question_choices AS choice
                 JOIN questions AS question ON question.id = choice.question_id
        WHERE choice.needs_review
          AND question.deleted IS NULL)::integer                             AS type_ahead_values_awaiting_review;

UPDATE questions
SET role = 'none'
WHERE key IN (
    'da89ae06_f229_4f38_8faa_e9c5bafef2f3',
    '3d662189_41cb_4430_9db8_7b2e4861df53',
    '52afac6c_b30c_4bd2_a052_212fa9249a45',
    '41c4d104_82c5_4f31_9d86_cb95e35622e4'
);
