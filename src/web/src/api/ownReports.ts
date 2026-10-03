/*
 * A reporter's own report before it is published (src/HpacSafety.Api/PublicReports/
 * OwnReportEndpoints.cs, issue no. 820, ADR-0196).
 *
 * A successful submission returns a receipt, a random token that proves "this
 * browser filed it" and never "this member filed it". This browser keeps
 * `{ reportId, receipt }` for each report it filed, in `localStorage`, and sends
 * them to the API in a request body — never in an address, a query string, or a
 * header. Nothing about the member is kept. Clearing site data loses the receipts,
 * and so the reports: they are visible only in the browser that filed them.
 *
 * A receipt is dropped once the API says its report is no longer the browser's
 * own: published (it is then in the public feed), deleted, or unknown.
 */

import type { PublicMedia, PublicMediaLink } from "./publicReports"
import { PublicReportNotFound } from "./publicReports"

const STORAGE_KEY = "hpac.report.receipts"

/** The most receipts kept, and sent in one lookup; the API reads at most this many. */
const MAX_RECEIPTS = 50

export interface StoredReceipt {
	reportId: string
	receipt: string
}

/** One of this browser's own reports as the feed shows it. */
export interface OwnReport {
	id: string
	submittedAt: string
	/** False when the reporter did not consent to publication: the report never has a summary. */
	forPublication: boolean
	/** The latest summary revision, approved or not; null before the Worker has made one. */
	aiSummaryEn: string | null
	aiSummaryFr: string | null
	attachmentCount: number
}

/** One of this browser's own reports' pages. */
export interface OwnReportDetail extends OwnReport {
	language: "en-CA" | "fr-CA"
	media: PublicMedia[]
}

interface OwnReportsResponse {
	items: OwnReport[]
	settled: string[]
}

function isStoredReceipt(value: unknown): value is StoredReceipt {
	return (
		typeof value === "object" &&
		value !== null &&
		typeof (value as StoredReceipt).reportId === "string" &&
		typeof (value as StoredReceipt).receipt === "string"
	)
}

/** Every receipt this browser holds, newest last. Empty when storage is unavailable or unreadable. */
export function readReceipts(): StoredReceipt[] {
	try {
		const raw = window.localStorage.getItem(STORAGE_KEY)
		const parsed: unknown = raw ? JSON.parse(raw) : []
		return Array.isArray(parsed) ? parsed.filter(isStoredReceipt) : []
	} catch {
		return []
	}
}

function writeReceipts(receipts: StoredReceipt[]) {
	try {
		if (receipts.length === 0) {
			window.localStorage.removeItem(STORAGE_KEY)
		} else {
			window.localStorage.setItem(STORAGE_KEY, JSON.stringify(receipts))
		}
	} catch {
		// Private browsing or a full quota: the report is filed all the same; this
		// browser just will not show it before it is published.
	}
}

/** Keeps the receipt a submission returned. Only the report ID and the receipt — nothing about the member. */
export function saveReceipt(reportId: string, receipt: string) {
	const others = readReceipts().filter((stored) => stored.reportId !== reportId)
	writeReceipts([...others, { reportId, receipt }].slice(-MAX_RECEIPTS))
}

/** The receipt this browser holds for a report, or null. */
export function receiptFor(reportId: string): string | null {
	return readReceipts().find((stored) => stored.reportId === reportId)?.receipt ?? null
}

/** Forgets the receipts for these reports. */
export function dropReceipts(reportIds: string[]) {
	if (reportIds.length === 0) return
	const dropped = new Set(reportIds)
	writeReceipts(readReceipts().filter((stored) => !dropped.has(stored.reportId)))
}

async function post(path: string, body: unknown): Promise<Response> {
	return await fetch(path, {
		method: "POST",
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify(body),
	})
}

/**
 * This browser's own reports that are not published, newest submitted first, and
 * drops every receipt the API says is no longer its own. Makes no request at all
 * when the browser holds no receipt. A failed lookup shows none: the public feed
 * is unaffected.
 */
export async function fetchOwnReports(): Promise<OwnReport[]> {
	const receipts = readReceipts()
	if (receipts.length === 0) return []

	try {
		const response = await post("/api/v1/public/reports/own", { receipts })
		if (!response.ok) return []

		const body = (await response.json()) as OwnReportsResponse
		dropReceipts(body.settled)
		return body.items
	} catch {
		return []
	}
}

/**
 * One of this browser's own reports, or null when the browser holds no receipt for
 * it or the API says it is no longer its own — in which case the receipt is dropped.
 */
export async function fetchOwnReport(reportId: string): Promise<OwnReportDetail | null> {
	const receipt = receiptFor(reportId)
	if (!receipt) return null

	const response = await post(`/api/v1/public/reports/own/${encodeURIComponent(reportId)}`, { receipt })

	if (response.status === 404) {
		dropReceipts([reportId])
		return null
	}

	if (!response.ok) {
		throw new Error(`The report could not be loaded (${response.status}).`)
	}

	return (await response.json()) as OwnReportDetail
}

/** A fresh link to one of this browser's own report's files, or PublicReportNotFound once it is not there. */
export async function fetchOwnMediaLink(reportId: string, mediaId: string, receipt: string): Promise<PublicMediaLink> {
	const response = await post(
		`/api/v1/public/reports/own/${encodeURIComponent(reportId)}/media/${encodeURIComponent(mediaId)}`,
		{ receipt },
	)

	if (response.status === 404) {
		throw new PublicReportNotFound()
	}

	if (!response.ok) {
		throw new Error(`The media link could not be loaded (${response.status}).`)
	}

	return (await response.json()) as PublicMediaLink
}

/** The summary in the given locale, or null before there is one. */
export function ownSummaryIn(report: OwnReport, locale: string): string | null {
	return locale === "fr-CA" ? report.aiSummaryFr : report.aiSummaryEn
}
