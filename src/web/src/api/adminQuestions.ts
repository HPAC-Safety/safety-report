/*
 * The admin question-bank client.
 *
 * Every call carries the member-session marker as a header, because the
 * authorization boundary is the API and never hidden markup (ADR-0048). The
 * marker itself is a stub standing in for ADR-0005's authenticator — see
 * src/HpacSafety.Api/Admin/AdminGate.cs. When that lands, the header here is
 * replaced by a real token and nothing else on this page changes.
 */

const SESSION_HEADER = "X-Hpac-Member-Session"
const SESSION_STORAGE_KEY = "hpac.memberSession"

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

export interface OptionView {
	code: string
	labelEn: string
	labelFr: string
	sourceItemId: string | null
	/** A reporter typed this into a type-ahead; an administrator has not reviewed it. */
	addedByReporter: boolean
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
	sectionKey: string | null
	dependsOnQuestionId: string | null
	optionSetId: string | null
	labelEn: string
	labelFr: string
	helpTextEn: string | null
	helpTextFr: string | null
	placeholderEn: string | null
	placeholderFr: string | null
	options: OptionView[]
	/**
	 * True when this question's choices are read from the live shared list
	 * rather than from the revision's own snapshot — which is how a site a
	 * reporter added shows up for the next one (ADR-0063).
	 */
	choicesComeFromLiveList: boolean
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
	sectionKey: string | null
	dependsOnQuestionId: string | null
	optionSetId: string | null
	options: { code: string; labelEn: string; labelFr: string }[]
}

export interface OptionSetView {
	id: string
	key: string
	nameEn: string
	nameFr: string
	items: OptionView[]
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

function sessionMarker(): string {
	try {
		return sessionStorage.getItem(SESSION_STORAGE_KEY) ?? ""
	} catch {
		// Storage can be unavailable; the call then fails with 401, which the
		// page reports honestly rather than pretending to be signed in.
		return ""
	}
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
			[SESSION_HEADER]: sessionMarker(),
			...init?.headers,
		},
	})

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

export interface SaveOptionSetRequest {
	key?: string
	nameEn: string
	nameFr: string
	items: { code: string; labelEn: string; labelFr: string }[]
}

export function listOptionSets(): Promise<OptionSetView[]> {
	return call<OptionSetView[]>("/api/admin/option-sets")
}

export function createOptionSet(request: SaveOptionSetRequest): Promise<OptionSetView> {
	return call<OptionSetView>("/api/admin/option-sets", { method: "POST", body: JSON.stringify(request) })
}

export function replaceOptionSet(id: string, request: SaveOptionSetRequest): Promise<OptionSetView> {
	return call<OptionSetView>(`/api/admin/option-sets/${id}`, { method: "PUT", body: JSON.stringify(request) })
}

export function deleteOptionSet(id: string): Promise<void> {
	return call<void>(`/api/admin/option-sets/${id}`, { method: "DELETE" })
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
