import { createBdd, type DataTable } from "playwright-bdd"
import { expect, type Page, type Request } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions, stubSubmission, type StubQuestion } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * REQ-SUB-085..095: an email or phone question (issue #513, ADR-0137) — the
 * keyboard each opens, blank allowed, a malformed answer held on its page, the
 * phone field's country picker and per-country mask, the E.164 it sends, and
 * the email field's domain suggestions. The API is stubbed at the network
 * boundary; its own refusal of a malformed answer is REQ-SUB-097, through the
 * booted host. Every question, address, and number is synthetic.
 */

const CONTACT = "#question-rev-contact"

function contactForm(type: string): StubQuestion[] {
	const base = {
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
	}

	return [
		{
			...base,
			id: "contact",
			revisionId: "rev-contact",
			key: "contact",
			role: "none",
			type,
			labelEn: "How can we reach you?",
			labelFr: "Comment pouvons-nous vous joindre?",
			displayOrder: 0,
		},
		{
			...base,
			id: "consent",
			revisionId: "rev-consent",
			key: "consent_publish",
			role: "consent_publish",
			type: "yes_no",
			labelEn: "May we publish a summary of this report?",
			labelFr: "Pouvons-nous publier un résumé de ce signalement?",
			displayOrder: 1,
			isRequired: true,
		},
	]
}

const sent = new WeakMap<Page, Request>()

function contactField(page: Page) {
	return page.locator(CONTACT)
}

function consentPage(page: Page) {
	return page.getByRole("group", { name: "May we publish a summary of this report?" })
}

function suggestionList(page: Page) {
	return page.getByRole("listbox", { name: "Suggested email addresses" })
}

async function submitFromConsent(page: Page) {
	await consentPage(page).getByRole("radio", { name: "Yes" }).click()
	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: "Submit report" }).click()
	sent.set(page, await request)
}

function sentValue(page: Page): unknown {
	const body = sent.get(page)!.postDataJSON() as { answers: { questionRevisionId: string; value: unknown }[] }
	return body.answers.find((answer) => answer.questionRevisionId === "rev-contact")?.value
}

Given(/^the current page shows an optional (email|phone) question$/, async ({ page }, type: string) => {
	await stubAuth(page)
	await stubCurrentQuestions(page, contactForm(type))
	await stubSubmission(page)
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(contactField(page)).toBeVisible()
})

Then(
	"its field has type {string}, input mode {string}, and autocomplete {string}",
	async ({ page }, type: string, inputMode: string, autocomplete: string) => {
		await expect(contactField(page)).toHaveAttribute("type", type)
		await expect(contactField(page)).toHaveAttribute("inputmode", inputMode)
		await expect(contactField(page)).toHaveAttribute("autocomplete", autocomplete)
	},
)

When("the reporter leaves it blank and presses Next", async ({ page }) => {
	await expect(contactField(page)).toHaveValue("")
	await page.getByRole("button", { name: "Next" }).click()
})

When("the reporter types {string} into it and presses Next", async ({ page }, typed: string) => {
	await contactField(page).pressSequentially(typed)
	await page.getByRole("button", { name: "Next" }).click()
})

When("the reporter types {string} into it", async ({ page }, typed: string) => {
	await contactField(page).pressSequentially(typed)
})

Then("the next page shows", async ({ page }) => {
	await expect(consentPage(page)).toBeVisible()
	await expect(contactField(page)).toHaveCount(0)
})

Then(/^the (?:email|phone) answer is sent as null when the report is submitted$/, async ({ page }) => {
	await submitFromConsent(page)
	expect(sentValue(page)).toBeNull()
})

Then("the reporter stays on that page", async ({ page }) => {
	await expect(contactField(page)).toBeVisible()
	await expect(consentPage(page)).toHaveCount(0)
})

Then("an inline, localized message asks for an email address like name@example.com", async ({ page }) => {
	await expect(page.getByRole("alert").filter({ hasText: "Enter an email address like name@example.com." })).toBeVisible()
	await expect(contactField(page)).toHaveAttribute("aria-describedby", /rev-contact-error/)
})

Then("an inline, localized message asks for a phone number valid for the chosen country", async ({ page }) => {
	await expect(
		page.getByRole("alert").filter({ hasText: "Enter a phone number that is valid for the country you chose." }),
	).toBeVisible()
	await expect(contactField(page)).toHaveAttribute("aria-describedby", /rev-contact-error/)
})

Then("its country picker shows {string}", async ({ page }, shown: string) => {
	await expect(page.locator(`${CONTACT}-country-shown`)).toHaveText(shown)
})

Then("the phone field's placeholder is {string}", async ({ page }, placeholder: string) => {
	await expect(contactField(page)).toHaveAttribute("placeholder", placeholder)
})

When(/^the reporter chooses (.+) in its country picker$/, async ({ page }, country: string) => {
	const picker = page.getByRole("combobox", { name: "Country" })
	// Each country is listed by flag, name, and calling code.
	const option = picker.locator("option").filter({ hasText: new RegExp(` ${country} \\(\\+\\d+\\)$`) })
	await picker.selectOption((await option.getAttribute("value"))!)
})

Then("the phone field reads {string}", async ({ page }, masked: string) => {
	await expect(contactField(page)).toHaveValue(masked)
})

When("the reporter goes on to submit the report", async ({ page }) => {
	await page.getByRole("button", { name: "Next" }).click()
	await submitFromConsent(page)
})

Then("the phone answer is sent as {string}", async ({ page }, e164: string) => {
	expect(sentValue(page)).toBe(e164)
})

Then("the field is a combobox whose suggestion list is labelled {string}", async ({ page }, label: string) => {
	const combobox = page.getByRole("combobox", { name: "How can we reach you?" })
	await expect(combobox).toHaveAttribute("aria-expanded", "true")
	const list = page.getByRole("listbox", { name: label })
	await expect(list).toBeVisible()
	await expect(combobox).toHaveAttribute("aria-controls", (await list.getAttribute("id"))!)
})

Then("the suggestions below it are, in order:", async ({ page }, table: DataTable) => {
	await expect(suggestionList(page).getByRole("option")).toHaveText(table.raw().map(([address]) => address))
})

When(/^the reporter chooses "(.+)" (with the keyboard|with the pointer)$/, async ({ page }, address: string, how: string) => {
	const options = suggestionList(page).getByRole("option")
	if (how === "with the pointer") {
		await options.filter({ hasText: address }).click()
		return
	}
	const index = (await options.allTextContents()).indexOf(address)
	expect(index).toBeGreaterThanOrEqual(0)
	for (let step = 0; step <= index; step++) await contactField(page).press("ArrowDown")
	await expect(options.nth(index)).toHaveAttribute("aria-selected", "true")
	await expect(contactField(page)).toHaveAttribute("aria-activedescendant", (await options.nth(index).getAttribute("id"))!)
	await contactField(page).press("Enter")
})

Then("the email field reads {string}", async ({ page }, address: string) => {
	await expect(contactField(page)).toHaveValue(address)
})

Then("no suggestions are shown", async ({ page }) => {
	await expect(suggestionList(page)).toHaveCount(0)
	await expect(contactField(page)).toHaveAttribute("aria-expanded", "false")
})
