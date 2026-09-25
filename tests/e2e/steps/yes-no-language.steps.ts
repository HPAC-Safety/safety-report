import { createBdd } from "playwright-bdd"
import { expect, type Page, type Request } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions, stubSubmission, type StubQuestion } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * REQ-SUB-077: a yes or no is sent as a JSON boolean whatever language the
 * report is submitted in (ADR-0130). The form holds a language-free answer
 * while the reporter works, so switching language before submitting changes
 * nothing that is sent. The API is stubbed at the network boundary; the
 * server's own handling of a boolean is covered by REQ-QB-019 and REQ-QB-118.
 */

type Language = "English" | "French"

const WORDS: Record<Language, { yes: string; no: string; next: string; submit: string; toggle: RegExp }> = {
	// The toggle names its target in the current language: "Anglais" from French.
	English: { yes: "Yes", no: "No", next: "Next", submit: "Submit report", toggle: /Anglais|English/ },
	French: { yes: "Oui", no: "Non", next: "Suivant", submit: "Soumettre un signalement", toggle: /Français/ },
}

function yesNoForm(): StubQuestion[] {
	const base = {
		role: "none",
		isRequired: false,
		isPrivate: false,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: false,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		children: [],
		type: "yes_no",
	}

	return [
		{
			...base,
			id: "injured",
			revisionId: "rev-injured",
			key: "was_injured",
			labelEn: "Was anyone injured?",
			labelFr: "Y a-t-il eu des blessés?",
			displayOrder: 0,
		},
		{
			...base,
			id: "consent",
			revisionId: "rev-consent",
			key: "consent_publish",
			role: "consent_publish",
			labelEn: "May we publish a summary of this report?",
			labelFr: "Pouvons-nous publier un résumé de ce signalement?",
			displayOrder: 1,
			isRequired: true,
		},
	] as StubQuestion[]
}

const sent = new WeakMap<Page, Request>()
const languageOf = new WeakMap<Page, Language>()

async function switchTo(page: Page, language: Language) {
	const lang = language === "French" ? "fr-CA" : "en-CA"

	// The one toggle names the language it switches to, and is pressed only
	// when the form is not already in that language.
	if ((await page.locator("html").getAttribute("lang")) !== lang) {
		await page.getByRole("button", { name: WORDS[language].toggle }).click()
	}

	await expect(page.locator("html")).toHaveAttribute("lang", lang)
	languageOf.set(page, language)
}

Given(
	/^a signed-in reporter answers a yes\/no question and publication consent in (English|French)$/,
	async ({ page }, language: Language) => {
		await stubAuth(page)
		await stubCurrentQuestions(page, yesNoForm())
		await stubSubmission(page)
		await signInAs(page, "user")
		await page.goto("/report")
		await expect(page.getByRole("radio", { name: "Yes" })).toBeVisible()
		await switchTo(page, language)

		const words = WORDS[language]
		await page.getByRole("radio", { name: words.no }).click()
		await page.getByRole("button", { name: words.next }).click()
		await page.getByRole("radio", { name: words.yes }).click()
	},
)

Given(/^the reporter switches the form to (English|French) before submitting$/, async ({ page }, language: Language) => {
	await switchTo(page, language)
})

When("the reporter submits the report", async ({ page }) => {
	const words = WORDS[languageOf.get(page) ?? "English"]
	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: words.submit }).click()
	sent.set(page, await request)
})

Then(
	/^the yes\/no answer is sent as the JSON boolean false and the consent answer as the JSON boolean true$/,
	async ({ page }) => {
		const body = sent.get(page)!.postDataJSON() as { answers: { questionRevisionId: string; value: unknown }[] }
		const valueOf = (revisionId: string) => body.answers.find((answer) => answer.questionRevisionId === revisionId)?.value

		expect(valueOf("rev-injured")).toBe(false)
		expect(valueOf("rev-consent")).toBe(true)
	},
)
