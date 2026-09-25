import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { pendingCountsStubbed } from "./auth"

const { Given, Then } = createBdd()

/*
 * The Admin menu's pending counts (REQ-MOD-087..089). The API's own rule —
 * what needs action, and who gets the translation count — is covered by
 * HpacSafety.Api.Tests; here the counts are stubbed and only what the header
 * draws is asserted. The stub answers as the API would for the signed-in role.
 */

async function stubCounts(page: Page, reports: string, values: string, answers: string) {
	pendingCountsStubbed.add(page)
	await page.route("**/api/admin/counts", (route) => {
		const administrator = (route.request().headers()["authorization"] ?? "").includes("administrator")

		return route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({
				reportsNeedingAction: Number(reports),
				answersAwaitingTranslation: administrator && answers !== "no" ? Number(answers) : null,
				typeAheadValuesAwaitingReview: Number(values),
			}),
		})
	})
}

Given(
	/^the API counts (\d+) reports needing action and (\d+|no) answers awaiting translation$/,
	async ({ page }, reports: string, answers: string) => stubCounts(page, reports, "0", answers),
)

Given(
	/^the API counts (\d+) reports needing action, (\d+) type-ahead values awaiting review, and (\d+|no) answers awaiting translation$/,
	async ({ page }, reports: string, values: string, answers: string) => stubCounts(page, reports, values, answers),
)

const adminButton = (page: Page) => page.locator("header").getByRole("button", { name: /^Admin/ })
const menu = (page: Page) => page.getByRole("menu", { name: "Admin" })

const OPTIONS: Record<string, string> = {
	"manage-reports": "Manage reports",
	"manage-questions": "Manage questions",
	"manage-answer-translations": "Answers awaiting translation",
	"review-type-ahead-values": "Type-ahead values to review",
}

const option = (page: Page, name: string) => menu(page).getByRole("menuitem", { name: OPTIONS[name] })

Then(/^the Admin menu shows a count of (\d+)$/, async ({ page }, count: string) => {
	await expect(adminButton(page)).toHaveAccessibleName(`Admin ${count} waiting`)
	await expect(adminButton(page).locator("[data-count-badge]")).toHaveText(new RegExp(`^${count}`))
})

Then("the Admin menu shows no count", async ({ page }) => {
	await expect(adminButton(page)).toHaveAccessibleName("Admin")
	await expect(adminButton(page).locator("[data-count-badge]")).toHaveCount(0)
})

Then(/^the ((?:manage|review)-[a-z-]+) option shows a count of (\d+)$/, async ({ page }, name: string, count: string) => {
	await expect(option(page, name)).toHaveAccessibleName(`${OPTIONS[name]} ${count} waiting`)
})

Then(/^the ((?:manage|review)-[a-z-]+) option shows no count$/, async ({ page }, name: string) => {
	await expect(option(page, name)).toHaveAccessibleName(OPTIONS[name])
})

Then("no option shows a count", async ({ page }) => {
	await expect(menu(page).getByRole("menuitem").first()).toBeVisible()
	await expect(menu(page).locator("[data-count-badge]")).toHaveCount(0)
})
