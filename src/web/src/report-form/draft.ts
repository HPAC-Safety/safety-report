/*
 * Browser-only continuity for the in-progress report (AGENTS.md invariant #2,
 * `features/report-submission/report-submission.feature`'s "browser holds
 * report state locally" scenarios).
 *
 * What is kept: the selected locale, the shown revision IDs, entered answer
 * values, the page the reporter was on, and each finished upload's ID, name,
 * and size — for at most 15 days from the first save, and never past a
 * successful submit (ADR-0100).
 * What is never kept: a `File` object or a file's bytes.
 */

const STORAGE_KEY = "hpac.report.draft"
const MAX_AGE_MS = 15 * 24 * 60 * 60 * 1000

/**
 * One answer as the draft holds it — never a file. A phone answer holds the
 * number as the field shows it, with the country it was typed for (ADR-0137).
 * A type-ahead answer picked from its list also names that choice's ID, so it
 * is sent as that choice even where another carries the same wording.
 */
export type DraftAnswer = { kind: "value"; value: string; country?: string; choice?: string } | { kind: "options"; values: string[] }

/** One finished upload, as the draft names it; the bytes stay in quarantine. */
export interface DraftAttachment {
	uploadId: string
	name: string
	size: number
}

export interface ReportDraft {
	locale: string
	/** Keyed by question-revision ID, matching the revisions the reporter was actually shown. */
	answers: Record<string, DraftAnswer>
	/** Finished uploads per file-upload question, keyed by question-revision ID. */
	attachments?: Record<string, DraftAttachment[]>
	/** The key of the question heading the page the reporter was last on — the same key the page's address names. */
	stepKey?: string
	/** What a draft saved before pages were saved by key names that page by; still honoured when reading, never written. */
	stepRevisionId?: string
	/**
	 * When the draft was first saved. The 15 days run from here, however often
	 * it is saved again, because its uploads expire 15 days after they were
	 * made. A draft saved before this was recorded is dated from `savedAtMs`.
	 */
	startedAtMs?: number
	savedAtMs: number
}

/** What the browser holds: a draft to offer, and the uploads of one that has just expired. */
export interface DraftRead {
	draft: ReportDraft | null
	/** Uploads named by an expired draft, for the caller to delete; the draft itself is already gone. */
	expiredUploadIds: string[]
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

function startOf(draft: ReportDraft): number {
	return typeof draft.startedAtMs === "number" ? draft.startedAtMs : draft.savedAtMs
}

function isExpired(draft: ReportDraft, clockNowMs: number): boolean {
	return clockNowMs - startOf(draft) > MAX_AGE_MS
}

function stored(): ReportDraft | null {
	const raw = localStorage.getItem(STORAGE_KEY)
	if (!raw) return null
	const parsed = JSON.parse(raw) as unknown
	return isDraft(parsed) ? parsed : null
}

/** Every upload a draft names. */
export function draftUploadIds(draft: ReportDraft): string[] {
	return Object.values(draft.attachments ?? {})
		.flat()
		.map((attachment) => attachment.uploadId)
}

/** The saved draft, or none if it is older than 15 days — in which case its uploads are returned for deleting. */
export function readDraft(clockNowMs: number = Date.now()): DraftRead {
	try {
		const draft = stored()
		if (!draft) return { draft: null, expiredUploadIds: [] }

		if (isExpired(draft, clockNowMs)) {
			clearDraft()
			return { draft: null, expiredUploadIds: draftUploadIds(draft) }
		}

		return { draft, expiredUploadIds: [] }
	} catch {
		// Storage unavailable or corrupt: no draft, not a crash.
		return { draft: null, expiredUploadIds: [] }
	}
}

/** Saves the draft, keeping the start time of the one already saved unless that one has expired. */
export function writeDraft(draft: Omit<ReportDraft, "savedAtMs" | "startedAtMs">, clockNowMs: number = Date.now()): void {
	try {
		let startedAtMs = clockNowMs
		try {
			const previous = stored()
			if (previous && !isExpired(previous, clockNowMs)) startedAtMs = startOf(previous)
		} catch {
			// A corrupt earlier draft starts a new window.
		}
		localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...draft, startedAtMs, savedAtMs: clockNowMs }))
	} catch {
		// The draft still applies to this page view; it just will not survive a reload.
	}
}

/** Called after a definite 202, or when the reporter abandons the report — never on an ambiguous network result. */
export function clearDraft(): void {
	try {
		localStorage.removeItem(STORAGE_KEY)
	} catch {
		// Nothing to clear if storage never worked.
	}
}
