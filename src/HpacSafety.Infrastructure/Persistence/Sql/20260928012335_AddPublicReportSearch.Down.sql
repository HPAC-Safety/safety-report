-- Reverses 20260928012335_AddPublicReportSearch.sql. Leaves pg_trgm and
-- unaccent installed: both are shared with the admin search (#573,
-- ADR-0156), and either pull request may still depend on them.
DROP FUNCTION IF EXISTS search_public_reports(text, text, text, integer);
