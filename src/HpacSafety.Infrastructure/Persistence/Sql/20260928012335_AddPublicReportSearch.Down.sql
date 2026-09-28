-- Reverses 20260928010000_AddPublicReportSearch.sql. Leaves pg_trgm installed:
-- it is shared with the admin search (#573, ADR-0156), and either pull request
-- may still depend on it.
DROP FUNCTION IF EXISTS search_public_reports(text, text, text, integer);

DROP INDEX IF EXISTS idx_summaries_ai_summary_en_trgm;
DROP INDEX IF EXISTS idx_summaries_ai_summary_fr_trgm;
DROP INDEX IF EXISTS idx_report_comment_revisions_text_trgm;
DROP INDEX IF EXISTS idx_report_comment_revisions_translated_text_trgm;
