/*
 * The anonymous public feed of published reports and each report's own page
 * (src/HpacSafety.Api/PublicReports/PublicReportEndpoints.cs, issue no. 28).
 *
 * No bearer token for reading a report: a published summary is public content.
 * Each item is the whole public allowlist — the opaque ID, both approved summary
 * texts, when it was published, and how many comments it has — and nothing else
 * about a report.
 *
 * Comments (ADR-0114) are read by anyone. When a session exists the token is
 * sent with the read too, only so the API can mark the reader's own comments.
 * Writing needs a member; hiding needs a reviewer.
 */

import { ApiError, authorization } from "./adminQuestions"

export interface PublicReport {
	id: string
	aiSummaryEn: string
	aiSummaryFr: string
	publishedAt: string
	/** Comments that are neither deleted nor hidden. */
	commentCount: number
}

/** One page of the feed, newest first; `next` continues it, or is null on the last page. */
export interface PublicReportPage {
	items: PublicReport[]
	next: string | null
}

/** Thrown when a report is not public — the API does not say whether it exists. */
export class PublicReportNotFound extends Error {
	constructor() {
		super("That report is not public.")
		this.name = "PublicReportNotFound"
	}
}

export async function fetchPublicReports(after: string | null): Promise<PublicReportPage> {
	const query = after ? `?after=${encodeURIComponent(after)}` : ""
	const response = await fetch(`/api/v1/public/reports${query}`)

	if (!response.ok) {
		throw new Error(`The published reports could not be loaded (${response.status}).`)
	}

	return (await response.json()) as PublicReportPage
}

export async function fetchPublicReport(id: string): Promise<PublicReport> {
	const response = await fetch(`/api/v1/public/reports/${encodeURIComponent(id)}`)

	if (response.status === 404) {
		throw new PublicReportNotFound()
	}

	if (!response.ok) {
		throw new Error(`The report could not be loaded (${response.status}).`)
	}

	return (await response.json()) as PublicReport
}

/** The summary text in the given locale, falling back to English. */
export function summaryIn(report: PublicReport, locale: string): string {
	return locale === "fr-CA" ? report.aiSummaryFr : report.aiSummaryEn
}

/** One visible comment. Never carries who wrote it; `isMine` is computed by the API. */
export interface PublicComment {
	id: string
	text: string
	locale: string
	translatedText: string | null
	createdAt: string
	updatedAt: string
	edited: boolean
	isMine: boolean
}

/** The longest comment the API accepts. */
export const COMMENT_MAX_LENGTH = 2000

// Signature split across lines on purpose: tools/check-hardcoded-strings.mjs
// is a line scanner (see adminQuestions.ts).
async function send<T>(
	path: string,
	init?: RequestInit,
): Promise<T> {
	const response = await fetch(path, {
		...init,
		headers: { "Content-Type": "application/json", ...authorization(), ...init?.headers },
	})

	if (response.status === 404) {
		throw new PublicReportNotFound()
	}

	if (!response.ok) {
		const problem = await response.json().catch(() => null)
		throw new ApiError(response.status, problem?.detail ?? problem?.title ?? response.statusText)
	}

	return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

function commentsOf(reportId: string): string {
	return `/api/v1/public/reports/${encodeURIComponent(reportId)}/comments`
}

export function listComments(reportId: string): Promise<PublicComment[]> {
	return send<PublicComment[]>(`${commentsOf(reportId)}/`)
}

export function postComment(reportId: string, text: string, locale: string): Promise<PublicComment> {
	return send<PublicComment>(`${commentsOf(reportId)}/`, { method: "POST", body: JSON.stringify({ text, locale }) })
}

export function editComment(reportId: string, commentId: string, text: string, locale: string): Promise<PublicComment> {
	return send<PublicComment>(`${commentsOf(reportId)}/${encodeURIComponent(commentId)}`, {
		method: "PUT",
		body: JSON.stringify({ text, locale }),
	})
}

export function deleteComment(reportId: string, commentId: string): Promise<void> {
	return send<void>(`${commentsOf(reportId)}/${encodeURIComponent(commentId)}`, { method: "DELETE" })
}

export function hideComment(commentId: string): Promise<void> {
	return send<void>(`/api/admin/comments/${encodeURIComponent(commentId)}/hide`, { method: "POST" })
}
