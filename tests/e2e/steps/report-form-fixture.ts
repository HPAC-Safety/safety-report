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
	/** The choice's identifier, which a submitted answer names (ADR-0128). */
	id: string
	code: string
	labelEn: string
	labelFr: string
	/** The one locale a reporter-added choice is worded in, or null when it has both (ADR-0095). */
	onlyIn: string | null
	/** `first`, `last`, or `none`; the API always sends it, and a stub leaving it out means `none` (ADR-0136). */
	pin?: string
}

export interface StubQuestion {
	id: string
	key: string
	role: string
	revisionId: string
	type: string
	isRequired: boolean
	isPrivate: boolean
	/** Whether a date answer may lie after today; the API sends it on every question (ADR-0138). */
	allowFutureDates?: boolean
	displayOrder: number
	dependsOnQuestionId: string | null
	dependsOnChoiceId: string | null
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
		role: "none",
		revisionId: `rev-${overrides.id}`,
		isRequired: false,
		isPrivate: false,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
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
						{ id: "choice-hang_glider", code: "hang_glider", labelEn: "Hang glider", labelFr: "Deltaplane", onlyIn: null },
						{ id: "choice-paraglider", code: "paraglider", labelEn: "Paraglider", labelFr: "Parapente", onlyIn: null },
					],
				}),
				question({ id: "aircraft_model", key: "aircraft_model", labelEn: "Model", type: "short_text", displayOrder: 1 }),
			],
		}),
		question({ id: "attachments", key: "attachments", labelEn: "Photos or videos", type: "file_upload", displayOrder: 5 }),
		question({
			id: "consent",
			key: "consent_publish",
			role: "consent_publish",
			labelEn: "May we publish a summary of this report?",
			type: "yes_no",
			displayOrder: 6,
			isRequired: true,
		}),
	]
}

/**
 * The default form with the media-consent system question after publication
 * consent, as the migrations seed it (ADR-0117, ADR-0119). The form asks it
 * only when publication consent is yes and a file is attached.
 */
export function mediaConsentFormQuestions(): StubQuestion[] {
	return [
		...defaultFormQuestions(),
		question({
			id: "media_consent",
			key: "consent_media",
			role: "consent_media",
			labelEn: "Photo, video, and document consent",
			type: "yes_no",
			displayOrder: 7,
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
				{ id: "choice-gusty", code: "gusty", labelEn: "Gusty", labelFr: "Rafales", onlyIn: null },
				{ id: "choice-thermic", code: "thermic", labelEn: "Thermic", labelFr: "Thermique", onlyIn: null },
				{ id: "choice-turbulent", code: "turbulent", labelEn: "Turbulent", labelFr: "Turbulent", onlyIn: null },
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
				{ id: "choice-coopers", code: "coopers", labelEn: "Cooper's Hill", labelFr: "Colline Cooper", onlyIn: null },
				{ id: "choice-mount_7", code: "mount_7", labelEn: "Mount 7", labelFr: "Mount 7", onlyIn: "en-CA" },
			],
		}),
	)
	return questions
}

/**
 * The default form with one choice question of `type` as its first
 * answer-producing page, offering `options` in the order given — the server's
 * order, which is not alphabetical (ADR-0136).
 */
export function choiceFormQuestions(type: string, options: StubOption[], extra: Partial<StubQuestion> = {}): StubQuestion[] {
	const questions = defaultFormQuestions()
	questions.splice(
		1,
		0,
		question({
			id: "choice_question",
			key: "choice_question",
			labelEn: "Which one applies?",
			type,
			displayOrder: 1,
			allowsReporterAdditions: type === "autocomplete",
			options,
			...extra,
		}),
	)
	return questions
}

/** The default form with a date and a time question as its first answer-producing pages (REQ-SUB-068). */
export function dateTimeFormQuestions(): StubQuestion[] {
	const questions = defaultFormQuestions()
	questions.splice(
		1,
		0,
		question({ id: "occurred_on", key: "occurred_on", labelEn: "On what date?", type: "date", displayOrder: 1 }),
		question({ id: "occurred_at", key: "occurred_at", labelEn: "At what time?", type: "time", displayOrder: 1 }),
	)
	return questions
}

/** Where REQ-QB-143 places its instructional text on the form. */
export type StatementPlacement = "the form's introduction" | "a page of its own" | "grouped under a group"

export const STATEMENT_TITLE = "Before you start"
export const STATEMENT_PARAGRAPHS = ["Take your time with each answer.", "You can come back to a saved report for 15 days."]

/**
 * The default form with a two-paragraph instructional text at `placement`:
 * as the leading statement, as the first page after it, or as the first
 * child of a group that is that page (REQ-QB-143).
 */
export function statementFormQuestions(placement: StatementPlacement): StubQuestion[] {
	const description = STATEMENT_PARAGRAPHS.join("\n\n")
	const statement = (displayOrder: number) =>
		question({
			id: "before_you_start",
			key: "before_you_start",
			labelEn: STATEMENT_TITLE,
			type: "statement",
			displayOrder,
			helpTextEn: description,
			helpTextFr: description,
		})
	const questions = defaultFormQuestions()

	if (placement === "the form's introduction") {
		questions[0] = { ...questions[0], labelEn: STATEMENT_TITLE, helpTextEn: description, helpTextFr: description }
	} else if (placement === "a page of its own") {
		questions.splice(1, 0, statement(1))
	} else {
		questions.splice(
			1,
			0,
			question({
				id: "pilot",
				key: "pilot",
				labelEn: "About the pilot:",
				type: "group",
				displayOrder: 1,
				children: [
					statement(0),
					question({ id: "pilot_rating", key: "pilot_rating", labelEn: "Pilot rating", type: "short_text", displayOrder: 1 }),
				],
			}),
		)
	}
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

/** A saved, unexpired report holding a date and a time in their stored ISO 8601 form (REQ-SUB-068). */
export async function writeSavedDateTimeDraftToBrowser(page: Page, date: string, time: string) {
	await page.addInitScript(
		([savedDate, savedTime]) => {
			localStorage.setItem(
				"hpac.report.draft",
				JSON.stringify({
					locale: "en-CA",
					answers: {
						"rev-occurred_on": { kind: "value", value: savedDate },
						"rev-occurred_at": { kind: "value", value: savedTime },
					},
					attachments: {},
					stepKey: "occurred_at",
					startedAtMs: Date.now() - 60 * 60 * 1000,
					savedAtMs: Date.now() - 60 * 60 * 1000,
				}),
			)
		},
		[date, time] as const,
	)
}
