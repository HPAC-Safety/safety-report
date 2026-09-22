/*
 * The one final, reporter-facing write (src/HpacSafety.Api/Reports/
 * ReportSubmissionEndpoints.cs, issue #14). Nothing on this module's path runs
 * before the reporter presses Submit — see report-form/draft.ts for the
 * browser-only state that exists until then.
 */

import { authorization } from "./adminQuestions"

/** Exactly one of `value`, `optionCodes`, or `attachmentPartIndexes` carries data. */
export interface SubmitAnswer {
	questionRevisionId: string
	value: string | null
	optionCodes: string[] | null
	attachmentPartIndexes: number[] | null
}

export interface SubmitReportResult {
	id: string
	status: string
}

/** A submission the API rejected, carrying its safe, localized detail text. */
export class SubmissionRejectedError extends Error {
	constructor(readonly detail: string) {
		super(detail)
		this.name = "SubmissionRejectedError"
	}
}

/** A network-level failure — the request never reached a response. Distinct from a rejection, because local state must survive it (ADR: "A network failure preserves local state"). */
export class SubmissionNetworkError extends Error {
	constructor() {
		super("The report could not be sent.")
		this.name = "SubmissionNetworkError"
	}
}

/**
 * Sends the one multipart request: the JSON report part, plus every file the
 * reporter attached, referenced positionally from `answers`.
 */
export async function submitReport(
	locale: string,
	answers: SubmitAnswer[],
	files: File[],
): Promise<SubmitReportResult> {
	const form = new FormData()
	form.append("report", JSON.stringify({ language: locale, answers }))
	for (const file of files) {
		form.append("files", file, file.name)
	}

	let response: Response
	try {
		response = await fetch("/api/v1/reports/", {
			method: "POST",
			headers: authorization(),
			body: form,
		})
	} catch {
		throw new SubmissionNetworkError()
	}

	if (response.status === 202) {
		return (await response.json()) as SubmitReportResult
	}

	const problem = await response.json().catch(() => null)
	throw new SubmissionRejectedError(problem?.detail ?? problem?.title ?? "That submission was not accepted.")
}
