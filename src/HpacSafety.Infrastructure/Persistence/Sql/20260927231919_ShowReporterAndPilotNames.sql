-- #571 / ADR-0154: the reporter's and pilot's first and last name answers are
-- read by stable QuestionRole, not by wording or position, so a fork or a
-- reworded label never loses them. The seeded question bank already asks
-- these four questions with role = 'none'; QuestionBankSeedWriter's guarded
-- INSERT never touches an already-seeded row, so a live database needs its
-- role backfilled by key, the identity ADR-0071 already guarantees across a
-- fork. Every row sharing a key gets the role, live or already forked away,
-- because an old answer resolves through whichever row was live when it was
-- given.
UPDATE questions
SET role = 'reporter_first_name'
WHERE key = 'da89ae06_f229_4f38_8faa_e9c5bafef2f3';

UPDATE questions
SET role = 'reporter_last_name'
WHERE key = '3d662189_41cb_4430_9db8_7b2e4861df53';

UPDATE questions
SET role = 'pilot_first_name'
WHERE key = '52afac6c_b30c_4bd2_a052_212fa9249a45';

UPDATE questions
SET role = 'pilot_last_name'
WHERE key = '41c4d104_82c5_4f31_9d86_cb95e35622e4';

-- admin_report_queue (20260924180636_CreateAdminQueueViews.sql,
-- 20260927172950_ListReportVersions.sql) gains reporter_name and pilot_name:
-- each is first-plus-last, trimmed, and null when neither half was answered,
-- read by role through a lateral join so a report answering only one of the
-- pair still shows it. Reporter and pilot are never merged into each other
-- even when the same text answers both.
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
