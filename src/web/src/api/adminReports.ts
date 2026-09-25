import { clearSession } from "../auth/session"
import { ApiError, authorization } from "./adminQuestions"

/** A report's workflow status, as the API's lowercase code. */
export type ReportStatus =
	| "submitted"
	| "summarizing"
	| "summary_failed"
	| "pending"
	| "published"
	| "unpublished"

/** A consent the reporter gave (`true`), refused (`false`), or left unanswered (`null`), never a word (ADR-0130). */
export type ReportConsent = boolean | null

/** The locale-catalogue suffix a consent reads as: `reports.detail.consent.<suffix>`. */
export function consentKey(consent: ReportConsent): "yes" | "no" | "unanswered" {
	if (consent === null) return "unanswered"
	return consent ? "yes" : "no"
}

/** Each filter the admin report list accepts, in the order the page offers them. */
export const REPORT_FILTERS = ["all", "needs-action", "published", "unpublished", "private", "summary-failed"] as const

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
	/** A boolean for a yes/no or checkbox answer (ADR-0130); the reporter's words otherwise. */
	value: string | boolean
	locale: string
	translatedValue: string | null
	translationSource: string | null
}

export interface ReportAnswer {
	questionKey: string
	labelEn: string
	labelFr: string
	/** The question's type code, so a stored value can be shown in the reader's language (REQ-MOD-075). */
	type: string
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

/**
 * Whether the published report shows a file (ADR-0117): public now, once the
 * report is published, hidden by a reviewer, not shared because the reporter
 * did not agree, or never public (a document, or no verified derivative).
 */
export type AttachmentVisibility = "public" | "when_published" | "hidden" | "no_consent" | "private"

export interface ReportAttachment {
	id: string
	kind: "image" | "video" | "document"
	state: "ready" | "processing" | "failed"
	visibility: AttachmentVisibility
}

/** Everything a reviewer needs to judge one report (REQ-MOD-031). Reading it is audited. */
export interface ReportDetail extends ReportListItem {
	summaryError: string | null
	answers: ReportAnswer[]
	summary: ReportSummary | null
	attachments: ReportAttachment[]
	/** Whether the reporter agreed to share photos and video; unanswered when they were never asked. */
	mediaConsent: ReportConsent
	/** Sent back with every review command; a stale one is refused with 409 (ADR-0105). */
	version: string
	unpublishNote: string | null
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

/** Approves the pair and makes the report public in one action (ADR-0125). */
export function publishReport(id: string, version: string): Promise<ReportDetail> {
	return post(`${reportPath(id)}/publish`, { version })
}

/** Takes a report off the public feed, or declines a pending one, with an optional reviewer-only note. */
export function unpublishReport(id: string, version: string, note: string): Promise<ReportDetail> {
	return post(`${reportPath(id)}/unpublish`, { version, note })
}

export function deleteReport(id: string): Promise<void> {
	return call(reportPath(id), { method: "DELETE" })
}

/** An image or video opens its safe derivative; a document downloads its validated original. */
/** Hides an image or video from the published report, or shows it again. Audited (REQ-MED-030). */
export function setAttachmentHidden(reportId: string, attachmentId: string, hidden: boolean): Promise<void> {
	return call<void>(`${reportPath(reportId)}/attachments/${encodeURIComponent(attachmentId)}/${hidden ? "hide" : "show"}`, {
		method: "POST",
	})
}

export function attachmentLink(reportId: string, attachment: ReportAttachment): Promise<AttachmentLink> {
	const verb = attachment.kind === "document" ? "download" : "view"
	return get(`${reportPath(reportId)}/attachments/${encodeURIComponent(attachment.id)}/${verb}`)
}

/** How much admin work is waiting, for the Admin menu's badges (REQ-MOD-084). */
export interface PendingCounts {
	reportsNeedingAction: number
	/** Null unless the member is an administrator: the queue is theirs alone. */
	answersAwaitingTranslation: number | null
	/** Type-ahead values waiting for a Safety Officer or Administrator to review (ADR-0129). */
	typeAheadValuesAwaitingReview: number
}

export function getPendingCounts(): Promise<PendingCounts> {
	return get("/api/admin/counts")
}
