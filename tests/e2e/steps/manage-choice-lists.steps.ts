import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for /admin/choice-lists (ADR-0053).
 *
 * The admin API is stubbed at the network boundary; the server side of the
 * same behaviour is covered by HpacSafety.Api.Tests against a real PostgreSQL
 * container (ADR-0045). Every site name here is synthetic.
 */

interface StubItem {
	code: string
	labelEn: string
	labelFr: string
	sourceItemId: string | null
	addedByReporter: boolean
}

function item(code: string, labelEn: string, addedByReporter = false): StubItem {
	return { code, labelEn, labelFr: `${labelEn} (fr)`, sourceItemId: `id-${code}`, addedByReporter }
}

async function stubChoiceLists(page: Page) {
	const sets = [
		{
			id: "aaaaaaaaaaa",
			key: "sites",
			nameEn: "Flying sites",
			nameFr: "Sites de vol",
			items: [item("coopers", "Cooper's"), item("mount_7", "mount 7", true)],
		},
	]

	await page.route("**/api/admin/option-sets", async (route) => {
		await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(sets) })
	})

	await page.route("**/api/admin/option-sets/*", async (route) => {
		const saved = JSON.parse(route.request().postData() ?? "{}") as {
			nameEn: string
			nameFr: string
			items: { code: string; labelEn: string; labelFr: string }[]
		}

		// A relabel keeps the reporter-added marker: the flag records where the
		// choice came from, not whether anyone has touched it since.
		sets[0] = {
			...sets[0],
			nameEn: saved.nameEn,
			nameFr: saved.nameFr,
			items: saved.items.map((saving) => ({
				...saving,
				sourceItemId: `id-${saving.code}`,
				addedByReporter: sets[0].items.find((existing) => existing.code === saving.code)?.addedByReporter ?? false,
			})),
		}

		await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(sets[0]) })
	})
}

Given("a signed-in Administrator opens the manage-choice-lists page", async ({ page }) => {
	await page.goto("/login")
	await page.getByRole("button", { name: "Log in" }).click()
	await stubChoiceLists(page)
	await page.goto("/admin/choice-lists")
	await expect(page.getByRole("list", { name: "Manage choice lists" })).toBeVisible()
})

Then("each reporter-added choice is marked as such", async ({ page }) => {
	const list = page.getByRole("list", { name: "Manage choice lists" })

	await expect(list.getByText("mount 7")).toBeVisible()
	await expect(list.getByText("Added by a reporter")).toBeVisible()
	await expect(list.getByText("Cooper's")).toBeVisible()
})

Then("the page says how many are waiting to be reviewed", async ({ page }) => {
	await expect(page.getByText("Added by reporters and not yet reviewed: 1", { exact: false })).toBeVisible()
})

When("they correct the wording of a reporter-added choice and save", async ({ page }) => {
	await page.getByRole("button", { name: "Edit" }).click()
	await page.getByLabel("Choice (English)").nth(1).fill("Mount 7")
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the corrected wording is shown in the list", async ({ page }) => {
	const list = page.getByRole("list", { name: "Manage choice lists" })

	await expect(list.getByText("Mount 7", { exact: false })).toBeVisible()
})
