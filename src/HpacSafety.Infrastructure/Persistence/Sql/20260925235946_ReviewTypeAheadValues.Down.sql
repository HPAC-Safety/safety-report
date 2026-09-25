-- Restores the pending counts as CreateAdminQueueViews defined them, before
-- the column they count is dropped. A view cannot drop a column in place.
DROP VIEW admin_pending_counts;

CREATE VIEW admin_pending_counts AS
SELECT (SELECT count(*) FROM admin_report_queue WHERE needs_action)::integer AS reports_needing_action,
       (SELECT count(*) FROM answers_awaiting_translation)::integer          AS answers_awaiting_translation;
