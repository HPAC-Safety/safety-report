import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { defaultFormQuestions, pickChoice, question, stubCurrentQuestions, stubSubmission, type StubOption, type StubQuestion } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * REQ-QB-256 to REQ-QB-258: the seeded Country question as a pinned pick list,
 * and Province shown only for Canada (ADR-0186). The API is stubbed at the
 * network boundary. What the migration seeds, and what the API sends for it, is
 * REQ-QB-249 to REQ-QB-255. A handful of countries stand in for the 249; the
 * stub sends them in the server's order (pinned first, then by ID), which is not
 * alphabetical, so the order on screen is the browser's own.
 */

function country(code: string, labelEn: string, labelFr: string, pin = "none"): StubOption {
	return { id: `choice-${code}`, code, labelEn, labelFr, onlyIn: null, pin }
}

function countryForm(): StubQuestion[] {
	const questions = defaultFormQuestions()
	questions.splice(
		1,
		0,
		question({
			id: "country",
			key: "2a401575_8cf4_4c87_b8df_95798d07a772",
			labelEn: "Country",
			labelFr: "Pays",
			helpTextEn: "Country where the occurrence happened.",
			helpTextFr: "Pays où l'évènement a eu lieu.",
			type: "single_select",
			displayOrder: 1,
			options: [
				country("ca", "Canada", "Canada", "first"),
				country("us", "United States", "États-Unis", "first"),
				country("za", "Zambia", "Zambie"),
				country("mx", "Mexico", "Mexique"),
				country("au", "Australia", "Australie"),
				country("br", "Brazil", "Brésil"),
			],
		}),
		question({
			id: "province",
			key: "849ea0c4_b36e_44a7_936e_e578967907a3",
			labelEn: "Province",
			labelFr: "Province",
			type: "single_select",
			displayOrder: 2,
			dependsOnQuestionId: "country",
			dependsOnChoiceId: "choice-ca",
			options: [
				{ id: "choice-on", code: "ontario", labelEn: "Ontario", labelFr: "Ontario", onlyIn: null },
				{ id: "choice-qc", code: "quebec", labelEn: "Quebec", labelFr: "Québec", onlyIn: null },
			],
		}),
	)
	return questions
}

async function goNext(page: Page) {
	await page.getByRole("button", { name: "Next" }).click()
}

Given("the form asks the Country question as the migrations seed it", async ({ page }) => {
	await stubAuth(page)
	await stubCurrentQuestions(page, countryForm())
	await stubSubmission(page)
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

When(/^a reporter using (English|French) opens the Country question$/, async ({ page }, language: string) => {
	await goNext(page) // intro -> the Country page
	if (language === "French") await page.getByRole("button", { name: "Français" }).click()
	await expect(page.getByRole("main").getByRole("combobox")).toBeVisible()
})

Then(/^its open list reads (.+)$/, async ({ page }, quoted: string) => {
	const expected = [...quoted.matchAll(/"([^"]*)"|a separator/g)].map((match) => match[1] ?? "|")
	const combobox = page.getByRole("main").getByRole("combobox")
	if ((await combobox.getAttribute("aria-expanded")) !== "true") await combobox.click()
	const listed = await page
		.getByRole("main")
		.getByRole("listbox")
		.locator('[role="option"]:not([data-placeholder]), [data-separator]')
		.evaluateAll((entries) => entries.map((entry) => (entry.hasAttribute("data-separator") ? "|" : (entry.textContent ?? "").trim())))
	expect(listed.slice(0, expected.length)).toEqual(expected)
})

When("presses Next without choosing a country", async ({ page }) => {
	await goNext(page)
})

Then("the form moves on without asking Province", async ({ page }) => {
	await expect(page.getByLabel("What happened?")).toBeVisible()
	await expect(page.getByRole("combobox", { name: "Province" })).toHaveCount(0)
})

Then("no message says an answer is required", async ({ page }) => {
	await expect(page.getByText("This question is required.")).toHaveCount(0)
})

When("a reporter using English chooses {string} for Country and presses Next", async ({ page }, choice: string) => {
	await goNext(page) // intro -> the Country page
	await pickChoice(page, "Country", choice)
	await goNext(page)
})

Then("the Province question is shown", async ({ page }) => {
	await expect(page.getByRole("combobox", { name: "Province" })).toBeVisible()
})

When("the reporter goes back and chooses {string} for Country and presses Next", async ({ page }, choice: string) => {
	await page.getByRole("button", { name: "Back" }).click()
	await pickChoice(page, "Country", choice)
	await goNext(page)
})
