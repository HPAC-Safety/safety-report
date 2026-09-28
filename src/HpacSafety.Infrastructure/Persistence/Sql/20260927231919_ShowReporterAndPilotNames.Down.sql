-- Reverses 20260927231919_ShowReporterAndPilotNames.sql.
CREATE OR REPLACE VIEW admin_report_queue AS
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

UPDATE questions
SET role = 'none'
WHERE key IN (
    'da89ae06_f229_4f38_8faa_e9c5bafef2f3',
    '3d662189_41cb_4430_9db8_7b2e4861df53',
    '52afac6c_b30c_4bd2_a052_212fa9249a45',
    '41c4d104_82c5_4f31_9d86_cb95e35622e4'
);
