import { readFileSync } from "node:fs"

import { createBdd } from "playwright-bdd"
import { expect, type Download, type Page } from "@playwright/test"

import { signInAs } from "./auth"
import { openReport, REPORT_ID, reportDetail, stubReportDetail, type StubAnswer } from "./admin-report-fixture"
import { present } from "./present"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the admin site as a whole: it is one route of the
 * one site (REQ-WLD-001), a report's private context, content, and summary
 * are visibly distinct (REQ-WLD-020), a destructive action asks first
 * (REQ-WLD-048), and a document is only ever a download (REQ-MED-012).
 *
 * The API and the storage link are stubbed at the network boundary, so what is
 * asserted is what the browser draws and sends. Who may delete, unpublish, or
 * download is proven against a real API by the moderation and media Reqnroll
 * scenarios (ADR-0045).
 *
 * Every report, answer, and file below is synthetic.
 */

// --- REQ-WLD-001: the review queue is a route of the one site ---

const PUBLIC_REPORT = {
	id: "siteaaaaaa1",
	aiSummaryEn: "The pilot landed in a field after a crosswind launch.",
	aiSummaryFr: "Le pilote s'est posé dans un champ après un décollage par vent de travers.",
	publishedAt: "2026-09-20T15:30:00Z",
	commentCount: 0,
	media: [],
	attachmentCount: 0,
	staffAttachments: null,
}

interface Origin {
	origin: string
}

const reportPageOrigin = new WeakMap<Page, Origin>()

Given("a Safety Officer is on the public report page", async ({ page }) => {
	await signInAs(page, "safety_officer")
	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/comments\/?$/, (route) => route.fulfill({ json: [] }))
	await page.route(/\/api\/v1\/public\/reports\/[^/?]+$/, (route) => route.fulfill({ json: PUBLIC_REPORT }))
	await page.route(/\/api\/admin\/reports(\?.*)?$/, (route) => route.fulfill({ json: { items: [], next: null } }))

	await page.goto(`/reports/${PUBLIC_REPORT.id}`)
	await expect(page.getByText(PUBLIC_REPORT.aiSummaryEn)).toBeVisible()

	// A property of this document's window: a new document load discards it.
	await page.evaluate(() => {
		;(window as unknown as { __sameDocument: boolean }).__sameDocument = true
	})
	reportPageOrigin.set(page, { origin: new URL(page.url()).origin })
})

When("they follow the Admin menu to the report list", async ({ page }) => {
	await page.locator("header").getByRole("button", { name: /^Admin/ }).click()
	await page.getByRole("menu", { name: "Admin" }).getByRole("menuitem", { name: /^Manage reports/ }).click()
})

Then("the report list loads on the same origin as the report page", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports$/)
	await expect(page.getByRole("heading", { level: 1, name: "Manage reports" })).toBeVisible()
	expect(new URL(page.url()).origin).toBe(present(reportPageOrigin.get(page)).origin)
})

Then("the browser does not load a new document", async ({ page }) => {
	const survived = await page.evaluate(() => (window as unknown as { __sameDocument?: boolean }).__sameDocument === true)
	expect(survived).toBe(true)
})

// --- REQ-WLD-020: private context, content, and summary are visibly distinct ---

const PILOT_NAME = "Casey Synthetic"
const NARRATIVE = "The glider touched down short of the field."
const FAILURE = "The AI chat provider was unavailable."

function answer(partial: Pick<StubAnswer, "questionKey" | "labelEn" | "labelFr" | "isPrivate"> & { value: string }): StubAnswer {
	const { value, ...rest } = partial
	return {
		...rest,
		type: "short_text",
		values: [{ value, locale: "en-CA", translatedValue: null, translationSource: null }],
	}
}

const region = (page: Page, name: string) => page.getByRole("region", { name, exact: true })

const LOCALES: Record<string, string> = { English: "en-CA", French: "fr-CA" }

// The catalogue the interface reads, so no French is written here: CI fills
// fr-CA.json, and its current value is what the reviewer sees.
function catalogueText(locale: string, key: string, values: Record<string, string> = {}): string {
	const entries = JSON.parse(readFileSync(new URL(`../../../locales/${locale}.json`, import.meta.url), "utf8")) as Record<string, unknown>
	// en-CA.json is flat; fr-CA.json nests on the dots. Either shape resolves.
	const text = typeof entries[key] === "string"
		? entries[key]
		: (key.split(".").reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], entries) as string)
	expect(typeof text, `catalogue key ${key} in ${locale}`).toBe("string")
	return text.replace(/\{(\w+)\}/g, (_, name: string) => values[name] ?? "")
}

const reviewerLocale = new WeakMap<Page, string>()
const reviewerText = (page: Page, key: string, values?: Record<string, string>) => catalogueText(present(reviewerLocale.get(page)), key, values)

Given(/^a reviewer whose language is (English|French) opens a report in the admin site$/, async ({ page, context }, language: string) => {
	const locale = LOCALES[language]
	reviewerLocale.set(page, locale)
	await context.addInitScript((chosen) => localStorage.setItem("hpac.locale", chosen), locale)

	const report = {
		...reportDetail([
			answer({ questionKey: "pilot_name", labelEn: "Pilot name", labelFr: "Nom du pilote", isPrivate: true, value: PILOT_NAME }),
			answer({ questionKey: "narrative", labelEn: "What happened", labelFr: "Que s'est-il passé", isPrivate: false, value: NARRATIVE }),
		]),
		consent: true,
		// A failed processing step and an unapproved pair, so both are shown.
		summaryError: FAILURE,
	}
	await stubReportDetail(page, report)
	await openReport(page)
	await expect(page.locator("html")).toHaveAttribute("lang", locale)
})

Then("private answers, ordinary answers, and the summary pair each sit in their own labeled section", async ({ page }) => {
	const priv = region(page, reviewerText(page, "reports.detail.privateAnswers"))
	await expect(priv.locator('[data-question-key="pilot_name"]')).toContainText(PILOT_NAME)
	await expect(priv.locator('[data-question-key="narrative"]')).toHaveCount(0)

	const ordinary = region(page, reviewerText(page, "reports.detail.answers"))
	await expect(ordinary.locator('[data-question-key="narrative"]')).toContainText(NARRATIVE)
	await expect(ordinary.locator('[data-question-key="pilot_name"]')).toHaveCount(0)

	const summary = region(page, reviewerText(page, "reports.detail.summary"))
	await expect(summary.locator('[data-summary="en"]')).toBeVisible()
	await expect(summary.locator('[data-summary="fr"]')).toBeVisible()
	await expect(summary.locator("[data-question-key]")).toHaveCount(0)
})

Then("each private answer is marked private in the reviewer's language", async ({ page }) => {
	const marks = page.locator("[data-private-answer]")
	await expect(marks).toHaveCount(1)
	await expect(marks).toHaveText(reviewerText(page, "reports.detail.private"))
	await expect(region(page, reviewerText(page, "reports.detail.privateAnswers")).locator('[data-question-key="pilot_name"] [data-private-answer]')).toHaveCount(1)
	await expect(region(page, reviewerText(page, "reports.detail.answers")).locator("[data-private-answer]")).toHaveCount(0)
})

Then("processing failures and the approval state are shown apart from the report's content", async ({ page }) => {
	const failed = reviewerText(page, "reports.detail.summaryFailed", { error: FAILURE })
	const notApproved = reviewerText(page, "reports.detail.notApproved")

	const status = region(page, reviewerText(page, "reports.detail.status"))
	await expect(status).toContainText(failed)
	await expect(status).toContainText(notApproved)

	for (const key of ["reports.detail.privateAnswers", "reports.detail.answers", "reports.detail.summary"]) {
		await expect(region(page, reviewerText(page, key))).not.toContainText(failed)
		await expect(region(page, reviewerText(page, key))).not.toContainText(notApproved)
	}
})

// --- REQ-WLD-048: a destructive action asks first ---

const writes = new WeakMap<Page, string[]>()
const chosen = new WeakMap<Page, "delete" | "unpublish">()

Given("a reviewer is on a published report in the admin site", async ({ page }) => {
	const report = { ...reportDetail(), status: "published", consent: true, publishedAt: "2026-09-21T12:00:00Z" }
	await stubReportDetail(page, report)

	const sent: string[] = []
	writes.set(page, sent)
	// Registered after the stub's GET, so it is tried first and answers every method.
	await page.route(new RegExp(`/api/admin/reports/${REPORT_ID}(/.*)?$`), (route) => {
		const request = route.request()
		if (request.method() === "GET") return route.fulfill({ json: report })
		sent.push(`${request.method()} ${new URL(request.url()).pathname}`)
		return request.method() === "DELETE" ? route.fulfill({ status: 204 }) : route.fulfill({ json: report })
	})

	await openReport(page)
})

When(/^they choose to (delete|unpublish) the report$/, async ({ page }, action: "delete" | "unpublish") => {
	chosen.set(page, action)
	await page.getByRole("button", { name: action === "delete" ? "Delete" : "Unpublish", exact: true }).click()
})

Then("the admin site asks them to confirm before sending anything", async ({ page }) => {
	if (chosen.get(page) === "delete") {
		await expect(page.getByRole("dialog", { name: "Delete this report?" })).toBeVisible()
		await expect(page.getByRole("button", { name: "Delete report" })).toBeVisible()
	} else {
		await expect(page.getByRole("form", { name: "Unpublish the report" })).toBeVisible()
		await expect(page.getByRole("button", { name: "Unpublish report" })).toBeVisible()
	}
	expect(writes.get(page)).toEqual([])
})

Then("cancelling sends no request", async ({ page }) => {
	if (chosen.get(page) === "delete") {
		await page.getByRole("button", { name: "Keep report" }).click()
		await expect(page.getByRole("dialog")).toHaveCount(0)
	} else {
		await page.getByRole("button", { name: "Cancel" }).click()
		await expect(page.getByRole("form", { name: "Unpublish the report" })).toHaveCount(0)
	}
	await expect(page.getByRole("button", { name: "Unpublish", exact: true })).toBeVisible()
	expect(writes.get(page)).toEqual([])
})

// --- REQ-MED-012: a document is only ever a download ---

const DOCUMENT_ID = "documentaa1"
const STORAGE = "https://storage.example.test"
const PDF = Buffer.from("%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n")

const downloads = new WeakMap<Page, Download>()
const adminRequests = new WeakMap<Page, string[]>()

Given("a reviewer opens a document attachment", async ({ page }) => {
	const report = {
		...reportDetail(),
		consent: true,
		attachments: [{ id: DOCUMENT_ID, kind: "document", state: "ready", visibility: "private", format: "pdf" }],
	}
	await stubReportDetail(page, report)

	const seen: string[] = []
	adminRequests.set(page, seen)
	await page.route(new RegExp(`/api/admin/reports/${REPORT_ID}/attachments/[^/?]+/[a-z]+$`), (route) => {
		seen.push(new URL(route.request().url()).pathname)
		return route.fulfill({ json: { url: `${STORAGE}/${DOCUMENT_ID}`, expiresAt: "2026-09-20T15:45:00Z", fileName: `${DOCUMENT_ID}.pdf` } })
	})
	// As S3 serves a document: a forced download (ADR-0119).
	await page.route(`${STORAGE}/**`, (route) =>
		route.fulfill({
			status: 200,
			contentType: "application/pdf",
			headers: { "Content-Disposition": `attachment; filename="${DOCUMENT_ID}.pdf"` },
			body: PDF,
		}),
	)

	await openReport(page)
})

When("the admin site presents it", async ({ page }) => {
	const pending = page.waitForEvent("download")
	await page.getByRole("button", { name: "Download Document 1 of 1 (PDF)" }).click()
	downloads.set(page, await pending)
})

Then("the admin site does not embed, preview, or inline-render the document content", async ({ page }) => {
	await expect(page.locator("main").locator("iframe, embed, object, img, video")).toHaveCount(0)
	await expect(page.getByRole("dialog")).toHaveCount(0)
	await expect(page).toHaveURL(new RegExp(`/admin/reports/${REPORT_ID}$`))
})

Then("the document is offered only as a download", ({ page }) => {
	expect(present(downloads.get(page)).suggestedFilename()).toBe(`${DOCUMENT_ID}.pdf`)
	// The download endpoint, never the inline /view one.
	expect(adminRequests.get(page)).toEqual([`/api/admin/reports/${REPORT_ID}/attachments/${DOCUMENT_ID}/download`])
})
