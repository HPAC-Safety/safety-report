import { createBdd } from "playwright-bdd"
import { expect, type Page, type Request } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions, stubSubmission } from "./report-form-fixture"
import { stubFeed } from "./public-reports.steps"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for a reporter's own report before it is published
 * (REQ-PUB-010 to REQ-PUB-014, REQ-SUB-136, issue no. 820, ADR-0196).
 *
 * The API is stubbed at the network boundary: what these assert is what the
 * browser keeps, sends, and shows. Which reports are a receipt holder's own, and
 * that nobody else sees them, is proven against a real database by the Reqnroll
 * scenarios REQ-PUB-001 to REQ-PUB-009 (ADR-0045).
 *
 * Every report and receipt below is synthetic.
 */

const OWN_NEWER = {
	id: "ownreport01",
	submittedAt: "2026-09-25T15:30:00Z",
	forPublication: true,
	aiSummaryEn: "## Description\nThe pilot launched in a crosswind and a reviewer is still editing this draft.",
	aiSummaryFr: "## Description\nLe pilote a décollé par vent de travers et un réviseur modifie encore ce brouillon.",
	attachmentCount: 0,
}

const OWN_OLDER = {
	id: "ownreport02",
	submittedAt: "2026-09-22T15:30:00Z",
	forPublication: true,
	aiSummaryEn: null,
	aiSummaryFr: null,
	attachmentCount: 0,
}

const OWN_PRIVATE = {
	id: "ownreport03",
	submittedAt: "2026-09-21T15:30:00Z",
	forPublication: false,
	aiSummaryEn: null,
	aiSummaryFr: null,
	attachmentCount: 0,
}

const RECEIPT = "synthetic-receipt-AAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
const STORAGE_KEY = "hpac.report.receipts"

/** The browser already holds one receipt per report, as a submission would have left. */
async function holdReceipts(page: Page, reportIds: string[]) {
	const receipts = reportIds.map((reportId) => ({ reportId, receipt: `${RECEIPT}-${reportId}` }))
	await page.addInitScript(
		([key, value]) => {
			// Only once: a reload must see what the page itself kept or dropped.
			if (localStorage.getItem("hpac.test.seeded") === null) {
				localStorage.setItem(key, value)
				localStorage.setItem("hpac.test.seeded", "yes")
			}
		},
		[STORAGE_KEY, JSON.stringify(receipts)],
	)
}

/** Answers the receipt lookup; registered after the public stubs, so it is matched first. */
async function stubLookup(page: Page, answer: { items: unknown[]; settled: string[] }) {
	await page.route(/\/api\/v1\/public\/reports\/own$/, async (route) => {
		await route.fulfill({ json: answer })
	})
}

const lookups = new WeakMap<Page, Request[]>()

function watchLookups(page: Page) {
	const seen: Request[] = []
	lookups.set(page, seen)
	page.on("request", (request) => {
		if (/\/api\/v1\/public\/reports\/own/.test(request.url())) seen.push(request)
	})
}

// --- REQ-PUB-010: at the top of the first page, newest first ---

Given("a browser holds receipts for two of its own reports that are not published", async ({ page }) => {
	await holdReceipts(page, [OWN_NEWER.id, OWN_OLDER.id])
	await stubLookup(page, { items: [OWN_NEWER, OWN_OLDER], settled: [] })
})

When("the visitor opens View safety reports", async ({ page }) => {
	await page.goto("/reports")
})

// --- REQ-PUB-031: the own reports are not repeated among the later pages ---

Given("the visitor has View safety reports open, showing their two own reports", async ({ page }) => {
	await page.goto("/reports")
	await expect(page.getByRole("list", { name: "Your reports" }).getByRole("listitem")).toHaveCount(2)
})

Then("the two own reports are listed first, newest submitted first, each with its pill", async ({ page }) => {
	const own = page.getByRole("list", { name: "Your reports" })
	await expect(own.getByRole("listitem")).toHaveCount(2)
	await expect(own.getByRole("listitem").nth(0)).toHaveAttribute("data-own-report-id", OWN_NEWER.id)
	await expect(own.getByRole("listitem").nth(1)).toHaveAttribute("data-own-report-id", OWN_OLDER.id)
	await expect(own.getByText("Not yet published")).toHaveCount(2)

	// Above the public list, not beside it.
	const ownBox = await own.boundingBox()
	const publicBox = await page.getByRole("list", { name: "Published safety reports" }).boundingBox()
	expect(ownBox?.y ?? 0).toBeLessThan(publicBox?.y ?? 0)
})

Then("the public feed follows them", async ({ page }) => {
	const published = page.getByRole("list", { name: "Published safety reports" })
	await expect(published.getByRole("listitem").first()).toBeVisible()
})

When("the visitor loads the later pages of the feed", async ({ page }) => {
	// The feed loads its later pages on its own as the visitor nears the end.
	await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight))
	await expect(page.locator('[data-report-id="publicaaaa3"]')).toBeVisible()
})

Then("the own reports are still listed once, above the first page, and not among the later pages", async ({ page }) => {
	await expect(page.locator(`[data-own-report-id="${OWN_NEWER.id}"]`)).toHaveCount(1)
	await expect(page.locator(`[data-own-report-id="${OWN_OLDER.id}"]`)).toHaveCount(1)
	await expect(page.locator(`[data-report-id="${OWN_NEWER.id}"]`)).toHaveCount(0)
	await expect(page.locator(`[data-report-id="${OWN_OLDER.id}"]`)).toHaveCount(0)
})

// --- REQ-PUB-011: the pill, the draft label, the placeholder ---

Given(
	"a browser holds receipts for one report with a summary, one still without a summary, and one without publication consent",
	async ({ page }) => {
		await stubFeed(page)
		await holdReceipts(page, [OWN_NEWER.id, OWN_OLDER.id, OWN_PRIVATE.id])
		await stubLookup(page, { items: [OWN_NEWER, OWN_OLDER, OWN_PRIVATE], settled: [] })
	},
)

Then(
	"the first shows its submitted date, the pill {string}, and its summary labelled as a draft that may change",
	async ({ page }, pill: string) => {
		const first = page.locator(`[data-own-report-id="${OWN_NEWER.id}"]`)
		await expect(first.getByText(pill)).toBeVisible()
		await expect(first).toContainText("Submitted September 25, 2026")
		await expect(first).toContainText("a reviewer is still editing this draft")
		await expect(first).toContainText("Draft summary. It may change before the report is published.")
	},
)

Then("the second shows its submitted date, the pill, and {string}", async ({ page }, placeholder: string) => {
	const second = page.locator(`[data-own-report-id="${OWN_OLDER.id}"]`)
	await expect(second.getByText("Not yet published")).toBeVisible()
	await expect(second).toContainText("Submitted September 22, 2026")
	await expect(second).toContainText(placeholder)
})

Then("the third shows the pill {string} and no summary", async ({ page }, pill: string) => {
	const third = page.locator(`[data-own-report-id="${OWN_PRIVATE.id}"]`)
	await expect(third.getByText(pill)).toBeVisible()
	await expect(third).not.toContainText("Draft summary")
	await expect(third).not.toContainText("Summary in preparation")
})

Then("no pill reads {string}", async ({ page }, word: string) => {
	await expect(page.locator("[data-own-pill]").filter({ hasText: word })).toHaveCount(0)
	await expect(page.getByRole("list", { name: "Your reports" }).getByText(word, { exact: true })).toHaveCount(0)
})

// --- REQ-PUB-012: a receipt is dropped once its report is published or gone ---

Given("a browser holds receipts for a report that has been published and one a moderator deleted", async ({ page }) => {
	await stubFeed(page)
	await holdReceipts(page, ["pubnowaaaa1", "deletedaaa1"])
	await stubLookup(page, { items: [], settled: ["pubnowaaaa1", "deletedaaa1"] })
})

Then("neither is listed as the visitor's own", async ({ page }) => {
	await expect(page.getByRole("list", { name: "Published safety reports" })).toBeVisible()
	await expect(page.getByRole("list", { name: "Your reports" })).toHaveCount(0)
})

Then("the browser no longer holds either receipt", async ({ page }) => {
	await expect.poll(async () => page.evaluate((key) => localStorage.getItem(key), STORAGE_KEY)).toBeNull()
})

// --- REQ-PUB-013: no receipt, no lookup ---

Given("a browser holds no receipt", async ({ page }) => {
	await stubFeed(page)
	watchLookups(page)
})

Then("the feed lists only published reports", async ({ page }) => {
	await expect(page.getByRole("list", { name: "Published safety reports" }).getByRole("listitem").first()).toBeVisible()
	await expect(page.getByRole("list", { name: "Your reports" })).toHaveCount(0)
})

Then("the browser sends no receipt lookup", ({ page }) => {
	expect(lookups.get(page)).toEqual([])
})

// --- REQ-PUB-014: the holder's own page ---

Given("a browser holds the receipt for a report that is not published", async ({ page }) => {
	await stubFeed(page)
	await holdReceipts(page, [OWN_NEWER.id])
	watchLookups(page)

	await page.route(/\/api\/v1\/public\/reports\/own\/[^/?]+$/, async (route) => {
		await route.fulfill({ json: { ...OWN_NEWER, language: "en-CA", media: [] } })
	})
})

When("the visitor opens that report's address", async ({ page }) => {
	await page.goto(`/reports/${OWN_NEWER.id}`)
})

Then("the page shows the report with its pill, its submitted date, and its summary labelled as a draft", async ({ page }) => {
	const article = page.locator(`[data-own-report="${OWN_NEWER.id}"]`)
	await expect(article.getByText("Not yet published")).toBeVisible()
	await expect(article).toContainText("Submitted September 25, 2026")
	await expect(article.locator("[data-draft-note]")).toHaveText("Draft summary. It may change before the report is published.")
	await expect(article.locator('[data-summary="en-CA"]')).toContainText("a reviewer is still editing this draft")
})

Then("the page offers no comments", async ({ page }) => {
	await expect(page.locator(`[data-own-report="${OWN_NEWER.id}"]`)).toBeVisible()
	await expect(page.getByRole("heading", { name: /comments/i })).toHaveCount(0)
	await expect(page.getByRole("textbox")).toHaveCount(0)
})

Then("the receipt is sent inside the request and never in any address", ({ page }) => {
	const asked = lookups.get(page) ?? []
	expect(asked.length).toBeGreaterThan(0)

	for (const request of asked) {
		expect(request.method()).toBe("POST")
		expect(request.url()).not.toContain(RECEIPT)
		expect(request.postData()).toContain(`${RECEIPT}-${OWN_NEWER.id}`)
	}
})

// --- REQ-SUB-136: the browser keeps the receipt after a 202 ---

const addresses = new WeakMap<Page, string[]>()

Given("a member submits a valid report", async ({ page }) => {
	const seen: string[] = []
	addresses.set(page, seen)
	page.on("request", (request) => seen.push(request.url()))
	page.on("framenavigated", (frame) => seen.push(frame.url()))

	await stubAuth(page)
	await stubCurrentQuestions(page)
	await stubSubmission(page, { body: { id: "synthetic-report-id", status: "submitted", receipt: RECEIPT } })
	await signInAs(page, "user")
	await page.goto("/report")

	// Walk the pages to the last, which holds the one required question.
	// Each check waits for the page to show Next or Submit: isVisible() alone
	// does not wait, so it raced the page that was still rendering.
	const submit = page.getByRole("button", { name: "Submit report" })
	const next = page.getByRole("button", { name: "Next" })
	for (let step = 0; step < 12; step++) {
		await expect(next.or(submit).first()).toBeVisible()
		if (await submit.isVisible()) break
		await next.click()
	}
	await page.getByRole("group", { name: /May we publish a summary of this report\?/ }).getByRole("radio", { name: "Yes" }).click()
})

When("the report is accepted with a receipt", async ({ page }) => {
	const answered = page.waitForResponse((response) => response.url().includes("/api/v1/reports/") && response.status() === 202)
	await page.getByRole("button", { name: "Submit report" }).click()
	await answered
})

Then("the browser keeps the report ID and the receipt in its own storage", async ({ page }) => {
	await expect
		.poll(async () => page.evaluate((key) => localStorage.getItem(key), STORAGE_KEY))
		.toBe(JSON.stringify([{ reportId: "synthetic-report-id", receipt: RECEIPT }]))
})

Then("no request address or navigation carries the receipt", ({ page }) => {
	expect((addresses.get(page) ?? []).filter((address) => address.includes(RECEIPT))).toEqual([])
})
