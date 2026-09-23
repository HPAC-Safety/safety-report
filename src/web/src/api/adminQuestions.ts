/*
 * The admin question-bank client.
 *
 * Every call carries the member's bearer token, because the authorization
 * boundary is the API and never hidden markup (ADR-0048). A 401 means the
 * token is gone or no longer valid, so the stored session is cleared rather
 * than left to draw chrome it cannot back up.
 */

import { clearSession, readSession } from "../auth/session"

/** Every type a question can be, in the order the authoring form offers them. */
export const QUESTION_TYPES = [
	"short_text",
	"long_text",
	"yes_no",
	"single_select",
	"multi_select",
	"autocomplete",
	"date",
	"time",
	"number",
	"email",
	"phone",
	"checkbox",
	"file_upload",
	"statement",
	"group",
] as const

export type QuestionType = (typeof QUESTION_TYPES)[number]

/** Types whose answer is an option code rather than free text. */
export const OPTION_TYPES: readonly QuestionType[] = ["single_select", "multi_select", "autocomplete"]

/**
 * Types that collect no answer — instructional copy or a section heading.
 * Neither can be required, private, a conditional parent, or a conditional
 * child (ADR-0076).
 */
export const NO_ANSWER_TYPES: readonly QuestionType[] = ["statement", "group"]

/** One of a question's own choices (ADR-0095). */
export interface OptionView {
	code: string
	/** Null only on a reporter-added choice typed in French, until an administrator supplies it. */
	labelEn: string | null
	/** Null only on a reporter-added choice typed in English, until an administrator supplies it. */
	labelFr: string | null
	/** A reporter typed this into a type-ahead rather than an administrator writing it (ADR-0063). */
	addedByReporter: boolean
	/** One language is still missing, waiting for an administrator to supply it. */
	needsTranslation: boolean
	/** The language a reporter typed it in, or null. */
	reporterLocale: string | null
}

export interface QuestionView {
	id: string
	key: string
	revisionId: string
	revisionNumber: number
	type: QuestionType
	isSystem: boolean
	isRequired: boolean
	isPrivate: boolean
	isActive: boolean
	displayOrder: number
	dependsOnQuestionId: string | null
	dependsOnOptionCode: string | null
	/** The group question this one renders together with, if any. Distinct from a conditional dependency (ADR-0076). */
	groupedUnderQuestionId: string | null
	labelEn: string
	labelFr: string
	helpTextEn: string | null
	helpTextFr: string | null
	placeholderEn: string | null
	placeholderFr: string | null
	/** The question's own choices, in order; removed ones are left out (ADR-0095). */
	options: OptionView[]
	/** How many reporter-added choices still wait for an administrator to supply a language. */
	reporterChoicesAwaitingReview: number
	/**
	 * Whether any answer references this question. A wording edit to an
	 * answered question retires it and creates a new one in its place
	 * (ADR-0071) — though editing only its choices never does (ADR-0095) — so
	 * the editor says so before the administrator saves.
	 */
	hasBeenAnswered: boolean
}

/**
 * One choice as authored. An administrator names a choice by its wording only:
 * a new choice sends a null code and the server derives it from the English
 * wording; an existing choice sends back the code it already has, so a relabel
 * is never a recode. A reporter-added choice may keep one language blank until
 * someone supplies it; every other choice needs both.
 */
export interface OptionInput {
	code: string | null
	labelEn: string
	labelFr: string
	/** Carried for the editor's marker only; the server keeps its own record of who added a choice. */
	addedByReporter?: boolean
}

export interface SaveQuestionRequest {
	key?: string
	type: QuestionType
	labelEn: string
	labelFr: string
	helpTextEn: string | null
	helpTextFr: string | null
	placeholderEn: string | null
	placeholderFr: string | null
	isRequired: boolean
	isPrivate: boolean
	isActive: boolean
	dependsOnQuestionId: string | null
	dependsOnOptionCode: string | null
	groupedUnderQuestionId: string | null
	/** The complete list of the question's choices, applied in place — never a new version (ADR-0095). */
	options: OptionInput[]
}

/**
 * A rejected call, carrying what the API said rather than a generic failure.
 * `detail` is authored form-definition text — the API never echoes report
 * content or reporter input into a problem response.
 */
export class ApiError extends Error {
	constructor(
		readonly status: number,
		readonly detail: string,
	) {
		super(detail)
		this.name = "ApiError"
	}
}

/** Exposed so a sibling client module (import/export) can reuse the same bearer header. */
export function authorization(): Record<string, string> {
	const session = readSession()

	// No header at all when signed out: the API answers 401 and the page
	// reports that honestly rather than pretending to be signed in.
	return session ? { Authorization: `Bearer ${session.accessToken}` } : {}
}

// Signature split across lines on purpose: tools/check-hardcoded-strings.mjs
// is a line scanner, and `…RequestInit): Promise<T>` on one line reads to it
// as JSX text between a `>` and a `<`.
async function call<T>(
	path: string,
	init?: RequestInit,
): Promise<T> {
	const response = await fetch(path, {
		...init,
		headers: {
			"Content-Type": "application/json",
			...authorization(),
			...init?.headers,
		},
	})

	if (response.status === 401) {
		// The token expired or was revoked while this page was open. Drop it,
		// so the next render draws a signed-out header instead of an admin
		// menu whose every call fails.
		clearSession()
	}

	if (!response.ok) {
		const problem = await response.json().catch(() => null)
		throw new ApiError(response.status, problem?.detail ?? problem?.title ?? response.statusText)
	}

	return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

export function listQuestions(): Promise<QuestionView[]> {
	return call<QuestionView[]>("/api/admin/questions")
}

export function createQuestion(request: SaveQuestionRequest): Promise<QuestionView> {
	return call<QuestionView>("/api/admin/questions", { method: "POST", body: JSON.stringify(request) })
}

export function reviseQuestion(id: string, request: SaveQuestionRequest): Promise<QuestionView> {
	return call<QuestionView>(`/api/admin/questions/${id}`, { method: "PUT", body: JSON.stringify(request) })
}

export function reorderQuestions(questionIdsInOrder: string[]): Promise<QuestionView[]> {
	return call<QuestionView[]>("/api/admin/questions/order", {
		method: "POST",
		body: JSON.stringify({ questionIdsInOrder }),
	})
}

export function deleteQuestion(id: string): Promise<void> {
	return call<void>(`/api/admin/questions/${id}`, { method: "DELETE" })
}

/**
 * Whether the server has a translation provider configured. Asked once, so the
 * Translate control can be disabled rather than offered and then failing.
 */
export function translationAvailable(): Promise<{ available: boolean; standIn: boolean }> {
	return call<{ available: boolean; standIn: boolean }>("/api/admin/translate")
}

/**
 * Translates authored question text between the two official languages.
 *
 * The request goes to our own API, never to a translation provider from the
 * browser — the credential stays on the server (ADR-0062). Blank entries come
 * back blank, and results line up positionally with what was sent.
 */
export function translate(texts: string[], from: string, to: string): Promise<{ texts: string[] }> {
	return call<{ texts: string[] }>("/api/admin/translate", {
		method: "POST",
		body: JSON.stringify({ texts, from, to }),
	})
}

/** One answer waiting for an administrator to supply its second language. */
export interface AwaitingTranslationView {
	id: string
	questionKey: string
	value: string
	locale: string
	into: string
}

/**
 * The answers waiting for a second official language.
 *
 * A reporter answers a picker or a type-ahead in one language and nothing on
 * the submission path translates it (ADR-0072). This is the queue where an
 * administrator supplies the other one.
 */
export function listAnswersAwaitingTranslation(): Promise<{
	answers: AwaitingTranslationView[]
	waiting: number
}> {
	return call<{ answers: AwaitingTranslationView[]; waiting: number }>(
		"/api/admin/answers/awaiting-translation",
	)
}

/**
 * Supplies an answer's second language. The reporter's own value is never
 * changed — this fills the language they did not answer in.
 */
export function supplyAnswerTranslation(id: string, value: string): Promise<void> {
	return call<void>(`/api/admin/answers/${id}/translation`, {
		method: "PUT",
		body: JSON.stringify({ value }),
	})
}
