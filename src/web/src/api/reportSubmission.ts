/*
 * The one final report write (src/HpacSafety.Api/Reports/
 * ReportSubmissionEndpoints.cs, issue #14). Nothing on this module's path runs
 * before the reporter presses Submit — see report-form/draft.ts for the
 * browser-only state that exists until then. Attachments were already uploaded
 * as they were attached (api/uploads.ts, ADR-0096); this names them.
 */

import { authorization } from "./adminQuestions"

/** One uploaded file a file-upload answer claims, with the reporter's name for it (ADR-0097). */
export interface SubmitAttachment {
	uploadId: string
	fileName: string
}

/** Exactly one of `value`, `choices`, or `attachments` carries data. */
export interface SubmitAnswer {
	questionRevisionId: string
	value: string | null
	/** A multi-select answer's chosen labels, in the reporter's language. */
	choices: string[] | null
	attachments: SubmitAttachment[] | null
}

export interface SubmitReportResult {
	id: string
	status: string
}

/** A submission the API rejected, carrying its safe, localized detail text. */
export class SubmissionRejectedError extends Error {
	constructor(
		readonly detail: string,
		/** Uploads the API could not find — expired, and to be attached again. */
		readonly expiredUploadIds: string[] = [],
	) {
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

/** Sends the one JSON request, naming each attachment by the upload id its upload returned. */
export async function submitReport(locale: string, answers: SubmitAnswer[]): Promise<SubmitReportResult> {
	let response: Response
	try {
		response = await fetch("/api/v1/reports/", {
			method: "POST",
			headers: { ...authorization(), "Content-Type": "application/json" },
			body: JSON.stringify({ language: locale, answers }),
		})
	} catch {
		throw new SubmissionNetworkError()
	}

	if (response.status === 202) {
		return (await response.json()) as SubmitReportResult
	}

	const problem = await response.json().catch(() => null)
	throw new SubmissionRejectedError(
		problem?.detail ?? problem?.title ?? "That submission was not accepted.",
		Array.isArray(problem?.expiredUploadIds) ? (problem.expiredUploadIds as string[]) : [],
	)
}
