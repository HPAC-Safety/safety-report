import type { Page } from "@playwright/test"

/*
 * One synthetic current-question set, shared by every report-form scenario.
 *
 * Shape mirrors src/HpacSafety.Api/PublicQuestions/QuestionContracts.cs
 * exactly: a leading `statement` (the introduction), an ordinary text
 * question, a yes/no question with a conditional child, a `group` with two
 * children, a file upload, and the required `consent_publish` question last.
 * Every question is synthetic.
 */

export interface StubOption {
	code: string
	labelEn: string
	labelFr: string
	sourceItemId: string | null
	addedByReporter: boolean
}

export interface StubQuestion {
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
	options: StubOption[]
	choicesComeFromLiveList: boolean
	children: StubQuestion[]
}

function question(overrides: Partial<StubQuestion> & { id: string; key: string; labelEn: string; type: string; displayOrder: number }): StubQuestion {
	return {
		revisionId: `rev-${overrides.id}`,
		isRequired: false,
		isPrivate: false,
		dependsOnQuestionId: null,
		dependsOnOptionCode: null,
		allowsReporterAdditions: false,
		labelFr: `${overrides.labelEn} (fr)`,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		choicesComeFromLiveList: false,
		children: [],
		...overrides,
	}
}

export function defaultFormQuestions(): StubQuestion[] {
	return [
		question({
			id: "intro",
			key: "intro",
			labelEn: "Thanks for taking the time to report a safety occurrence.",
			type: "statement",
			displayOrder: 0,
		}),
		question({ id: "narrative", key: "narrative", labelEn: "What happened?", type: "long_text", displayOrder: 1 }),
		question({ id: "injured", key: "was_injured", labelEn: "Was anyone injured?", type: "yes_no", displayOrder: 2 }),
		question({
			id: "injury_detail",
			key: "injury_detail",
			labelEn: "Describe the injury",
			type: "short_text",
			displayOrder: 3,
			dependsOnQuestionId: "injured",
		}),
		question({
			id: "aircraft",
			key: "aircraft",
			labelEn: "Aircraft:",
			type: "group",
			displayOrder: 4,
			children: [
				question({
					id: "aircraft_type",
					key: "aircraft_type",
					labelEn: "Type of aircraft",
					type: "single_select",
					displayOrder: 0,
					options: [
						{ code: "hang_glider", labelEn: "Hang glider", labelFr: "Deltaplane", sourceItemId: null, addedByReporter: false },
						{ code: "paraglider", labelEn: "Paraglider", labelFr: "Parapente", sourceItemId: null, addedByReporter: false },
					],
				}),
				question({ id: "aircraft_model", key: "aircraft_model", labelEn: "Model", type: "short_text", displayOrder: 1 }),
			],
		}),
		question({ id: "attachments", key: "attachments", labelEn: "Photos or videos", type: "file_upload", displayOrder: 5 }),
		question({
			id: "consent",
			key: "consent_publish",
			labelEn: "May we publish a summary of this report?",
			type: "yes_no",
			displayOrder: 6,
			isRequired: true,
		}),
	]
}

/** The default form with a "Pick several" question as its first answer-producing page (REQ-SUB-034). */
export function multiSelectFormQuestions(): StubQuestion[] {
	const questions = defaultFormQuestions()
	questions.splice(
		1,
		0,
		question({
			id: "conditions",
			key: "conditions",
			labelEn: "Which conditions applied?",
			type: "multi_select",
			displayOrder: 1,
			options: [
				{ code: "gusty", labelEn: "Gusty", labelFr: "Rafales", sourceItemId: null, addedByReporter: false },
				{ code: "thermic", labelEn: "Thermic", labelFr: "Thermique", sourceItemId: null, addedByReporter: false },
				{ code: "turbulent", labelEn: "Turbulent", labelFr: "Turbulent", sourceItemId: null, addedByReporter: false },
			],
		}),
	)
	return questions
}

export async function stubCurrentQuestions(page: Page, questions: StubQuestion[] = defaultFormQuestions()) {
	await page.route("**/api/v1/questions/", (route) =>
		route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(questions) }),
	)
}

export interface SubmissionStubOptions {
	status?: number
	delayMs?: number
	networkError?: boolean
	body?: unknown
}

export async function stubSubmission(page: Page, options: SubmissionStubOptions = {}) {
	await page.route("**/api/v1/reports/", async (route) => {
		if (options.networkError) {
			await route.abort("failed")
			return
		}
		if (options.delayMs) {
			await new Promise((resolve) => setTimeout(resolve, options.delayMs))
		}
		await route.fulfill({
			status: options.status ?? 202,
			contentType: "application/json",
			body: JSON.stringify(options.body ?? { id: "synthetic-report-id", status: "submitted" }),
		})
	})
}

export async function readDraftFromBrowser(page: Page): Promise<unknown> {
	return page.evaluate(() => {
		const raw = localStorage.getItem("hpac.report.draft")
		return raw ? JSON.parse(raw) : null
	})
}

export async function writeStaleDraftToBrowser(page: Page) {
	await page.addInitScript(() => {
		const sixteenDaysAgo = Date.now() - 16 * 24 * 60 * 60 * 1000
		localStorage.setItem(
			"hpac.report.draft",
			JSON.stringify({ locale: "en-CA", answers: { "rev-narrative": { kind: "value", value: "stale" } }, savedAtMs: sixteenDaysAgo }),
		)
	})
}
