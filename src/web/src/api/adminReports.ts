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

/** How one summary language was produced (ADR-0108). */
export type SummarySource = "generated" | "human" | "machine"

export interface ReportSummary {
	aiSummaryEn: string
	aiSummaryFr: string
	model: string
	promptVersion: string
	generatedAt: string
	updatedAt: string
	approvedBySubject: string | null
	approvedAt: string | null
	sourceEn: SummarySource
	sourceFr: SummarySource
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
	/** Sent back with every review command; a stale one is refused with 409 (ADR-0105). */
	version: string
	rejectionNote: string | null
	publishedAt: string | null
}

/** A short-lived link to one attachment, minted by its own audited request. */
export interface AttachmentLink {
	url: string
	expiresAt: string
	fileName: string
}

/** The problem type the API sends when a review command was based on a stale view. */
export const STALE_REPORT = "https://hpac.ca/problems/stale-report"

// Signature split across lines on purpose: tools/check-hardcoded-strings.mjs
// is a line scanner — see adminQuestions.ts.
async function call<T>(
	path: string,
	init?: RequestInit,
): Promise<T> {
	const response = await fetch(path, {
		...init,
		headers: { "Content-Type": "application/json", ...authorization(), ...init?.headers },
	})

	if (response.status === 401) {
		clearSession()
	}

	if (!response.ok) {
		const problem = await response.json().catch(() => null)
		throw new ApiError(
			response.status,
			problem?.detail ?? problem?.title ?? response.statusText,
			problem?.type ?? null,
		)
	}

	return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

function get<T>(
	path: string,
): Promise<T> {
	return call<T>(path)
}

function post<T>(
	path: string,
	body: unknown,
): Promise<T> {
	return call<T>(path, { method: "POST", body: JSON.stringify(body) })
}

export function listReports(filter: ReportFilter): Promise<ReportListItem[]> {
	return get(`/api/admin/reports?filter=${encodeURIComponent(filter)}`)
}

export function getReport(id: string): Promise<ReportDetail> {
	return get(`/api/admin/reports/${encodeURIComponent(id)}`)
}

const reportPath = (id: string) => `/api/admin/reports/${encodeURIComponent(id)}`

/** Saves both texts together; after a failed summarization this writes the pair by hand. */
export function saveSummaryPair(
	id: string,
	version: string,
	aiSummaryEn: string,
	aiSummaryFr: string,
	sourceEn: SummarySource = "human",
	sourceFr: SummarySource = "human",
): Promise<ReportDetail> {
	return call(`${reportPath(id)}/summary`, {
		method: "PUT",
		body: JSON.stringify({ version, aiSummaryEn, aiSummaryFr, sourceEn, sourceFr }),
	})
}

/** Approves the pair; the API publishes it too when the reporter consented (ADR-0105). */
export function approveReport(id: string, version: string): Promise<ReportDetail> {
	return post(`${reportPath(id)}/approve`, { version })
}

export function rejectReport(id: string, version: string, note: string): Promise<ReportDetail> {
	return post(`${reportPath(id)}/reject`, { version, note })
}

export function reopenReport(id: string, version: string): Promise<ReportDetail> {
	return post(`${reportPath(id)}/reopen`, { version })
}

export function unpublishReport(id: string, version: string): Promise<ReportDetail> {
	return post(`${reportPath(id)}/unpublish`, { version })
}

export function deleteReport(id: string): Promise<void> {
	return call(reportPath(id), { method: "DELETE" })
}

/** An image or video opens its safe derivative; a document downloads its validated original. */
export function attachmentLink(reportId: string, attachment: ReportAttachment): Promise<AttachmentLink> {
	const verb = attachment.kind === "document" ? "download" : "view"
	return get(`${reportPath(reportId)}/attachments/${encodeURIComponent(attachment.id)}/${verb}`)
}
