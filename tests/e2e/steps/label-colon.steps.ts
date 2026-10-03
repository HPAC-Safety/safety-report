import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

import { openReport, reportDetail, stubReportDetail, type StubAnswer } from "./admin-report-fixture"
import { signInAs, stubAuth } from "./auth"
import { question, stubCurrentQuestions, type StubQuestion } from "./report-form-fixture"
import { present } from "./present"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the closing colon after a question's label (ADR-0181,
 * REQ-QB-240 to REQ-QB-243): a label is stored without one, and the interface
 * draws it in the locale's style, `Label:` in English and `Label :` in French. The
 * API is stubbed at the network boundary; every question is synthetic.
 */

/** A regular expression matching `text` with any kind of space standing for a single one. */
function exactly(text: string): RegExp {
	return new RegExp(`^${text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&").replace(/ /g, "\\s")}$`)
}

// --- REQ-QB-240: the reporter form ---

const TYPES: Record<string, string> = {
	"short text": "short_text",
	"single-select": "single_select",
	"multi-select": "multi_select",
	"yes/no": "yes_no",
	statement: "statement",
	group: "group",
}

let asked: { type: string; label: string } | null = null

Given("the form asks a {} question labelled {string}", async ({ page }, kind: string, label: string) => {
	const type = TYPES[kind]
	expect(type, `no type for ${kind}`).toBeDefined()
	asked = { type, label }

	const options = [{ id: "choice-a", code: "a", labelEn: "First", labelFr: "Premier", onlyIn: null }]
	const tested: StubQuestion = question({
		id: "tested",
		key: "tested",
		labelEn: label,
		labelFr: label,
		type,
		displayOrder: 1,
		options: type === "single_select" || type === "multi_select" ? options : [],
		children: type === "group" ? [question({ id: "child", key: "child", labelEn: "Child", type: "short_text", displayOrder: 0 })] : [],
	})
	const consent = question({
		id: "consent",
		key: "consent_publish",
		role: "consent_publish",
		labelEn: "May we publish a summary of this report?",
		type: "yes_no",
		displayOrder: 2,
		isRequired: true,
	})

	await stubAuth(page)
	await stubCurrentQuestions(page, [tested, consent])
	await signInAs(page, "user")
})

When("a reporter opens the form in {word}", async ({ page }, language: string) => {
	await page.addInitScript((code) => localStorage.setItem("hpac.locale", code), language === "French" ? "fr-CA" : "en-CA")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 }).or(page.getByRole("group")).or(page.locator("label, span[id$='-label']")).first()).toBeVisible()
})

Then("the question is labelled {string}", async ({ page }, shown: string) => {
	const { type } = present(asked)
	const where = type === "statement" ? page.getByRole("heading", { level: 1 }) : type === "group" ? page.locator("fieldset > legend") : page.locator("label, legend, span[id$='-label']")

	await expect(where.filter({ hasText: exactly(shown) })).toHaveCount(1)
})

// --- REQ-QB-241: the admin report detail ---

function answer(key: string, label: string, type: string): StubAnswer {
	return {
		questionKey: key,
		labelEn: label,
		labelFr: label,
		type,
		isPrivate: false,
		values: [{ value: type === "yes_no" ? true : "synthetic", locale: "en-CA", translatedValue: null, translationSource: null }],
	}
}

Given(
	'a signed-in Safety Officer and a report with a short-text answer labelled "Date", a yes\\/no answer labelled "Injured?" and a long-text answer labelled "Description"',
	async ({ page }) => {
		await stubReportDetail(
			page,
			reportDetail([
				answer("occurred_on", "Date", "short_text"),
				answer("injured", "Injured?", "yes_no"),
				answer("narrative", "Description", "long_text"),
			]),
		)
	},
)

When("they open that report in {word}", async ({ page }, language: string) => {
	await page.addInitScript((code) => localStorage.setItem("hpac.locale", code), language === "French" ? "fr-CA" : "en-CA")
	await openReport(page)
})

Then("the answers are labelled {}", async ({ page }, labels: string) => {
	const expected = [...labels.matchAll(/"([^"]*)"/g)].map((match) => match[1])
	const shown = (await page.locator("[data-question-key] dt").allInnerTexts()).map((text) =>
		text.replace(/\s+/g, " ").replace(/\s?Private$/i, "").trim(),
	)

	expect(shown).toEqual(expected)
})

// --- REQ-QB-242: the question bank list ---

let listed: unknown[] = []

function adminQuestion(id: string, type: string, labelEn: string, labelFr: string, displayOrder: number) {
	return {
		id,
		key: id,
		revisionId: `rev${id}`,
		revisionNumber: 1,
		type,
		isSystem: false,
		isRequired: false,
		isPrivate: false,
		isTranslatable: false,
		allowFutureDates: false,
		isActive: true,
		displayOrder,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		labelEn,
		labelFr,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		reporterChoicesAwaitingReview: 0,
		hasBeenAnswered: false,
	}
}

Given(
	"a signed-in Administrator and a question labelled {string} in English and {string} in French",
	async ({ page }, english: string, french: string) => {
		listed = [adminQuestion("datequest01", "short_text", english, french, 0)]
		await signInAs(page, "administrator")
	},
)

Given("a statement labelled {string} in both languages", async ({ page }, label: string) => {
	listed.push(adminQuestion("statement001", "statement", label, label, 1))
	await page.route("**/api/admin/questions", (route) => route.fulfill({ json: listed }))
	await page.route("**/api/admin/translate", (route) => route.fulfill({ json: { available: true } }))
})

When("they open the manage-questions page", async ({ page }) => {
	await page.goto("/admin/questions")
	await expect(page.getByRole("list", { name: "Questions on the form" })).toBeVisible()
})

Then('the question\'s English label reads "Date:" and its French label reads "Date :"', async ({ page }) => {
	const row = page.getByRole("listitem").filter({ has: page.locator('[data-label="en"]:text-matches("^Date")') })
	await expect(row.locator('[data-label="en"]')).toHaveText(exactly("Date:"))
	await expect(row.locator('[data-label="fr"]')).toHaveText(exactly("Date :"))
})

Then("the statement's labels have no colon", async ({ page }) => {
	const row = page.getByRole("listitem").filter({ hasText: "Tell us more" })
	await expect(row.locator('[data-label="en"]')).toHaveText(exactly("Tell us more"))
	await expect(row.locator('[data-label="fr"]')).toHaveText(exactly("Tell us more"))
})

// --- REQ-QB-243: the editor refuses a colon ---

When("they write {string} as the English wording and {string} as the French wording", async ({ page }, english: string, french: string) => {
	await page.getByLabel("Question (English)").fill(english)
	await page.getByLabel("Question (French)").fill(french)
})

Then("a message says the form adds the colon itself", async ({ page }) => {
	await expect(page.getByRole("alert").filter({ hasText: "The form adds the colon itself" })).toBeVisible()
})

Then("Save stays disabled", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Save", exact: true })).toBeDisabled()
})

When("they remove the colon", async ({ page }) => {
	await page.getByLabel("Question (English)").fill("Date")
})

Then("the message goes away and Save is enabled", async ({ page }) => {
	await expect(page.getByRole("alert").filter({ hasText: "The form adds the colon itself" })).toHaveCount(0)
	await expect(page.getByRole("button", { name: "Save", exact: true })).toBeEnabled()
})
