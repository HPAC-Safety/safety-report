/*
 * Browser-only continuity for the in-progress report (AGENTS.md invariant #2,
 * `features/report-submission/report-submission.feature`'s "browser holds
 * report state locally" scenarios).
 *
 * What is kept: the selected locale, the shown revision IDs, entered answer
 * values, and the page the reporter was on — for at most 15 days, and never
 * past a successful submit.
 * What is never kept: a `File` object, a filename, or anything that would
 * make a reload skip re-selecting an attachment.
 */

const STORAGE_KEY = "hpac.report.draft"
const MAX_AGE_MS = 15 * 24 * 60 * 60 * 1000

/** One answer as the draft holds it — never a file. */
export type DraftAnswer = { kind: "value"; value: string } | { kind: "options"; values: string[] }

export interface ReportDraft {
	locale: string
	/** Keyed by question-revision ID, matching the revisions the reporter was actually shown. */
	answers: Record<string, DraftAnswer>
	/** The key of the question heading the page the reporter was last on — the same key the page's address names. */
	stepKey?: string
	/** What a draft saved before #366 names that page by; still honoured when reading, never written. */
	stepRevisionId?: string
	savedAtMs: number
}

function isDraft(value: unknown): value is ReportDraft {
	if (!value || typeof value !== "object") return false
	const candidate = value as Partial<ReportDraft>
	return (
		typeof candidate.locale === "string" &&
		typeof candidate.savedAtMs === "number" &&
		typeof candidate.answers === "object" &&
		candidate.answers !== null
	)
}

/** The saved draft, or null if there is none or it is older than 15 days. */
export function readDraft(clockNowMs: number = Date.now()): ReportDraft | null {
	try {
		const raw = localStorage.getItem(STORAGE_KEY)
		if (!raw) return null

		const parsed = JSON.parse(raw) as unknown
		if (!isDraft(parsed)) return null

		if (clockNowMs - parsed.savedAtMs > MAX_AGE_MS) {
			clearDraft()
			return null
		}

		return parsed
	} catch {
		// Storage unavailable or corrupt: no draft, not a crash.
		return null
	}
}

export function writeDraft(draft: Omit<ReportDraft, "savedAtMs">, clockNowMs: number = Date.now()): void {
	try {
		localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...draft, savedAtMs: clockNowMs }))
	} catch {
		// The draft still applies to this page view; it just will not survive a reload.
	}
}

/** Called only after a definite 202 — never on an ambiguous network result. */
export function clearDraft(): void {
	try {
		localStorage.removeItem(STORAGE_KEY)
	} catch {
		// Nothing to clear if storage never worked.
	}
}
