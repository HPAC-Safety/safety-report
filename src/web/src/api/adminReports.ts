import { clearSession } from "../auth/session"
import { ApiError, authorization } from "./adminQuestions"
import { deleteUpload } from "./uploads"

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
	/** For a choice answer, its choice's pin — `first`, `last`, or `none` (ADR-0136); null otherwise. */
	pin: string | null
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

/** The longest private note the API accepts (ADR-0133). */
export const PRIVATE_NOTE_MAX_LENGTH = 4000

/** The problem type the API sends when a note edit was based on an older revision. */
export const STALE_PRIVATE_NOTE = "https://hpac.ca/problems/stale-private-note"

/** A staff-only note as it reads now (ADR-0133). */
export interface PrivateNote {
	id: string
	text: string
	/** The current revision number, sent back with an edit. */
	revision: number
	/** The current text's writer, as an opaque token subject. */
	writtenBy: string
	writtenAt: string
	createdAt: string
	edited: boolean
	isMine: boolean
	/** The private attachment the current text refers to, if any (ADR-0135). */
	attachment: PrivateNoteAttachment | null
}

/** The private attachment a note's revision refers to, marked when it was removed since. */
export interface PrivateNoteAttachment {
	id: string
	fileName: string
	removed: boolean
}

/** One revision in a note's history. */
export interface PrivateNoteRevision {
	number: number
	text: string
	writtenBy: string
	writtenAt: string
	isMine: boolean
	attachment: PrivateNoteAttachment | null
}

const notesPath = (reportId: string) => `${reportPath(reportId)}/private-notes`

export function listPrivateNotes(reportId: string): Promise<PrivateNote[]> {
	return get(notesPath(reportId))
}

export function addPrivateNote(reportId: string, text: string, attachmentId: string | null = null): Promise<PrivateNote> {
	return post(notesPath(reportId), { text, attachmentId })
}

export function editPrivateNote(
	reportId: string,
	note: PrivateNote,
	text: string,
	attachmentId: string | null = null,
): Promise<PrivateNote> {
	return call(`${notesPath(reportId)}/${encodeURIComponent(note.id)}`, {
		method: "PUT",
		body: JSON.stringify({ text, revision: note.revision, attachmentId }),
	})
}

export function removePrivateNote(reportId: string, noteId: string): Promise<void> {
	return call(`${notesPath(reportId)}/${encodeURIComponent(noteId)}`, { method: "DELETE" })
}

export function privateNoteHistory(reportId: string, noteId: string): Promise<PrivateNoteRevision[]> {
	return get(`${notesPath(reportId)}/${encodeURIComponent(noteId)}/revisions`)
}

/** The longest private-attachment description the API accepts (ADR-0135). */
export const PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH = 500

/** The largest private attachment the API accepts by default (HpacSafety:Media:PrivateAttachments:MaxByteSize). */
export const PRIVATE_ATTACHMENT_MAX_BYTES = 1024 * 1024 * 1024

/** A staff-only file on a report (ADR-0135). */
export interface PrivateAttachment {
	id: string
	fileName: string
	contentType: string
	byteSize: number
	description: string | null
	/** The adder, as an opaque token subject. */
	addedBy: string
	addedAt: string
	isMine: boolean
}

interface MintedPrivateUpload {
	uploadId: string
	contentType: string
	uploadUrl: string
}

/** Why a private upload failed, for the section's message. */
export class PrivateUploadError extends Error {
	constructor(readonly reason: "too_large" | "empty" | "storage" | "network" | "claim") {
		super(reason)
		this.name = "PrivateUploadError"
	}
}

const privateAttachmentsPath = (reportId: string) => `${reportPath(reportId)}/private-attachments`

export function listPrivateAttachments(reportId: string): Promise<PrivateAttachment[]> {
	return get(privateAttachmentsPath(reportId))
}

export function removePrivateAttachment(reportId: string, attachmentId: string): Promise<void> {
	return call(`${privateAttachmentsPath(reportId)}/${encodeURIComponent(attachmentId)}`, { method: "DELETE" })
}

/** A short-lived link that downloads the file, unchanged, under its name. Each one is audited. */
export function privateAttachmentLink(reportId: string, attachmentId: string): Promise<AttachmentLink> {
	return get(`${privateAttachmentsPath(reportId)}/${encodeURIComponent(attachmentId)}/download`)
}

/**
 * Adds one file: mints a pre-signed PUT to quarantine, sends the file straight
 * to storage with progress, then claims it onto the report (ADR-0135). The API
 * never holds the bytes. Aborting `signal` cancels the send, and an upload that
 * was minted but never claimed is erased; the lifecycle rule is the backstop.
 */
export async function uploadPrivateAttachment(
	reportId: string,
	file: File,
	description: string,
	onProgress: (fraction: number) => void,
	signal: AbortSignal,
): Promise<PrivateAttachment> {
	let minted: MintedPrivateUpload
	try {
		minted = await post<MintedPrivateUpload>(`${privateAttachmentsPath(reportId)}/uploads`, {
			contentType: file.type,
			byteSize: file.size,
		})
	} catch (cause) {
		if (cause instanceof ApiError && cause.status === 400) {
			throw new PrivateUploadError(file.size === 0 ? "empty" : "too_large")
		}
		throw cause
	}

	try {
		await put(minted, file, onProgress, signal)
	} catch (cause) {
		void deleteUpload(minted.uploadId)
		throw cause
	}

	try {
		return await post<PrivateAttachment>(privateAttachmentsPath(reportId), {
			uploadId: minted.uploadId,
			fileName: file.name,
			description: description.trim() || null,
		})
	} catch {
		void deleteUpload(minted.uploadId)
		throw new PrivateUploadError("claim")
	}
}

/**
 * PUTs the file with XMLHttpRequest rather than fetch, because only XHR reports
 * upload progress. No Authorization header: the signature in the URL is the
 * only credential storage takes.
 */
function put(minted: MintedPrivateUpload, file: File, onProgress: (fraction: number) => void, signal: AbortSignal) {
	return new Promise<void>((resolve, reject) => {
		const request = new XMLHttpRequest()
		request.open("PUT", minted.uploadUrl)
		request.setRequestHeader("Content-Type", minted.contentType)
		request.upload.onprogress = (event) => {
			if (event.lengthComputable) onProgress(event.loaded / event.total)
		}
		request.onload = () =>
			Math.floor(request.status / 100) === 2 ? resolve() : reject(new PrivateUploadError("storage"))
		request.onerror = () => reject(new PrivateUploadError("network"))
		request.onabort = () => reject(new DOMException("Aborted", "AbortError"))
		signal.addEventListener("abort", () => request.abort(), { once: true })
		request.send(file)
	})
}
