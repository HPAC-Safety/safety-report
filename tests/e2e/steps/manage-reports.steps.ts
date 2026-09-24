import { createBdd } from "playwright-bdd"

import { signInAs } from "./auth"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the Manage reports list and the read-only report
 * detail view (REQ-MOD-052..054, ADR-0053).
 *
 * The admin API is stubbed at the network boundary, as for the other admin
 * pages: what these scenarios assert is what the browser shows. The filtering,
 * stuck detection, and audit write themselves are proven against a real
 * database in HpacSafety.Api.Tests and the Reqnroll scenarios (ADR-0045).
 *
 * Every report below is synthetic.
 */

interface StubRow {
	id: string
	submittedAt: string
	status: string
	language: string
	consent: "yes" | "no" | "unanswered"
	isStuck: boolean
}

const ROWS: StubRow[] = [
	{ id: "pendingaaaa", submittedAt: "2026-09-20T15:30:00Z", status: "pending_review", language: "en-CA", consent: "yes", isStuck: false },
	{ id: "privateaaaa", submittedAt: "2026-09-19T15:30:00Z", status: "pending_review", language: "fr-CA", consent: "no", isStuck: false },
	{ id: "publishedaa", submittedAt: "2026-09-18T15:30:00Z", status: "published", language: "en-CA", consent: "yes", isStuck: false },
	{ id: "rejectedaaa", submittedAt: "2026-09-17T15:30:00Z", status: "rejected", language: "en-CA", consent: "yes", isStuck: false },
	{ id: "stuckaaaaaa", submittedAt: "2026-09-10T15:30:00Z", status: "summarizing", language: "en-CA", consent: "yes", isStuck: true },
]

const DETAIL = {
	...ROWS[0],
	summaryError: null,
	answers: [
		{
			questionKey: "pilot_name",
			labelEn: "Pilot name",
			labelFr: "Nom du pilote",
			isPrivate: true,
			values: [{ value: "Casey Synthetic", locale: "en-CA", translatedValue: null, translationSource: null }],
		},
		{
			questionKey: "narrative",
			labelEn: "What happened",
			labelFr: "Ce qui s'est passé",
			isPrivate: false,
			values: [
				{
					value: "A synthetic firm landing.",
					locale: "en-CA",
					translatedValue: "Un atterrissage ferme synthétique.",
					translationSource: "auto",
				},
			],
		},
	],
	summary: {
		aiSummaryEn: "The pilot made a firm landing.",
		aiSummaryFr: "Le pilote a fait un atterrissage ferme.",
		model: "gemini-3.7-flash",
		promptVersion: "summarize-anonymize.v3",
		generatedAt: "2026-09-20T15:35:00Z",
		updatedAt: "2026-09-20T15:35:00Z",
		approvedBySubject: null,
		approvedAt: null,
	},
	attachments: [{ id: "fileaaaaaaa", kind: "document", state: "ready" }],
}

const FILTERED: Record<string, (row: StubRow) => boolean> = {
	all: () => true,
	"needs-action": (row) => row.isStuck || row.status === "pending_review" || row.status === "summary_failed",
	published: (row) => row.status === "published",
	private: (row) => row.consent === "no",
	rejected: (row) => row.status === "rejected",
	"summary-failed": (row) => row.status === "summary_failed",
}

async function stubReports(page: Page) {
	await page.route(/\/api\/admin\/reports(\?.*)?$/, async (route) => {
		const filter = new URL(route.request().url()).searchParams.get("filter") ?? "all"
		await route.fulfill({ json: ROWS.filter(FILTERED[filter] ?? (() => false)) })
	})

	await page.route(/\/api\/admin\/reports\/[^/?]+$/, async (route) => {
		await route.fulfill({ json: DETAIL })
	})
}

function rows(page: Page) {
	return page.getByRole("list", { name: "Reports" }).getByRole("listitem")
}

Given("a safety officer is signed in and reports exist in several states", async ({ page }) => {
	await stubReports(page)
	await signInAs(page, "safety_officer")
})

When("the safety officer opens Manage reports", async ({ page }) => {
	await page.goto("/admin/reports")
	await expect(page.getByRole("heading", { level: 1, name: "Manage reports" })).toBeVisible()
})

When("the safety officer chooses the {string} filter", async ({ page }, filter: string) => {
	await page.getByRole("navigation", { name: "Filter reports" }).getByRole("link", { name: filter }).click()
})

When("the safety officer opens a pending-review report", async ({ page }) => {
	await page.locator(`[data-report-id="${ROWS[0].id}"] a`).click()
	await expect(page.getByRole("heading", { level: 1, name: "Report" })).toBeVisible()
})

Then("each report shows its submission time and a badge for its workflow status", async ({ page }) => {
	await expect(rows(page)).toHaveCount(ROWS.length)

	for (const row of await rows(page).all()) {
		await expect(row).toContainText("Submitted")
		await expect(row.locator('[data-badge="status"]')).toBeVisible()
	}

	await expect(page.locator(`[data-report-id="publishedaa"] [data-badge="status"]`)).toHaveText("Published")
	await expect(page.locator(`[data-report-id="pendingaaaa"] [data-badge="status"]`)).toHaveText("Pending review")
})

Then('a report whose reporter refused consent also shows a "Private \\(no consent)" badge', async ({ page }) => {
	await expect(page.locator(`[data-report-id="privateaaaa"] [data-badge="private"]`)).toHaveText("Private (no consent)")
	await expect(page.locator(`[data-report-id="privateaaaa"] [data-badge="status"]`)).toHaveText("Pending review")
	await expect(page.locator(`[data-report-id="pendingaaaa"] [data-badge="private"]`)).toHaveCount(0)
})

Then('a stuck report shows a "Stuck" badge', async ({ page }) => {
	await expect(page.locator(`[data-report-id="stuckaaaaaa"] [data-badge="stuck"]`)).toHaveText("Stuck")
	await expect(page.locator(`[data-report-id="pendingaaaa"] [data-badge="stuck"]`)).toHaveCount(0)
})

Then("only published reports are listed", async ({ page }) => {
	await expect(rows(page)).toHaveCount(1)
	await expect(rows(page).locator('[data-badge="status"]')).toHaveText("Published")
})

Then("the chosen filter stays in the address bar", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports\?filter=published$/)
	await expect(
		page.getByRole("navigation", { name: "Filter reports" }).getByRole("link", { name: "Published" }),
	).toHaveAttribute("aria-current", "page")

	// A reload keeps it: the filter is read from the address, not from memory.
	await page.reload()
	await expect(rows(page)).toHaveCount(1)
})

Then("its answers are shown under their questions, with each private answer marked private", async ({ page }) => {
	const pilot = page.locator('[data-question-key="pilot_name"]')
	await expect(pilot).toContainText("Pilot name")
	await expect(pilot).toContainText("Casey Synthetic")
	await expect(pilot.locator("[data-private-answer]")).toHaveText("Private")

	const narrative = page.locator('[data-question-key="narrative"]')
	await expect(narrative).toContainText("A synthetic firm landing.")
	await expect(narrative).toContainText("Un atterrissage ferme synthétique.")
	await expect(narrative.locator("[data-private-answer]")).toHaveCount(0)
})

Then("both the English and French summary texts are shown with the model and prompt version", async ({ page }) => {
	await expect(page.locator('[data-summary="en"]')).toHaveText(DETAIL.summary.aiSummaryEn)
	await expect(page.locator('[data-summary="fr"]')).toHaveText(DETAIL.summary.aiSummaryFr)
	await expect(page.locator("[data-provenance]")).toContainText("gemini-3.7-flash")
	await expect(page.locator("[data-provenance]")).toContainText("summarize-anonymize.v3")
})

Then("no action to edit, approve, reject, or publish is offered", async ({ page }) => {
	const main = page.getByRole("main")
	await expect(main.getByRole("button")).toHaveCount(0)
	await expect(main.getByRole("textbox")).toHaveCount(0)
})
