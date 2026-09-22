import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { defaultFormQuestions, stubCurrentQuestions } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * The one @ui scenario in question-bank-and-form.feature: the group-page
 * rendering contract also exercised, in more depth, by report-form.steps.ts.
 * Kept in its own file to match this repository's one-steps-file-per-feature
 * convention (ADR-0053).
 */

async function reachGroupPage(page: Page) {
	await stubAuth(page)
	await stubCurrentQuestions(page, defaultFormQuestions())
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()

	await page.getByRole("button", { name: "Next" }).click() // intro -> narrative
	await page.getByRole("button", { name: "Next" }).click() // narrative -> injured
	await page.getByRole("group", { name: "Was anyone injured?" }).getByRole("radio", { name: "No" }).click()
	await page.getByRole("button", { name: "Next" }).click() // injured -> aircraft group
}

Given("a group question exists as a section heading", async () => {}) // The synthetic fixture's "Aircraft:" question already is one.

Given("another question is grouped under it", async () => {}) // Its two children are already grouped under it.

When("a reporter is shown the form", async ({ page }) => {
	await reachGroupPage(page)
})

Then("that question renders together with the group heading and its other children", async ({ page }) => {
	await expect(page.getByRole("group", { name: "Aircraft:" })).toBeVisible()
	await expect(page.getByLabel("Type of aircraft")).toBeVisible()
	await expect(page.getByLabel("Model")).toBeVisible()
})
