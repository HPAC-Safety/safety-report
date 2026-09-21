import { createBdd } from "playwright-bdd"

import { signInAs } from "./auth"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for /admin/answer-translations (ADR-0053).
 *
 * A reporter answers a picker or a type-ahead in one official language and
 * nothing translates it on its way in (ADR-0072). This is the screen where an
 * administrator supplies the other one.
 *
 * The admin API is stubbed at the network boundary; the server side is covered
 * by HpacSafety.Api.Tests against a real PostgreSQL container (ADR-0045).
 * Every value here is synthetic.
 */

async function stubQueue(page: Page) {
	const answers = [
		{
			id: "aaaaaaaaaaa",
			questionKey: "occurrence_site",
			value: "Mont Sept",
			locale: "fr-CA",
			into: "en-CA",
		},
	]

	await page.route("**/api/admin/answers/awaiting-translation", async (route) => {
		await route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({ answers, waiting: answers.length }),
		})
	})

	await page.route("**/api/admin/translate", async (route) => {
		if (route.request().method() === "GET") {
			await route.fulfill({
				status: 200,
				contentType: "application/json",
				body: JSON.stringify({ available: true, standIn: false }),
			})
			return
		}

		await route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({ texts: ["Mount Seven"] }),
		})
	})

	await page.route("**/api/admin/answers/*/translation", async (route) => {
		// Saving clears the flag, so the answer leaves the queue.
		answers.length = 0
		await route.fulfill({ status: 204, body: "" })
	})
}

Given("a signed-in Administrator opens the answers-awaiting-translation page", async ({ page }) => {
	await signInAs(page, "administrator")
	await stubQueue(page)
	await page.goto("/admin/answer-translations")
	await expect(page.getByRole("list", { name: "Answers awaiting translation" })).toBeVisible()
})

Then("each answer is listed with its question, its value, and the language it was given in", async ({ page }) => {
	const list = page.getByRole("list", { name: "Answers awaiting translation" })

	await expect(list.getByText("occurrence_site")).toBeVisible()
	await expect(list.getByText("Mont Sept")).toBeVisible()
	await expect(list.getByText("Given in fr-CA")).toBeVisible()
})

Then("the page says how many are waiting", async ({ page }) => {
	await expect(page.getByText("Waiting for a second language: 1", { exact: false })).toBeVisible()
})

When("they press Translate on the first answer and save", async ({ page }) => {
	await page.getByRole("button", { name: "Translate" }).first().click()
	await expect(page.getByLabel("Supply en-CA")).toHaveValue("Mount Seven")
	await page.getByRole("button", { name: "Save" }).first().click()
})

Then("that answer leaves the queue", async ({ page }) => {
	await expect(page.getByText("No answers are waiting for a second language.")).toBeVisible()
})

Then("the value the reporter gave is unchanged", async ({ page }) => {
	// Nothing on this screen can edit it: the reporter's words are the record
	// of what they answered, and only the second language is authored here.
	await expect(page.getByRole("textbox", { name: "Mont Sept" })).toHaveCount(0)
})
