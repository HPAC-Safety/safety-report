-- Removes the immutability triggers and their functions (issue #669,
-- ADR-0178). No table or row is touched.
DROP TRIGGER summary_revisions_never_truncated ON summary_revisions;
DROP TRIGGER reports_never_truncated ON reports;
DROP TRIGGER report_files_never_truncated ON report_files;
DROP TRIGGER report_answers_never_truncated ON report_answers;
DROP FUNCTION refuse_truncate_of_reporters_account();
DROP TRIGGER summary_revisions_immutable ON summary_revisions;
DROP FUNCTION enforce_summary_revisions_immutability();
DROP TRIGGER reports_immutable ON reports;
DROP FUNCTION enforce_reports_immutability();
DROP TRIGGER report_files_immutable ON report_files;
DROP FUNCTION enforce_report_files_immutability();
DROP TRIGGER report_answers_immutable ON report_answers;
DROP FUNCTION enforce_report_answers_immutability();
