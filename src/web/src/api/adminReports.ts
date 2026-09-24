import { clearSession } from "../auth/session"
import { ApiError, authorization } from "./adminQuestions"

/** A report's workflow status, as the API's lowercase code. */
export type ReportStatus =
	| "submitted"
	| "summarizing"
	| "pending_review"
	| "summary_failed"
	| "approved"
	| "rejected"
	| "published"

/** Publication consent: the reporter's yes or no, or unanswered on an older report. */
export type ReportConsent = "yes" | "no" | "unanswered"

/** Each filter the admin report list accepts, in the order the page offers them. */
export const REPORT_FILTERS = ["all", "needs-action", "published", "private", "rejected", "summary-failed"] as const

export type ReportFilter = (typeof REPORT_FILTERS)[number]

export function isReportFilter(value: string | null): value is ReportFilter {
	return value !== null && (REPORT_FILTERS as readonly string[]).includes(value)
}

/** One row of the list: state and timing only, never answer or summary text (REQ-MOD-030). */
export interface ReportListItem {
	id: string
	submittedAt: string
	status: ReportStatus
	language: string
	consent: ReportConsent
	isStuck: boolean
}

export interface ReportAnswerValue {
	value: string
	locale: string
	translatedValue: string | null
	translationSource: string | null
}

export interface ReportAnswer {
	questionKey: string
	labelEn: string
	labelFr: string
	isPrivate: boolean
	values: ReportAnswerValue[]
}

export interface ReportSummary {
	aiSummaryEn: string
	aiSummaryFr: string
	model: string
	promptVersion: string
	generatedAt: string
	updatedAt: string
	approvedBySubject: string | null
	approvedAt: string | null
}

export interface ReportAttachment {
	id: string
	kind: "image" | "video" | "document"
	state: "ready" | "processing" | "failed"
}

/** Everything a reviewer needs to judge one report (REQ-MOD-031). Reading it is audited. */
export interface ReportDetail extends ReportListItem {
	summaryError: string | null
	answers: ReportAnswer[]
	summary: ReportSummary | null
	attachments: ReportAttachment[]
}

// Signature split across lines on purpose: tools/check-hardcoded-strings.mjs
// is a line scanner — see adminQuestions.ts.
async function get<T>(
	path: string,
): Promise<T> {
	const response = await fetch(path, { headers: { ...authorization() } })

	if (response.status === 401) {
		clearSession()
	}

	if (!response.ok) {
		const problem = await response.json().catch(() => null)
		throw new ApiError(response.status, problem?.detail ?? problem?.title ?? response.statusText)
	}

	return (await response.json()) as T
}

export function listReports(filter: ReportFilter): Promise<ReportListItem[]> {
	return get(`/api/admin/reports?filter=${encodeURIComponent(filter)}`)
}

export function getReport(id: string): Promise<ReportDetail> {
	return get(`/api/admin/reports/${encodeURIComponent(id)}`)
}
