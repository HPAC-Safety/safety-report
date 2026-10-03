import { readFileSync } from "node:fs"
import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions, stubSubmission, type StubQuestion } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * REQ-QB-266: each seeded group is one page with its heading and exactly its
 * questions (ADR-0187). The API is stubbed with fixtures/seeded-questions.json,
 * which is exactly what a freshly migrated database sends: REQ-QB-265 fails the
 * moment the two differ, so this walks the form the migrations really seed rather
 * than a hand-written one (lesson 0041).
 */

const seededQuestions = JSON.parse(
	readFileSync(new URL("../fixtures/seeded-questions.json", import.meta.url), "utf8"),
) as StubQuestion[]

const MAX_PAGES = seededQuestions.length + 1

function quoted(list: string): string[] {
	return [...list.matchAll(/"([^"]*)"/g)].map((match) => match[1])
}

function groupPage(page: Page, heading: string) {
	return page.getByRole("main").getByRole("group", { name: heading, exact: true })
}

Given("the form asks the questions the migrations seed", async ({ page }) => {
	await stubAuth(page)
	await stubCurrentQuestions(page, seededQuestions)
	await stubSubmission(page)
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

When(/^a reporter using (English|French) moves through the form to the "(.+)" group$/, async ({ page }, language: string, heading: string) => {
	const counter = page.getByRole("main").getByRole("status").filter({ hasText: /\d+/ }).first()
	if (language === "French") {
		await page.getByRole("button", { name: "Français" }).click()
		// The counter re-renders in French; settle it before reading it below.
		await expect(counter).toHaveText(/Étape/)
	}
	const next = page.getByRole("button", { name: language === "French" ? "Suivant" : "Next", exact: true })

	// Every seeded question before the groups is optional, so Next moves on from
	// each page unanswered.
	for (let pages = 0; pages < MAX_PAGES; pages++) {
		if (await groupPage(page, heading).isVisible()) return
		// Wait for the next page before looking again, or a second click lands on
		// the page just reached and skips it.
		const before = await counter.textContent()
		await next.click()
		await expect(counter).not.toHaveText(before ?? "")
	}
	throw new Error(`No page headed "${heading}" in ${MAX_PAGES} pages.`)
})

Then(/^the page is headed "(.+)" and asks exactly (.+)$/, async ({ page }, heading: string, list: string) => {
	const group = groupPage(page, heading)
	await expect(group).toBeVisible()

	// Each question's prompt: a label for a text, phone, or email field, and the
	// labelling span of a multi-select. The locale's colon is stripped (ADR-0181).
	const prompts = group.locator('label[for^="question-"], span[id^="question-"][id$="-label"]')
	await expect
		.poll(() => prompts.evaluateAll((elements) => elements.map((element) => element.textContent.trim().replace(/\s*:$/, ""))))
		.toEqual(quoted(list))
})
