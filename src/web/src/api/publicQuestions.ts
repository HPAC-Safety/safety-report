/*
 * The public, unauthenticated read side of the question bank
 * (src/HpacSafety.Api/PublicQuestions/QuestionEndpoints.cs, issue no. 270).
 *
 * No bearer token: this is public content, distinct from the admin authoring
 * endpoints and from the member-gated submission endpoint (reportSubmission.ts).
 */

/**
 * One of a question's own choices. A reporter-added choice may have only one
 * language yet: both labels then carry that wording, and `onlyIn` names the
 * language it is in, so the form can mark it (ADR-0095).
 */
export interface PublicOptionView {
	/** The choice's identifier: what a submitted answer names (ADR-0128). */
	id: string
	/** The invariant code a conditional question names (ADR-0074). */
	code: string
	labelEn: string
	labelFr: string
	onlyIn: string | null
	/** Listed before (`first`) or after (`last`) the alphabetical rest, or among them (`none`) — ADR-0136. */
	pin: string
}

/**
 * One question as the server currently shows it to a reporter: its live
 * revision, bilingual, with a group's children nested inside it exactly the
 * way the client should render them together (never repeated at the top
 * level).
 */
export interface PublicQuestionView {
	id: string
	key: string
	/** What logic reads the answer for: `consent_publish`, `consent_media` (ADR-0117), or `none`. */
	role: string
	revisionId: string
	type: string
	isRequired: boolean
	isPrivate: boolean
	displayOrder: number
	dependsOnQuestionId: string | null
	/** The parent's required choice, by ID — for a replaced option, the option that replaced it (ADR-0128). */
	dependsOnChoiceId: string | null
	allowsReporterAdditions: boolean
	labelEn: string
	labelFr: string
	helpTextEn: string | null
	helpTextFr: string | null
	placeholderEn: string | null
	placeholderFr: string | null
	options: PublicOptionView[]
	children: PublicQuestionView[]
}

export async function fetchCurrentQuestions(): Promise<PublicQuestionView[]> {
	const response = await fetch("/api/v1/questions/")

	if (!response.ok) {
		throw new Error(`The current question set could not be loaded (${response.status}).`)
	}

	return (await response.json()) as PublicQuestionView[]
}
