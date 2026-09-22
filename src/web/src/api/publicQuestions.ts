/*
 * The public, unauthenticated read side of the question bank
 * (src/HpacSafety.Api/PublicQuestions/QuestionEndpoints.cs, issue #270).
 *
 * No bearer token: this is public content, distinct from the admin authoring
 * endpoints and from the member-gated submission endpoint (reportSubmission.ts).
 */

export interface PublicOptionView {
	code: string
	labelEn: string
	labelFr: string
	sourceItemId: string | null
	addedByReporter: boolean
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
	revisionId: string
	type: string
	isRequired: boolean
	isPrivate: boolean
	displayOrder: number
	dependsOnQuestionId: string | null
	dependsOnOptionCode: string | null
	allowsReporterAdditions: boolean
	labelEn: string
	labelFr: string
	helpTextEn: string | null
	helpTextFr: string | null
	placeholderEn: string | null
	placeholderFr: string | null
	options: PublicOptionView[]
	choicesComeFromLiveList: boolean
	children: PublicQuestionView[]
}

export async function fetchCurrentQuestions(): Promise<PublicQuestionView[]> {
	const response = await fetch("/api/v1/questions/")

	if (!response.ok) {
		throw new Error(`The current question set could not be loaded (${response.status}).`)
	}

	return (await response.json()) as PublicQuestionView[]
}
