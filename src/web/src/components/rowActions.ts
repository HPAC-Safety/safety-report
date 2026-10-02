import type { ReportListItem } from "../api/adminReports"
import { actionsFor } from "./ReviewActions"

/** The quick actions a Manage reports row offers (REQ-MOD-120). */
export type RowAction = "publish" | "unpublish" | "delete"

/**
 * Publish wherever the report view offers it, Unpublish on a published report,
 * and Delete on every row. Declining a Pending report stays in the report view,
 * where its optional note lives.
 */
export function rowActionsFor(report: ReportListItem): RowAction[] {
	const offered = actionsFor(report)
	const actions: RowAction[] = []
	if (offered.includes("publish")) actions.push("publish")
	if (report.status === "published") actions.push("unpublish")
	actions.push("delete")
	return actions
}
