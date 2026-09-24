-- Reverses 20260924170847_AddReportComments.sql, before its tables are dropped.
-- Views hold no data. public_reports is put back as
-- 20260924161944_CreatePublicReportsView.sql made it, which the migration runs
-- next.
DROP VIEW IF EXISTS public_report_comments;
DROP VIEW IF EXISTS public_reports;
