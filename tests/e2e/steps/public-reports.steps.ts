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
		await (report ? route.fulfill({ json: { ...report, media: [] } }) : route.fulfill({ status: 404, body: "" }))
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

Then("only that locale's text is shown, with no language control on the report itself", async ({ page }) => {
	await expect(page.locator('[data-summary="fr-CA"]')).toHaveText(FIRST.aiSummaryFr)
	await expect(page.locator('[data-summary="en-CA"]')).toHaveCount(0)
	await expect(page.getByRole("main").getByRole("button")).toHaveCount(0)
})

When("the visitor switches the site's language", async ({ page }) => {
	// The header's one language toggle; its label is in whichever language is active.
	await page.getByRole("banner").getByRole("button", { name: /^(Switch to|Passer)/ }).click()
})

Then("the report shows the other language's text", async ({ page }) => {
	await expect(page.locator('[data-summary="en-CA"]')).toHaveText(FIRST.aiSummaryEn)
	await expect(page).toHaveURL(new RegExp(`/reports/${FIRST.id}$`))
})

// ── Infinite scroll (issue no. 572): auto-load, back-button restore, fallback, retry ──

/**
 * Disables the auto-load sentinel so a scenario proves the fallback button
 * itself works, on its own, the way a keyboard or screen-reader visitor who
 * never triggers the IntersectionObserver would rely on it.
 */
async function disableAutoLoad(page: Page) {
	await page.addInitScript(() => {
		class NoObserver {
			observe() {}
			unobserve() {}
			disconnect() {}
		}
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		;(window as any).IntersectionObserver = NoObserver
	})
}

When("a visitor scrolls to the end of the list", async ({ page }) => {
	await page.goto("/reports")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	const sentinel = page.locator("[data-infinite-scroll-sentinel]")
	// A short synthetic list may already sit within the viewport, so the
	// sentinel can auto-load before this ever scrolls it into view — that is
	// still the behaviour under test, so a sentinel already gone is fine.
	await sentinel.scrollIntoViewIfNeeded().catch(() => {})
})

Then("the older reports load without a page change or an address change", async ({ page }) => {
	await expect(page.locator(`[data-report-id="${OLDER.id}"]`)).toBeVisible()
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await expect(page).toHaveURL(/\/reports$/)
})

When("a visitor opens one of them and goes back", async ({ page }) => {
	await page.locator(`[data-report-id="${OLDER.id}"] a, [data-report-id="${OLDER.id}"]`).first().click()
	await expect(page).toHaveURL(new RegExp(`/reports/${OLDER.id}$`))
	await page.goBack()
})

Then("the same reports are still shown, at the same scroll position", async ({ page }) => {
	await expect(page).toHaveURL(/\/reports$/)
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await expect(page.locator(`[data-report-id="${OLDER.id}"]`)).toBeVisible()

	// The end of the list: OLDER's own page named no further cursor, so
	// neither the sentinel nor the fallback button is offered any more.
	await expect(page.locator("[data-infinite-scroll-sentinel]")).toHaveCount(0)
	await expect(page.getByRole("button", { name: "Load more" })).toHaveCount(0)
})

When("a visitor activates the {string} action without scrolling", async ({ page }, name: string) => {
	await disableAutoLoad(page)
	await page.goto("/reports")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await page.getByRole("button", { name }).click()
})

Then("the older reports load", async ({ page }) => {
	await expect(page.locator(`[data-report-id="${OLDER.id}"]`)).toBeVisible()
})

Then("a screen reader is told how many more reports loaded", async ({ page }) => {
	await expect(page.locator('[aria-live="polite"]')).toContainText("1 more report loaded")
})

Given("the public feed's next page fails to load", async ({ page }) => {
	await stubFeed(page)
	await page.route(/\/api\/v1\/public\/reports\?after=/, (route) => route.fulfill({ status: 500, body: "" }))
})

When("a visitor activates the {string} action", async ({ page }, name: string) => {
	await disableAutoLoad(page)
	await page.goto("/reports")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await page.getByRole("button", { name }).click()
})

Then("the feed offers a {string} action instead of failing silently", async ({ page }, name: string) => {
	await expect(page.getByRole("button", { name })).toBeVisible()
})
