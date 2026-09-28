-- Reverses 20260928012257_EnableAdminReportSearch.sql. The pg_trgm and
-- unaccent extensions are left installed: #574 (public search) may depend on
-- either one independently of this migration's order.
DROP FUNCTION IF EXISTS search_admin_reports(text);
DROP VIEW IF EXISTS admin_report_search_document;
