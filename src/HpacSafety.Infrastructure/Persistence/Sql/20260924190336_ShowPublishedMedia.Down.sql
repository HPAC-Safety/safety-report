-- Reverses 20260924190336_ShowPublishedMedia.sql before its columns are dropped.
-- The view holds no data. The seeded consent_media question is kept: a question
-- is never physically deleted (AGENTS.md invariant 8), and one reports may have
-- answered is history. Its role is set back to none so the older role
-- constraint the migration restores accepts it.
DROP VIEW IF EXISTS public_report_media;
UPDATE questions SET role = 'none' WHERE role = 'consent_media';
