/*
 * The anonymous public feed of published reports and each report's own page
 * (src/HpacSafety.Api/PublicReports/PublicReportEndpoints.cs, issue no. 28).
 *
 * No bearer token: a published summary is public content. Each item is the
 * whole public allowlist — the opaque ID, both approved summary texts, and when
 * it was published — and nothing else about a report.
 */

export interface PublicReport {
	id: string
	aiSummaryEn: string
	aiSummaryFr: string
	publishedAt: string
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
