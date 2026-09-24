-- The public side's one source of truth for what is public (#28, ADR-0055).
--
-- A row exists only while the whole publication invariant holds, the same rule
-- as Report.IsPublishable plus the nonblank-text clause REQ-DOM-004 adds:
-- the report and its summary are live, consent is exactly true, the report
-- is Approved or Published, and the pair carries a current human approval.
--
-- The columns are the public DTO's allowlist and nothing else (CON-DP-011).
-- id is read under the "C" collation so the feed's order and its keyset
-- cursor compare IDs byte by byte, the same way everywhere.
-- published_at falls back to the pair's approval time for a report approved
-- before approval published it, so every row has a time to order by.
CREATE OR REPLACE VIEW public_reports AS
SELECT report.id COLLATE "C" AS id,
       summary.ai_summary_en,
       summary.ai_summary_fr,
       COALESCE(report.published_at, summary.approved_at) AS published_at
FROM reports AS report
         JOIN summaries AS summary ON summary.report_id = report.id
WHERE report.deleted IS NULL
  AND report.consent_publish IS TRUE
  AND report.status IN ('approved', 'published')
  AND summary.deleted IS NULL
  AND summary.approved_at IS NOT NULL
  AND btrim(summary.ai_summary_en) <> ''
  AND btrim(summary.ai_summary_fr) <> '';
