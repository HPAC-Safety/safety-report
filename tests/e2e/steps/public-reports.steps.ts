import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"
import { signInAs, stubAuth, type Role } from "./auth"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the public feed and each report's own page
 * (REQ-MOD-079..082, REQ-MOD-190..192, REQ-WLD-019, issue no. 28, issue no. 682).
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
	attachmentCount: number
}

const FIRST: StubReport = {
	id: "publicaaaa1",
	aiSummaryEn: "The pilot launched in a crosswind and landed safely in a nearby field.",
	aiSummaryFr: "Le pilote a décollé par vent de travers et s'est posé sans incident dans un champ voisin.",
	publishedAt: "2026-09-20T15:30:00Z",
	commentCount: 0,
	attachmentCount: 0,
}

const SECOND: StubReport = {
	id: "publicaaaa2",
	aiSummaryEn: "A reserve was deployed after a collapse at low altitude.",
	aiSummaryFr: "Un parachute de secours a été déployé après une fermeture à basse altitude.",
	publishedAt: "2026-09-18T15:30:00Z",
	commentCount: 0,
	attachmentCount: 0,
}

const OLDER: StubReport = {
	id: "publicaaaa3",
	aiSummaryEn: "The pilot misjudged the approach and landed short.",
	aiSummaryFr: "Le pilote a mal évalué l'approche et s'est posé court.",
	publishedAt: "2026-08-02T15:30:00Z",
	commentCount: 0,
	attachmentCount: 0,
}

const CURSOR = "c2Vjb25kLXBhZ2U"
const HIDDEN_ID = "hiddenaaaaa"

/** The one search term this stub recognizes; only FIRST's summary contains it. */
const SEARCH_TERM = "crosswind"

export async function stubFeed(page: Page) {
	await page.route(/\/api\/v1\/public\/reports\/?(\?.*)?$/, async (route) => {
		const url = new URL(route.request().url())
		const q = url.searchParams.get("q")

		if (q) {
			const matches = q.toLowerCase() === SEARCH_TERM ? [FIRST] : []
			await route.fulfill({ json: { items: matches, next: null } })
			return
		}

		const after = url.searchParams.get("after")
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

// --- REQ-MOD-153: the feed's attachment icon and count, omitted at zero ---

Given("the public feed holds a report with attachments and one with none", async ({ page }) => {
	await page.route(/\/api\/v1\/public\/reports\/?(\?.*)?$/, async (route) => {
		await route.fulfill({
			json: { items: [{ ...FIRST, attachmentCount: 3 }, { ...SECOND, attachmentCount: 0 }], next: null },
		})
	})
})

When("a visitor opens the public feed", async ({ page }) => {
	await page.goto("/reports")
})

Then("the report with attachments shows an attachment icon with its count, accessibly labelled", async ({ page }) => {
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`).getByText("3 attachments")).toBeVisible()
})

Then("the report with none shows no attachment icon", async ({ page }) => {
	await expect(page.locator(`[data-report-id="${SECOND.id}"]`).getByText(/attachments?/)).toHaveCount(0)
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
		;(window as unknown as { IntersectionObserver: unknown }).IntersectionObserver = NoObserver
	})
}

When("a visitor scrolls to the end of the list", async ({ page }) => {
	await page.goto("/reports")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	// Scroll the window to the bottom, as a visitor does. A short synthetic list
	// may already sit within the viewport and auto-load first, unmounting the
	// sentinel; waiting on the sentinel itself would then wait out the whole
	// test timeout for an element that never returns (issue no. 672).
	await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight))
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

// --- REQ-MOD-178: opening the feed afresh never restores a list kept from earlier ---

const firstPageRequests = new WeakMap<Page, { count: number }>()

Given("the visitor's window is too short to show the whole feed", async ({ page }) => {
	await page.setViewportSize({ width: 1280, height: 240 })
})

When(
	"the visitor follows the footer's link to the contact page, then the one back to View safety reports",
	async ({ page }) => {
		await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight))
		await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(0)

		const footer = page.getByRole("contentinfo")
		await footer.getByRole("link", { name: "Contact", exact: true }).click()
		await expect(page).toHaveURL(/\/contact$/)

		// Counted only from here, so the first visit's own request does not count.
		const counter = { count: 0 }
		firstPageRequests.set(page, counter)
		page.on("request", (request) => {
			const url = new URL(request.url())
			if (/\/api\/v1\/public\/reports\/?$/.test(url.pathname) && !url.searchParams.has("after")) counter.count += 1
		})

		await footer.getByRole("link", { name: "View safety reports", exact: true }).click()
		await expect(page).toHaveURL(/\/reports$/)
	},
)

Then("the public feed asks for its first page again", async ({ page }) => {
	await expect.poll(() => firstPageRequests.get(page)?.count ?? 0).toBeGreaterThan(0)
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
})

Then("the public feed is shown from its top", async ({ page }) => {
	// Taller than the window, so a position of 0 is the reset rather than a clamp.
	expect(await page.evaluate(() => document.documentElement.scrollHeight > window.innerHeight)).toBe(true)
	await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
})

/** The rendered width of whichever element currently holds focus, in CSS pixels. */
async function focusedWidth(page: Page): Promise<number> {
	const box = await page.locator(":focus").boundingBox()
	return box?.width ?? 0
}

/**
 * Tabs to the named button, bounded rather than one fixed Tab count, since
 * the two lists' rows carry a different number of stops (Manage reports' row
 * actions) before reaching this same fallback control.
 */
async function tabToButton(page: Page, name: string) {
	const target = page.getByRole("button", { name })
	const isFocused = () => target.evaluate((element) => element === document.activeElement).catch(() => false)

	if (await isFocused()) {
		return
	}

	for (let tabs = 0; tabs < 50; tabs += 1) {
		await page.keyboard.press("Tab")
		if (await isFocused()) {
			return
		}
	}
	throw new Error(`Could not reach the "${name}" action by tabbing.`)
}

When("a visitor opens the feed", async ({ page }) => {
	await disableAutoLoad(page)
	await page.goto("/reports")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
})

Then("the {string} action is not visible", async ({ page }, name: string) => {
	const box = await page.getByRole("button", { name }).boundingBox()
	// Tailwind's `sr-only` clips the button to a 1px square rather than
	// removing it, so it stays reachable by Tab; a real, usable button is far
	// wider than that.
	expect(box?.width ?? 0).toBeLessThanOrEqual(2)
})

When("a keyboard visitor tabs to the {string} action", async ({ page }, name: string) => {
	await tabToButton(page, name)
})

Then("it becomes visible", async ({ page }) => {
	expect(await focusedWidth(page)).toBeGreaterThan(10)
})

When("that visitor activates it", async ({ page }) => {
	await page.keyboard.press("Enter")
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

When("a visitor activates the {string} action without scrolling", async ({ page }, name: string) => {
	await disableAutoLoad(page)
	await page.goto("/reports")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await tabToButton(page, name)
	await page.keyboard.press("Enter")
})

Then("the feed offers a visible {string} action instead of failing silently", async ({ page }, name: string) => {
	await expect(page.getByRole("button", { name })).toBeVisible()
})

/*
 * Search (#574, REQ-MOD-149): the search box at the top of /reports, and its
 * bookmarkable ?q=. What actually matches — the summary and comments, scoped
 * to the site's language — is proven against a real database by the Reqnroll
 * scenarios REQ-MOD-140..148; this only proves the box's own wiring.
 */

function searchBox(page: Page) {
	return page.getByRole("searchbox", { name: "Search safety reports" })
}

When("the visitor types a search term into the search box at the top of the page", async ({ page }) => {
	await searchBox(page).fill(SEARCH_TERM)
})

Then("the address bar carries that search term as ?q=", async ({ page }) => {
	await expect(page).toHaveURL(new RegExp(`[?&]q=${SEARCH_TERM}(&|$)`))
})

Then("only matching reports are listed", async ({ page }) => {
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await expect(page.locator(`[data-report-id="${SECOND.id}"]`)).toHaveCount(0)
})

Then("the search box still shows that search term, and only matching reports are listed", async ({ page }) => {
	await expect(searchBox(page)).toHaveValue(SEARCH_TERM)
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await expect(page.locator(`[data-report-id="${SECOND.id}"]`)).toHaveCount(0)
})

When("the visitor goes back", async ({ page }) => {
	await page.goBack()
})

Then("the search box is empty and the full feed is shown again", async ({ page }) => {
	await expect(searchBox(page)).toHaveValue("")
	await expect(page.locator(`[data-report-id="${FIRST.id}"]`)).toBeVisible()
	await expect(page.locator(`[data-report-id="${SECOND.id}"]`)).toBeVisible()
})

// ── Admin link from a published report's page (issue no. 657, REQ-MOD-164/165) ──

const ADMIN_LINK_ROLES: Record<string, Role> = {
	Administrator: "administrator",
	SafetyOfficer: "safety_officer",
	User: "user",
}

Given(
	/^(?:a signed-in (Administrator|SafetyOfficer|User)|a signed-out visitor) visits a published report's page$/,
	async ({ page }, roleLabel?: string) => {
		await stubFeed(page)

		if (roleLabel) {
			await signInAs(page, ADMIN_LINK_ROLES[roleLabel])
		} else {
			await stubAuth(page)
		}

		await page.goto(`/reports/${FIRST.id}`)
	},
)

function adminLink(page: Page) {
	return page.locator("[data-admin-link]")
}

Then("the page offers a link to that report's admin detail page", async ({ page }) => {
	await expect(adminLink(page)).toBeVisible()
	await expect(adminLink(page)).toHaveAttribute("href", `/admin/reports/${FIRST.id}`)
})

When("the visitor activates that link", async ({ page }) => {
	await adminLink(page).click()
})

Then("the browser opens the report's admin detail page, in the same tab", async ({ page, context }) => {
	await expect(page).toHaveURL(new RegExp(`/admin/reports/${FIRST.id}$`))
	expect(context.pages()).toHaveLength(1)
})

Then("the page offers no link to the admin detail page", async ({ page }) => {
	await expect(adminLink(page)).toHaveCount(0)
})

// ── The translation label (issue no. 682, REQ-MOD-190..192, ADR-0176) ──

const WRITTEN_IN: Record<string, "en-CA" | "fr-CA"> = { English: "en-CA", French: "fr-CA" }

/**
 * What the label reads in each site language, keyed by the report's language.
 * The French wording comes from CI's translation of the English catalogue
 * (ADR-0057); on a branch that has not been translated yet the catalogue holds
 * the `#`-prefixed stub of the English instead, so that is accepted too.
 */
const TRANSLATED_FROM: Record<string, RegExp> = {
	"Translated from French": /^Translated from French$/,
	"Translated from English": /^Translated from English$/,
	"Traduit de l'anglais": /^(Traduit de l['\u2019]anglais|#Translated from English)$/,
	"Traduit du français": /^(Traduit du français|#Translated from French)$/,
}

Given(/^a report written in (English|French) is published$/, async ({ page }, written: string) => {
	await stubFeed(page)
	// Registered after stubFeed's own, so it answers first.
	await page.route(/\/api\/v1\/public\/reports\/[^/?]+$/, async (route) => {
		await route.fulfill({ json: { ...FIRST, language: WRITTEN_IN[written], media: [] } })
	})
})

async function openPageInSiteLanguage(page: Page, shown: string) {
	await page.addInitScript((code) => localStorage.setItem("hpac.locale", code), WRITTEN_IN[shown])
	await page.goto(`/reports/${FIRST.id}`)
}

When(/^a visitor opens its page with the site in (English|French)$/, async ({ page }, shown: string) => {
	await openPageInSiteLanguage(page, shown)
})

Given(/^a visitor has its page open with the site in (English|French)$/, async ({ page }, shown: string) => {
	await openPageInSiteLanguage(page, shown)
	await expect(page.locator("[data-summary]")).toBeVisible()
})

Then(/^the page shows the muted label "(.+)"$/, async ({ page }, label: string) => {
	const line = page.locator("[data-translated-from]")
	await expect(line).toBeVisible()
	await expect(line).toHaveText(TRANSLATED_FROM[label])
})

Then("the page shows no translation label", async ({ page }) => {
	await expect(page.locator("[data-summary]")).toBeVisible()
	await expect(page.locator("[data-translated-from]")).toHaveCount(0)
})
