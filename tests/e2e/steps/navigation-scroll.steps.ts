import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

/*
 * REQ-WLD-032: following a link to another page starts that page at its top,
 * on every page, because the reset lives at the router (issue no. 670). The
 * window is made short enough that every page here is taller than it, so a
 * scroll position of 0 on arrival is the reset, not the browser clamping a
 * page too short to scroll. Every report here is synthetic.
 */

const PAGES: Record<string, string> = { contact: "/contact", "public feed": "/reports", report: "/report" }

const FEED = Array.from({ length: 6 }, (_, index) => ({
	id: `scrolltop${index}`,
	aiSummaryEn: `A synthetic report, number ${index + 1}, long enough to take up a row of the feed.`,
	aiSummaryFr: `Un signalement fictif, numéro ${index + 1}, assez long pour occuper une ligne du fil.`,
	publishedAt: `2026-09-${String(20 - index).padStart(2, "0")}T15:30:00Z`,
	commentCount: 0,
	attachmentCount: 0,
}))

async function stubFeed(page: Page) {
	await page.route(/\/api\/v1\/public\/reports\/?(\?.*)?$/, (route) => route.fulfill({ json: { items: FEED, next: null } }))
}

const scrollY = (page: Page) => page.evaluate(() => window.scrollY)

/** The public feed paints its reports after the page itself; wait for them before measuring. */
async function settle(page: Page, name: string) {
	await expect(page.getByRole("contentinfo")).toBeVisible()
	if (name === "public feed") {
		await expect(page.locator(`[data-report-id="${FEED[0].id}"]`)).toBeVisible()
	}
}

Given(
	/^a visitor is at the bottom of the (contact|public feed|report) page, in a window too short to show it all$/,
	async ({ page }, from: string) => {
		await page.setViewportSize({ width: 1280, height: 240 })
		await stubFeed(page)
		await page.goto(PAGES[from])
		await settle(page, from)
		await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight))
		await expect.poll(() => scrollY(page)).toBeGreaterThan(0)
	},
)

When("they follow the footer's {string} link", async ({ page }, link: string) => {
	await page.getByRole("contentinfo").getByRole("link", { name: link, exact: true }).click()
})

Then(/^the (contact|public feed|report) page is shown from its top$/, async ({ page }, to: string) => {
	await expect(page).toHaveURL(new RegExp(`${PAGES[to]}$`))
	await settle(page, to)
	// Taller than the window, so a position of 0 is the reset rather than a clamp.
	expect(await page.evaluate(() => document.documentElement.scrollHeight > window.innerHeight)).toBe(true)
	await expect.poll(() => scrollY(page)).toBe(0)
})
