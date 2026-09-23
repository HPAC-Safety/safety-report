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
	/** The one locale a reporter-added choice is worded in, or null when it has both (ADR-0095). */
	onlyIn: string | null
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
						{ code: "hang_glider", labelEn: "Hang glider", labelFr: "Deltaplane", onlyIn: null },
						{ code: "paraglider", labelEn: "Paraglider", labelFr: "Parapente", onlyIn: null },
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
				{ code: "gusty", labelEn: "Gusty", labelFr: "Rafales", onlyIn: null },
				{ code: "thermic", labelEn: "Thermic", labelFr: "Thermique", onlyIn: null },
				{ code: "turbulent", labelEn: "Turbulent", labelFr: "Turbulent", onlyIn: null },
			],
		}),
	)
	return questions
}

/**
 * The default form with a type-ahead as its first answer-producing page. One of
 * its choices was added by a reporter in English and has no French yet, so the
 * server sends that English wording in both labels (REQ-QB-103).
 */
export function typeAheadFormQuestions(): StubQuestion[] {
	const questions = defaultFormQuestions()
	questions.splice(
		1,
		0,
		question({
			id: "launch_site",
			key: "launch_site",
			labelEn: "Where did you launch?",
			type: "autocomplete",
			displayOrder: 1,
			allowsReporterAdditions: true,
			options: [
				{ code: "coopers", labelEn: "Cooper's Hill", labelFr: "Colline Cooper", onlyIn: null },
				{ code: "mount_7", labelEn: "Mount 7", labelFr: "Mount 7", onlyIn: "en-CA" },
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

/** The upload a saved report names, as the form would have written it after a finished upload. */
export const SAVED_UPLOAD = { uploadId: "synthetic-upload-saved01", name: "saved-photo.png", size: 2048 }

/**
 * A saved report started sixteen days ago and saved again an hour ago: past
 * its window, which runs from the first save however recently it was edited
 * (ADR-0100). It names one upload, which the form must delete.
 */
export async function writeStaleDraftToBrowser(page: Page) {
	await page.addInitScript((upload) => {
		const sixteenDaysAgo = Date.now() - 16 * 24 * 60 * 60 * 1000
		localStorage.setItem(
			"hpac.report.draft",
			JSON.stringify({
				locale: "en-CA",
				answers: { "rev-narrative": { kind: "value", value: "stale" } },
				attachments: { "rev-attachments": [upload] },
				startedAtMs: sixteenDaysAgo,
				savedAtMs: Date.now() - 60 * 60 * 1000,
			}),
		)
	}, SAVED_UPLOAD)
}

/**
 * A saved, unexpired report as the form would have written it: a narrative,
 * "yes" to the injury question, its conditional detail, a group child, one
 * uploaded file, the page the reporter was last on, and one answer to a
 * revision the current form no longer shows. Written once, before the first
 * page load, so a later navigation does not put it back after the reporter
 * removes it.
 */
export async function writeSavedDraftToBrowser(page: Page) {
	await page.addInitScript((upload) => {
		if (sessionStorage.getItem("hpac.test.savedDraftWritten")) return
		sessionStorage.setItem("hpac.test.savedDraftWritten", "1")
		localStorage.setItem(
			"hpac.report.draft",
			JSON.stringify({
				locale: "en-CA",
				answers: {
					"rev-narrative": { kind: "value", value: "A saved synthetic narrative." },
					"rev-injured": { kind: "value", value: "yes" },
					"rev-injury_detail": { kind: "value", value: "A synthetic sprain." },
					"rev-aircraft_type": { kind: "value", value: "Paraglider" },
					"rev-retired": { kind: "value", value: "An answer to a retired question." },
				},
				attachments: { "rev-attachments": [upload] },
				stepKey: "injury_detail",
				startedAtMs: Date.now() - 2 * 24 * 60 * 60 * 1000,
				savedAtMs: Date.now() - 60 * 60 * 1000,
			}),
		)
	}, SAVED_UPLOAD)
}

/** Removes the saved report, so a reload opens the form without asking whether to continue. */
export async function forgetDraftInBrowser(page: Page) {
	await page.evaluate(() => localStorage.removeItem("hpac.report.draft"))
}
