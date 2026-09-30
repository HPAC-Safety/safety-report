import { expect, type Page } from "@playwright/test"

import { signInAs } from "./auth"

/*
 * One synthetic report, as the admin API sends it, for the scenarios that read a
 * report's summary and answers (ADR-0180, ADR-0181). The API is stubbed at the
 * network boundary; nothing here is real report content.
 */

export const REPORT_ID = "mdreportaaa"

export const SUMMARY_EN = "## Description\nThe pilot launched in a gusting wind.\n\n## Action and prevention\nThe pilot told the club."
export const SUMMARY_FR =
	"## Description\nLe pilote a décollé par vent en rafales.\n\n## Action et prévention\nLe pilote a informé le club."
export const OLDER_EN = "## Description\nAn earlier version of the description.\n\n## Action and prevention\nNot provided."
export const OLDER_FR = "## Description\nUne version antérieure de la description.\n\n## Action et prévention\nNon fourni."

export interface StubAnswer {
	questionKey: string
	labelEn: string
	labelFr: string
	type: string
	isPrivate: boolean
	values: { value: string | boolean; locale: string; translatedValue: string | null; translationSource: string | null }[]
}

function revision(sequence: number, en: string, fr: string, isCurrent: boolean) {
	return {
		id: `mdrevision${sequence}`,
		sequence,
		aiSummaryEn: en,
		aiSummaryFr: fr,
		sourceEn: "generated",
		sourceFr: "generated",
		authorSubject: null,
		createdAt: "2026-09-20T15:35:00Z",
		restoredFromSequence: null,
		approvedBySubject: null,
		approvedAt: null,
		isCurrent,
	}
}

export function reportDetail(answers: StubAnswer[] = []) {
	return {
		id: REPORT_ID,
		submittedAt: "2026-09-20T15:30:00Z",
		status: "pending",
		language: "en-CA",
		consent: true,
		isStuck: false,
		version: "1.1",
		reporterName: null,
		pilotName: null,
		attachmentCount: 0,
		unpublishNote: null,
		publishedAt: null,
		summaryError: null,
		answers,
		summary: {
			aiSummaryEn: SUMMARY_EN,
			aiSummaryFr: SUMMARY_FR,
			model: "gemini-3.7-flash",
			promptVersion: "summarize-anonymize.v4",
			generatedAt: "2026-09-20T15:35:00Z",
			updatedAt: "2026-09-20T15:35:00Z",
			approvedBySubject: null,
			approvedAt: null,
			sourceEn: "generated",
			sourceFr: "generated",
		},
		attachments: [],
		mediaConsent: null,
		summaryRevisions: [revision(2, SUMMARY_EN, SUMMARY_FR, true), revision(1, OLDER_EN, OLDER_FR, false)],
	}
}

/** A safety officer is signed in, and the admin API answers for this one report. */
export async function stubReportDetail(page: Page, report: unknown) {
	await signInAs(page, "safety_officer")
	await page.route(new RegExp(`/api/admin/reports/${REPORT_ID}$`), (route) => route.fulfill({ json: report }))
}

export async function openReport(page: Page) {
	await page.goto(`/admin/reports/${REPORT_ID}`)
	await expect(page.getByRole("heading", { level: 1, name: /^(Report|Signalement)$/ })).toBeVisible()
}
