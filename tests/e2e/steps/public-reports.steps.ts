import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the public feed and each report's own page
 * (REQ-MOD-079..082, REQ-WLD-019, issue no. 28).
 *
 * The public API is stubbed at the network boundary: what these assert is what
 * the browser shows and what the address bar says. The feed's order, its
 * cursor, the allowlist, and the indistinguishable 404 are proven against a
 * real database by the Reqnroll scenarios REQ-MOD-036..038 (ADR-0045).
 *
 * Every report below is synthetic.
 */

interface StubReport {
	id: string
	aiSummaryEn: string
	aiSummaryFr: string
	publishedAt: string
	commentCount: number
}

const FIRST: StubReport = {
	id: "publicaaaa1",
	aiSummaryEn: "The pilot launched in a crosswind and landed safely in a nearby field.",
	aiSummaryFr: "Le pilote a décollé par vent de travers et s'est posé sans incident dans un champ voisin.",
	publishedAt: "2026-09-20T15:30:00Z",
	commentCount: 0,
}

const SECOND: StubReport = {
	id: "publicaaaa2",
	aiSummaryEn: "A reserve was deployed after a collapse at low altitude.",
	aiSummaryFr: "Un parachute de secours a été déployé après une fermeture à basse altitude.",
	publishedAt: "2026-09-18T15:30:00Z",
	commentCount: 0,
}

const OLDER: StubReport = {
	id: "publicaaaa3",
	aiSummaryEn: "The pilot misjudged the approach and landed short.",
	aiSummaryFr: "Le pilote a mal évalué l'approche et s'est posé court.",
	publishedAt: "2026-08-02T15:30:00Z",
	commentCount: 0,
}

const CURSOR = "c2Vjb25kLXBhZ2U"
const HIDDEN_ID = "hiddenaaaaa"

async function stubFeed(page: Page) {
	await page.route(/\/api\/v1\/public\/reports\/?(\?.*)?$/, async (route) => {
		const after = new URL(route.request().url()).searchParams.get("after")
		await route.fulfill({
			json: after === CURSOR ? { items: [OLDER], next: null } : { items: [FIRST, SECOND], next: CURSOR },
		})
	})

	// The report page reads its comments too; these reports have none.
	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/comments\/?$/, async (route) => {
		await route.fulfill({ json: [] })
	})

	await page.route(/\/api\/v1\/public\/reports\/[^/?]+$/, async (route) => {
		const id = new URL(route.request().url()).pathname.split("/").pop()
		const report = [FIRST, SECOND, OLDER].find((candidate) => candidate.id === id)
		await (report ? route.fulfill({ json: report }) : route.fulfill({ status: 404, body: "" }))
	})
}

async function expectFullSummary(page: Page, report: StubReport) {
	await expect(page.locator('[data-summary="en-CA"]')).toHaveText(report.aiSummaryEn)
}

Given("the public feed has published reports", async ({ page }) => {
	await stubFeed(page)
})

Given("the public feed has more published reports than fit on one page", async ({ page }) => {
	await stubFeed(page)
})

Given("a visitor has the address of a published report", async ({ page }) => {
	await stubFeed(page)
})

Given("a report ID the public API answers with 404", async ({ page }) => {
	await stubFeed(page)
})

Given("a published report has both ai_summary_en and ai_summary_fr", async ({ page }) => {
	await stubFeed(page)
})

When("a visitor opens View safety reports and selects one", async ({ page }) => {
	await page.goto("/reports")
	await page.getByRole("list", { name: "Published safety reports" }).getByRole("link").first().click()
})

When("the visitor opens that address directly", async ({ page }) => {
	await page.goto(`/reports/${FIRST.id}`)
})

When("a visitor opens \\/reports\\/ followed by that ID", async ({ page }) => {
	await page.goto(`/reports/${HIDDEN_ID}`)
})

When("a visitor moves to the next page", async ({ page }) => {
	await page.goto("/reports")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await page.getByRole("link", { name: "Older reports" }).click()
})

When("a visitor views it in a given locale", async ({ page, context }) => {
	await context.addInitScript(() => localStorage.setItem("hpac.locale", "fr-CA"))
	await page.goto(`/reports/${FIRST.id}`)
})

Then("the address bar shows \\/reports\\/ followed by that report's ID", async ({ page }) => {
	await expect(page).toHaveURL(new RegExp(`/reports/${FIRST.id}$`))
})

Then("the page shows that report's full summary in the visitor's language", async ({ page }) => {
	await expectFullSummary(page, FIRST)
})

Then("the page shows that report's full summary", async ({ page }) => {
	await expectFullSummary(page, FIRST)
})

Then("the page still shows that report's full summary", async ({ page }) => {
	await expect(page).toHaveURL(new RegExp(`/reports/${FIRST.id}$`))
	await expectFullSummary(page, FIRST)
})

Then("the page says the report was not found", async ({ page }) => {
	await expect(page.getByRole("heading", { level: 1, name: "Report not found" })).toBeVisible()
})

Then("it says nothing about whether such a report exists", async ({ page }) => {
	const text = (await page.locator("main").innerText()).toLowerCase()

	for (const hint of ["deleted", "unpublished", "private", "rejected", "consent", "review"]) {
		expect(text).not.toContain(hint)
	}
})

Then("the address bar carries that page's cursor", async ({ page }) => {
	await expect(page).toHaveURL(new RegExp(`/reports\\?after=${CURSOR}$`))
	await expect(page.locator(`[data-report-id="${OLDER.id}"]`)).toBeVisible()
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toHaveCount(0)
})

Then("going back returns the visitor to the first page", async ({ page }) => {
	await page.goBack()
	await expect(page).toHaveURL(/\/reports$/)
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
})

Then("that locale's text is shown first", async ({ page }) => {
	await expect(page.locator('[data-summary="fr-CA"]')).toHaveText(FIRST.aiSummaryFr)
})

Then("the visitor can switch to the counterpart text", async ({ page }) => {
	await page.getByRole("article").getByRole("button").click()
	await expect(page.locator('[data-summary="en-CA"]')).toHaveText(FIRST.aiSummaryEn)
})
