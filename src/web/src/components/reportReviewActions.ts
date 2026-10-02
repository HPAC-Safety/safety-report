import type { ReportListItem, ReportStatus } from "../api/adminReports"

/** What a reviewer may do in each state (REQ-MOD-062, ADR-0125). Delete is always offered. */
export type ReviewAction = "edit" | "write" | "publish" | "unpublish" | "delete"

const ACTIONS: Record<ReportStatus, ReviewAction[]> = {
	pending: ["edit", "publish", "unpublish", "delete"],
	published: ["edit", "unpublish", "delete"],
	unpublished: ["edit", "publish", "delete"],
	summary_failed: ["write", "delete"],
	submitted: ["delete"],
	summarizing: ["delete"],
}

/**
 * A report whose reporter did not consent is unpublished for good: it is never
 * summarized, and deleting it is the one thing a reviewer can do (REQ-DOM-015).
 */
export function actionsFor(report: Pick<ReportListItem, "status" | "consent">): ReviewAction[] {
	return report.consent === true ? ACTIONS[report.status] : ["delete"]
}
