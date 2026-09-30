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
	consent: boolean | null
	isStuck: boolean
	version: string
	reporterName: string | null
	pilotName: string | null
	attachmentCount: number
}

const ROWS: StubRow[] = [
	{
		id: "pendingaaaa",
		submittedAt: "2026-09-20T15:30:00Z",
		status: "pending",
		language: "en-CA",
		consent: true,
		isStuck: false,
		version: "11.1",
		reporterName: "Alex Rivera",
		pilotName: "Sam Chen",
		attachmentCount: 0,
	},
	{ id: "privateaaaa", submittedAt: "2026-09-19T15:30:00Z", status: "unpublished", language: "fr-CA", consent: false, isStuck: false, version: "12.0", reporterName: null, pilotName: null, attachmentCount: 0 },
	{ id: "publishedaa", submittedAt: "2026-09-18T15:30:00Z", status: "published", language: "en-CA", consent: true, isStuck: false, version: "13.1", reporterName: null, pilotName: null, attachmentCount: 0 },
	{ id: "unpublished", submittedAt: "2026-09-17T15:30:00Z", status: "unpublished", language: "en-CA", consent: true, isStuck: false, version: "14.1", reporterName: null, pilotName: null, attachmentCount: 0 },
	{ id: "failedaaaaa", submittedAt: "2026-09-16T15:30:00Z", status: "summary_failed", language: "en-CA", consent: true, isStuck: false, version: "15.0", reporterName: null, pilotName: null, attachmentCount: 0 },
	{ id: "stuckaaaaaa", submittedAt: "2026-09-10T15:30:00Z", status: "summarizing", language: "en-CA", consent: true, isStuck: true, version: "16.0", reporterName: null, pilotName: null, attachmentCount: 0 },
]

const DETAIL = {
	...ROWS[0],
	summaryError: null,
	answers: [
		{
			questionKey: "pilot_name",
			labelEn: "Pilot name",
			labelFr: "Nom du pilote",
			type: "short_text",
			isPrivate: true,
			values: [{ value: "Casey Synthetic", locale: "en-CA", translatedValue: null, translationSource: null }],
		},
		{
			// The API gives an email no second language, even for a row that
			// stored one before ADR-0112.
			questionKey: "pilot_email",
			labelEn: "Email",
			labelFr: "Courriel",
			isPrivate: true,
			values: [{ value: "casey@example.test", locale: "en-CA", translatedValue: null, translationSource: null }],
		},
		{
			questionKey: "narrative",
			labelEn: "What happened",
			labelFr: "Ce qui s'est passé",
			type: "long_text",
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
		sourceEn: "generated",
		sourceFr: "generated",
	},
	attachments: [{ id: "fileaaaaaaa", kind: "document", state: "ready", visibility: "private", format: "pdf" }],
	mediaConsent: null,
	summaryRevisions: [
		{
			id: "revisionaa1",
			sequence: 1,
			aiSummaryEn: "The pilot made a firm landing.",
			aiSummaryFr: "Le pilote a fait un atterrissage ferme.",
			sourceEn: "generated",
			sourceFr: "generated",
			authorSubject: null,
			createdAt: "2026-09-20T15:35:00Z",
			restoredFromSequence: null,
			approvedBySubject: null,
			approvedAt: null,
			isCurrent: true,
		} as SummaryRevisionStub,
	],
}

interface SummaryRevisionStub {
	id: string
	sequence: number
	aiSummaryEn: string
	aiSummaryFr: string
	sourceEn: string
	sourceFr: string
	authorSubject: string | null
	createdAt: string
	restoredFromSequence: number | null
	approvedBySubject: string | null
	approvedAt: string | null
	isCurrent: boolean
}

const FILTERED: Record<string, (row: StubRow) => boolean> = {
	all: () => true,
	"needs-action": (row) => row.isStuck || row.status === "pending" || row.status === "summary_failed",
	published: (row) => row.status === "published",
	unpublished: (row) => row.status === "unpublished",
	private: (row) => row.consent === false,
	"summary-failed": (row) => row.status === "summary_failed",
}

/*
 * The list's own copy of the rows, so a row action changes what the next list
 * request returns, and every command the page sent, with its body.
 */
interface ListStub {
	rows: StubRow[]
	stale: Set<string>
	sent: { method: string; path: string; version?: string }[]
}

const listStubs = new WeakMap<Page, ListStub>()

async function stubReports(page: Page) {
	const stub: ListStub = { rows: ROWS.map((row) => ({ ...row })), stale: new Set(), sent: [] }
	listStubs.set(page, stub)

	await page.route(/\/api\/admin\/reports(\?.*)?$/, async (route) => {
		const url = new URL(route.request().url())
		const filter = url.searchParams.get("filter") ?? "all"
		const q = url.searchParams.get("q")
		let rows = stub.rows.filter(FILTERED[filter] ?? (() => false))

		// The stub only simulates that a query narrows and reorders the list —
		// what actually matches (every answer, choice label, summary, note,
		// comment, or file name), and that paging a ranked result never skips or
		// repeats a report, is proven server-side in HpacSafety.Api.Tests and the
		// Reqnroll scenarios (ADR-0156).
		if (q) {
			const needle = q.toLowerCase()
			rows = rows.filter(
				(row) => (row.reporterName ?? "").toLowerCase().includes(needle) || (row.pilotName ?? "").toLowerCase().includes(needle),
			)
		}

		await route.fulfill({ json: { items: rows, next: null } })
	})

	await page.route(/\/api\/admin\/reports\/[^/?]+(\/(publish|unpublish))?$/, async (route) => {
		const request = route.request()
		const path = new URL(request.url()).pathname
		const id = path.split("/")[4]

		if (request.method() === "GET") {
			return route.fulfill({ json: DETAIL })
		}

		stub.sent.push({ method: request.method(), path, version: request.postDataJSON()?.version })

		if (request.method() === "DELETE") {
			stub.rows = stub.rows.filter((row) => row.id !== id)
			return route.fulfill({ status: 204 })
		}

		if (stub.stale.has(id)) {
			return route.fulfill({
				status: 409,
				contentType: "application/problem+json",
				body: JSON.stringify({
					type: "https://hpac.ca/problems/stale-report",
					title: "This report changed since you opened it.",
					detail: "Another reviewer saved a change to this report.",
				}),
			})
		}

		const row = stub.rows.find((candidate) => candidate.id === id)!
		row.status = path.endsWith("/unpublish") ? "unpublished" : "published"
		row.version = `${row.version}+`
		return route.fulfill({ json: { ...DETAIL, ...row } })
	})
}

/** The stubbed row each scenario word names. */
const ROW_BY_WORD: Record<string, string> = {
	pending: "pendingaaaa",
	published: "publishedaa",
	unpublished: "unpublished",
	"private-unpublished": "privateaaaa",
	"summary-failed": "failedaaaaa",
	stuck: "stuckaaaaaa",
}

function row(page: Page, word: string) {
	return page.locator(`[data-report-id="${ROW_BY_WORD[word]}"]`)
}

function rowButtons(page: Page, word: string) {
	return row(page, word).getByRole("group").getByRole("button")
}

function rows(page: Page) {
	return page.getByRole("list", { name: "Reports" }).getByRole("listitem")
}

Given("a safety officer is signed in and reports exist in several states", async ({ page }) => {
	await stubReports(page)
	await signInAs(page, "safety_officer")
})

// --- REQ-MOD-154: Manage reports shows each row's attachment icon and count ---

Given("a safety officer is signed in and Manage reports holds a report with attachments and one with none", async ({ page }) => {
	await stubReports(page)
	listStubs.get(page)!.rows.find((candidate) => candidate.id === "pendingaaaa")!.attachmentCount = 3
	await signInAs(page, "safety_officer")
})

Then("the row with attachments shows an attachment icon with its count, accessibly labelled", async ({ page }) => {
	await expect(row(page, "pending").getByText("3 attachments")).toBeVisible()
})

Then("the row with none shows no attachment icon", async ({ page }) => {
	await expect(row(page, "published").getByText(/attachments?/)).toHaveCount(0)
})

When("the safety officer opens Manage reports", async ({ page }) => {
	await page.goto("/admin/reports")
	await expect(page.getByRole("heading", { level: 1, name: "Manage reports" })).toBeVisible()
})

When("the safety officer chooses the {string} filter", async ({ page }, filter: string) => {
	await page.getByRole("navigation", { name: "Filter reports" }).getByRole("link", { name: filter, exact: true }).click()
})

When("the safety officer searches for {string}", async ({ page }, text: string) => {
	await page.getByRole("searchbox", { name: "Search reports" }).fill(text)
})

When("the safety officer searches for a word that matches nothing", async ({ page }) => {
	await page.getByRole("searchbox", { name: "Search reports" }).fill("zzsynthnothingmatchesanything")
})

When("the safety officer clears the search box", async ({ page }) => {
	await page.getByRole("searchbox", { name: "Search reports" }).fill("")
})

When("the safety officer reloads the page", async ({ page }) => {
	await page.reload()
})

When("the safety officer opens a pending report", async ({ page }) => {
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
	await expect(page.locator(`[data-report-id="pendingaaaa"] [data-badge="status"]`)).toHaveText("Pending")
})

Then('a report whose reporter refused consent also shows a "Private \\(no consent)" badge', async ({ page }) => {
	await expect(page.locator(`[data-report-id="privateaaaa"] [data-badge="private"]`)).toHaveText("Private (no consent)")
	await expect(page.locator(`[data-report-id="privateaaaa"] [data-badge="status"]`)).toHaveText("Unpublished")
	await expect(page.locator(`[data-report-id="pendingaaaa"] [data-badge="private"]`)).toHaveCount(0)
})

Then('a stuck report shows a "Stuck" badge', async ({ page }) => {
	await expect(page.locator(`[data-report-id="stuckaaaaaa"] [data-badge="stuck"]`)).toHaveText("Stuck")
	await expect(page.locator(`[data-report-id="pendingaaaa"] [data-badge="stuck"]`)).toHaveCount(0)
})

Then('the pending row shows reporter name "Alex Rivera" and pilot name "Sam Chen"', async ({ page }) => {
	await expect(row(page, "pending")).toContainText("Reporter: Alex Rivera")
	await expect(row(page, "pending")).toContainText("Pilot: Sam Chen")
})

Then("the published row shows no reporter or pilot name", async ({ page }) => {
	await expect(row(page, "published")).not.toContainText("Reporter:")
	await expect(row(page, "published")).not.toContainText("Pilot:")
})

Then("only published reports are listed", async ({ page }) => {
	await expect(rows(page)).toHaveCount(1)
	await expect(rows(page).locator('[data-badge="status"]')).toHaveText("Published")
})

Then("the chosen filter stays in the address bar", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports\?filter=published$/)
	await expect(
		page.getByRole("navigation", { name: "Filter reports" }).getByRole("link", { name: "Published", exact: true }),
	).toHaveAttribute("aria-current", "page")

	// A reload keeps it: the filter is read from the address, not from memory.
	await page.reload()
	await expect(rows(page)).toHaveCount(1)
})

Then("every report is listed newest first as before the search", async ({ page }) => {
	await expect(page).not.toHaveURL(/[?&]q=/)
	await expect(rows(page)).toHaveCount(ROWS.length)
})

Then('the address bar carries "q=Alex"', async ({ page }) => {
	await expect(page).toHaveURL(/[?&]q=Alex$/)
})

Then('the search box still reads "Alex"', async ({ page }) => {
	await expect(page.getByRole("searchbox", { name: "Search reports" })).toHaveValue("Alex")
})

Then("a message says no reports match that search", async ({ page }) => {
	await expect(page.getByText('No reports match "zzsynthnothingmatchesanything" in this filter.')).toBeVisible()
})

Then("no error is shown", async ({ page }) => {
	await expect(page.getByRole("alert")).toHaveCount(0)
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

/*
 * Review actions (REQ-MOD-062..068). One stubbed report per status; each
 * command answers with the report as the API would leave it, so what is
 * asserted is what the page does with the answer.
 */

type StubStatus = "pending" | "published" | "unpublished" | "summary_failed"

const STATUS_BY_WORD: Record<string, StubStatus> = {
	pending: "pending",
	"machine-translated": "pending",
	published: "published",
	unpublished: "unpublished",
	"private-unpublished": "unpublished",
	"summary-failed": "summary_failed",
}

interface ReviewStub {
	detail: Omit<typeof DETAIL, "summary" | "consent" | "status" | "summaryError"> & {
		status: string
		consent: string
		summaryError: string | null
		summary: typeof DETAIL.summary | null
		version: string
		unpublishNote: string | null
		publishedAt: string | null
		summaryRevisions: SummaryRevisionStub[]
	}
	stale: boolean
	requests: string[]
}

const reviewStubs = new WeakMap<Page, ReviewStub>()

function detailIn(status: StubStatus) {
	return {
		...DETAIL,
		id: "reviewaaaaa",
		status,
		version: "1.1",
		unpublishNote: null,
		publishedAt: status === "published" ? "2026-09-21T12:00:00Z" : null,
		summaryError: status === "summary_failed" ? "The AI chat provider was unavailable." : null,
		summary: status === "summary_failed" ? null : { ...DETAIL.summary },
		summaryRevisions: status === "summary_failed" ? [] : DETAIL.summaryRevisions.map((revision) => ({ ...revision })),
	}
}

/*
 * A pending report whose summary has a history of four versions: the Worker's,
 * a reviewer's edit, a restore of the first, and a later edit (the current one).
 */
function detailWithHistory() {
	const detail = detailIn("pending")
	const revisions: SummaryRevisionStub[] = [
		{ ...DETAIL.summaryRevisions[0], isCurrent: false },
		{
			id: "revisionaa2",
			sequence: 2,
			aiSummaryEn: "The pilot made a firm landing in gusts.",
			aiSummaryFr: "Le pilote a fait un atterrissage ferme dans les rafales.",
			sourceEn: "human",
			sourceFr: "machine",
			authorSubject: "auth0|synthetic-editor",
			createdAt: "2026-09-21T10:00:00Z",
			restoredFromSequence: null,
			approvedBySubject: null,
			approvedAt: null,
			isCurrent: false,
		},
		{
			id: "revisionaa3",
			sequence: 3,
			aiSummaryEn: DETAIL.summary.aiSummaryEn,
			aiSummaryFr: DETAIL.summary.aiSummaryFr,
			sourceEn: "generated",
			sourceFr: "generated",
			authorSubject: "auth0|synthetic-restorer",
			createdAt: "2026-09-22T10:00:00Z",
			restoredFromSequence: 1,
			approvedBySubject: null,
			approvedAt: null,
			isCurrent: false,
		},
		{
			id: "revisionaa4",
			sequence: 4,
			aiSummaryEn: "The pilot made a firm landing after the collapse of the wind.",
			aiSummaryFr: "Le pilote a fait un atterrissage ferme après la chute du vent.",
			sourceEn: "human",
			sourceFr: "human",
			authorSubject: "auth0|synthetic-editor",
			createdAt: "2026-09-23T10:00:00Z",
			restoredFromSequence: null,
			approvedBySubject: null,
			approvedAt: null,
			isCurrent: true,
		},
	]
	const current = revisions[3]

	return {
		...detail,
		summary: { ...detail.summary!, aiSummaryEn: current.aiSummaryEn, aiSummaryFr: current.aiSummaryFr, sourceEn: "human", sourceFr: "human" },
		summaryRevisions: [...revisions].reverse(),
	}
}

/** A new revision on top of the history, as the API answers an edit or a rollback (ADR-0177). */
function withRevision(
	detail: ReviewStub["detail"],
	text: { en: string; fr: string },
	sources: { en: string; fr: string },
	restoredFromSequence: number | null,
): ReviewStub["detail"] {
	const isLive = detail.status === "published"
	const sequence = detail.summaryRevisions[0].sequence + 1
	const created: SummaryRevisionStub = {
		id: `revisionaa${sequence}`,
		sequence,
		aiSummaryEn: text.en,
		aiSummaryFr: text.fr,
		sourceEn: sources.en,
		sourceFr: sources.fr,
		authorSubject: "auth0|synthetic-officer",
		createdAt: "2026-09-24T10:00:00Z",
		restoredFromSequence,
		// A live report publishes what is saved, approved by whoever saved it.
		approvedBySubject: isLive ? "auth0|synthetic-officer" : null,
		approvedAt: isLive ? "2026-09-24T10:00:00Z" : null,
		isCurrent: true,
	}

	return {
		...detail,
		version: `1.${sequence}`,
		status: isLive ? "published" : "pending",
		publishedAt: isLive ? detail.publishedAt : null,
		summary: {
			...detail.summary!,
			aiSummaryEn: text.en,
			aiSummaryFr: text.fr,
			sourceEn: sources.en as "generated",
			sourceFr: sources.fr as "generated",
			approvedBySubject: created.approvedBySubject,
			approvedAt: created.approvedAt,
		},
		summaryRevisions: [created, ...detail.summaryRevisions.map((revision) => ({ ...revision, isCurrent: false }))],
	}
}

async function stubReview(page: Page, status: StubStatus, word = "") {
	const detail = word === "history" ? detailWithHistory() : detailIn(status)
	const stub: ReviewStub = {
		// A report without consent is never summarized and stays unpublished (REQ-DOM-006, REQ-DOM-015).
		detail: word.startsWith("private-")
			? { ...detail, consent: false, summary: null }
			: word === "machine-translated"
				? { ...detail, summary: { ...detail.summary!, sourceEn: "human", sourceFr: "machine" } }
				: detail,
		stale: false,
		requests: [],
	}
	reviewStubs.set(page, stub)

	await page.route(/\/api\/admin\/reports(\?.*)?$/, async (route) => {
		await route.fulfill({ json: { items: [{ ...ROWS[0], id: stub.detail.id, status: stub.detail.status }], next: null } })
	})

	await page.route(/\/api\/admin\/reports\/reviewaaaaa(\/.*)?$/, async (route) => {
		const request = route.request()
		const path = new URL(request.url()).pathname
		stub.requests.push(`${request.method()} ${path}`)

		if (request.method() === "GET" && path.endsWith("/download")) {
			return route.fulfill({ json: { url: "/attachment-opened", expiresAt: "2026-09-23T12:05:00Z", fileName: "evidence.pdf" } })
		}

		if (request.method() === "GET") {
			return route.fulfill({ json: stub.detail })
		}

		if (request.method() === "DELETE") {
			return route.fulfill({ status: 204 })
		}

		if (stub.stale) {
			return route.fulfill({
				status: 409,
				contentType: "application/problem+json",
				body: JSON.stringify({
					type: "https://hpac.ca/problems/stale-report",
					title: "This report changed since you opened it.",
					detail: "Another reviewer saved a change to this report.",
				}),
			})
		}

		const body = request.postDataJSON() ?? {}
		const next = { ...stub.detail, version: "1.2" }

		if (path.endsWith("/unpublish")) {
			next.status = "unpublished"
			next.publishedAt = null
			next.unpublishNote = body.note || null
		} else if (path.endsWith("/publish")) {
			next.status = "published"
			next.publishedAt = "2026-09-23T12:00:00Z"
			next.unpublishNote = null
		} else if (path.endsWith("/summary")) {
			stub.detail = withRevision(
				stub.detail,
				{ en: body.aiSummaryEn, fr: body.aiSummaryFr },
				{ en: body.sourceEn ?? "human", fr: body.sourceFr ?? "human" },
				null,
			)
			return route.fulfill({ json: stub.detail })
		} else if (path.endsWith("/rollback")) {
			const target = stub.detail.summaryRevisions.find((revision) => path.includes(`/revisions/${revision.id}/`))!
			stub.detail = withRevision(
				stub.detail,
				{ en: target.aiSummaryEn, fr: target.aiSummaryFr },
				{ en: target.sourceEn, fr: target.sourceFr },
				target.sequence,
			)
			return route.fulfill({ json: stub.detail })
		}

		stub.detail = next
		return route.fulfill({ json: next })
	})
}

Given("a safety officer is signed in and a {word} report exists", async ({ page }, word: string) => {
	await stubReview(page, STATUS_BY_WORD[word], word)
	await signInAs(page, "safety_officer")
})

Given("a safety officer is signed in and a pending report with four summary revisions exists", async ({ page }) => {
	await stubReview(page, "pending", "history")
	await signInAs(page, "safety_officer")
})

Given("another reviewer has changed that report since it was opened", async ({ page }) => {
	reviewStubs.get(page)!.stale = true
})

When("the safety officer opens that report", async ({ page }) => {
	await page.goto("/admin/reports/reviewaaaaa")
	await expect(page.getByRole("heading", { level: 1, name: /^(Report|Signalement)$/ })).toBeVisible()
	await expect(page.locator('[data-badge="status"]')).toBeVisible()
})

Then("the offered actions are {}", async ({ page }, list: string) => {
	const expected = list.split(",").map((name) => name.trim())
	const group = page.getByRole("group", { name: "Review actions" })
	await expect(group.getByRole("button")).toHaveText(expected)

	// A report without consent was never summarized, so it has no summary panel.
	if (reviewStubs.get(page)!.detail.consent === false) {
		await expect(page.locator("#summary-heading")).toHaveCount(0)
	}
})

When("the safety officer edits the English summary and saves", async ({ page }) => {
	await page.getByRole("button", { name: "Edit summary" }).click()
	await page.getByLabel("English summary").fill("The pilot landed firmly after the collapse.")
	await page.getByRole("button", { name: "Save summary" }).click()
})

When("the safety officer publishes it", async ({ page }) => {
	await page.getByRole("button", { name: "Publish", exact: true }).click()
})

When("the safety officer unpublishes it with the note {string}", async ({ page }, note: string) => {
	await page.getByRole("button", { name: "Unpublish", exact: true }).click()
	await page.getByLabel("Note for other reviewers (optional)").fill(note)
	await page.getByRole("button", { name: "Unpublish report" }).click()
})

When("the safety officer chooses Delete", async ({ page }) => {
	await page.getByRole("button", { name: "Delete" }).click()
})

When("the safety officer confirms", async ({ page }) => {
	await page.getByRole("dialog").getByRole("button", { name: "Delete report" }).click()
})

When("the safety officer opens its document attachment", async ({ page }) => {
	await page.route("**/attachment-opened", (route) => route.fulfill({ body: "synthetic" }))
	await page.getByRole("button", { name: /^Download/ }).click()
})

Then("the report shows the {string} badge", async ({ page }, badge: string) => {
	await expect(page.locator('[data-badge="status"]')).toHaveText(badge)
})

Then("the saved English text is shown", async ({ page }) => {
	await expect(page.locator('[data-summary="en"]')).toHaveText("The pilot landed firmly after the collapse.")
	const sent = reviewStubs.get(page)!.requests
	expect(sent).toContain("PUT /api/admin/reports/reviewaaaaa/summary")
})

Then("Save summary is not offered", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Save summary" })).toBeDisabled()
})

Then("Save summary is offered", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Save summary" })).toBeEnabled()
})

Then("the revision history lists four revisions, newest first", async ({ page }) => {
	const revisions = page.locator("[data-summary-history] [data-revision]")
	await expect(revisions).toHaveCount(4)
	await expect(revisions.evaluateAll((items) => items.map((item) => item.getAttribute("data-revision")))).resolves.toEqual([
		"4",
		"3",
		"2",
		"1",
	])
	await expect(revisions.first().locator("[data-current-revision]")).toBeVisible()
})

Then("each shows its author, its time, and how each language was written", async ({ page }) => {
	const second = page.locator('[data-revision="2"]')
	await expect(second.locator("[data-revision-saved]")).toContainText("auth0|synthetic-editor")
	await expect(second.locator("[data-revision-saved]")).toContainText("2026")
	await expect(second.locator("[data-revision-sources]")).toHaveText("English: Written by a reviewer. French: Machine-translated.")

	// The Worker's version has no author, and says so.
	const first = page.locator('[data-revision="1"]')
	await expect(first.locator("[data-revision-saved]")).toContainText("by the AI")
	await expect(first.locator("[data-revision-sources]")).toHaveText("English: Generated by the AI. French: Generated by the AI.")
})

Then("the restored revision says which revision it was restored from", async ({ page }) => {
	await expect(page.locator('[data-revision="3"] [data-revision-restored-from]')).toHaveText("Restored from version 1.")
	await expect(page.locator('[data-revision="2"] [data-revision-restored-from]')).toHaveCount(0)
})

When("the safety officer views the first revision", async ({ page }) => {
	await page.locator('[data-revision="1"]').getByRole("button", { name: "View this version" }).click()
})

Then("that revision's English and French text is shown", async ({ page }) => {
	const first = page.locator('[data-revision="1"]')
	await expect(first.locator('[data-revision-text="en"]')).toHaveText(DETAIL.summaryRevisions[0].aiSummaryEn)
	await expect(first.locator('[data-revision-text="fr"]')).toHaveText(DETAIL.summaryRevisions[0].aiSummaryFr)
})

Then("the current summary is unchanged", async ({ page }) => {
	await expect(page.locator('[data-summary="en"]')).toHaveText("The pilot made a firm landing after the collapse of the wind.")
	expect(reviewStubs.get(page)!.requests.filter((request) => !request.startsWith("GET"))).toEqual([])
})

When("the safety officer chooses Restore this version on the first revision", async ({ page }) => {
	await page.locator('[data-revision="1"]').getByRole("button", { name: "Restore this version" }).click()
})

Then("a confirmation asks whether to restore that version", async ({ page }) => {
	const dialog = page.getByRole("dialog", { name: "Restore version 1?" })
	await expect(dialog).toBeVisible()
	await expect(dialog).toContainText("saved as a draft")
})

Then("nothing has been restored yet", async ({ page }) => {
	expect(reviewStubs.get(page)!.requests.some((request) => request.includes("/rollback"))).toBe(false)
	await expect(page.locator("[data-summary-history] [data-revision]")).toHaveCount(4)
})

When("the safety officer confirms the restore", async ({ page }) => {
	await page.getByRole("dialog").getByRole("button", { name: "Restore version" }).click()
})

Then("the browser asks the API to restore that revision", async ({ page }) => {
	await expect
		.poll(() => reviewStubs.get(page)!.requests)
		.toContain("POST /api/admin/reports/reviewaaaaa/summary/revisions/revisionaa1/rollback")
})

Then("the restored text is the current summary", async ({ page }) => {
	await expect(page.locator('[data-summary="en"]')).toHaveText(DETAIL.summaryRevisions[0].aiSummaryEn)
	const newest = page.locator("[data-summary-history] [data-revision]").first()
	await expect(newest).toHaveAttribute("data-revision", "5")
	await expect(newest.locator("[data-current-revision]")).toBeVisible()
	await expect(newest.locator("[data-revision-restored-from]")).toHaveText("Restored from version 1.")
})

Then("the note {string} is shown", async ({ page }, note: string) => {
	await expect(page.locator("[data-unpublish-note]")).toContainText(note)
})

Then("a message says the report changed and offers to reload it", async ({ page }) => {
	await expect(page.getByRole("alert")).toContainText("Another reviewer changed this report")
	await expect(page.getByRole("button", { name: "Reload report" })).toBeVisible()
	await expect(page.locator('[data-badge="status"]')).toHaveText("Pending")
})

Then("a confirmation asks whether to delete the report", async ({ page }) => {
	await expect(page.getByRole("dialog", { name: "Delete this report?" })).toBeVisible()
	expect(reviewStubs.get(page)!.requests).not.toContain("DELETE /api/admin/reports/reviewaaaaa")
})

Then("the browser returns to Manage reports", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports$/)
	expect(reviewStubs.get(page)!.requests).toContain("DELETE /api/admin/reports/reviewaaaaa")
})

Then("the browser requests that attachment's download link", async ({ page }) => {
	await expect
		.poll(() => reviewStubs.get(page)!.requests)
		.toContain(`GET /api/admin/reports/reviewaaaaa/attachments/${DETAIL.attachments[0].id}/download`)
})

/*
 * Translating one summary language from the other (REQ-MOD-071..074,
 * ADR-0108). The translation endpoint is stubbed with a recognisable marker so
 * the proposed text is predictable.
 */

const translatedFrom = (text: string) => `Traduction : ${text}`

async function stubTranslate(page: Page) {
	await page.route("**/api/admin/translate", async (route) => {
		const body = route.request().postDataJSON() as { texts: string[] }
		await route.fulfill({ json: { texts: body.texts.map(translatedFrom) } })
	})
}

Given("a safety officer is signed in and a report whose French text was machine-translated exists", async ({ page }) => {
	await stubReview(page, "pending", "machine-translated")
	await signInAs(page, "safety_officer")
})

When("the safety officer opens the summary editor", async ({ page }) => {
	await stubTranslate(page)
	await page.getByRole("button", { name: "Edit summary" }).click()
})

When("the safety officer changes {}", async ({ page }, what: string) => {
	if (what.includes("English")) await page.getByLabel("English summary").fill("The pilot landed firmly after the collapse.")
	if (what.includes("French")) await page.getByLabel("French summary").fill("Le pilote s'est posé fermement après la fermeture.")
})

When("the safety officer chooses Write summary", async ({ page }) => {
	await stubTranslate(page)
	await page.getByRole("button", { name: "Write summary" }).click()
})

When("the safety officer types the English text", async ({ page }) => {
	await page.getByLabel("English summary").fill("The pilot landed in a field.")
})

When("the safety officer chooses Translate to French", async ({ page }) => {
	await page.getByRole("button", { name: "Translate to French" }).click()
})

When("the safety officer keeps the current text", async ({ page }) => {
	await page.getByRole("dialog").getByRole("button", { name: "Keep current text" }).click()
})

When("the safety officer chooses Translate to French and accepts the translation", async ({ page }) => {
	await page.getByRole("button", { name: "Translate to French" }).click()
	await page.getByRole("dialog").getByRole("button", { name: "Use translation" }).click()
})

Then("the translate buttons offered are {}", async ({ page }, list: string) => {
	const group = page.getByRole("group", { name: "Translate" })
	if (list === "none") {
		await expect(group).toHaveCount(0)
		return
	}
	await expect(group.getByRole("button")).toHaveText(list.split(",").map((name) => name.trim()))
})

Then(
	"a confirmation shows the current French text and the proposed translation with their differences marked",
	async ({ page }) => {
		const dialog = page.getByRole("dialog", { name: "Replace the French text?" })
		await expect(dialog).toBeVisible()
		await expect(dialog.locator('[data-diff="current"]')).toContainText(DETAIL.summary.aiSummaryFr.split(" ")[0])
		await expect(dialog.locator('[data-diff="current"] del').first()).toBeVisible()
		await expect(dialog.locator('[data-diff="proposed"]')).toContainText("Traduction")
		await expect(dialog.locator('[data-diff="proposed"] ins').first()).toBeVisible()
	},
)

Then("the French text is unchanged", async ({ page }) => {
	await expect(page.getByRole("dialog")).toHaveCount(0)
	await expect(page.getByLabel("French summary")).toHaveValue(DETAIL.summary.aiSummaryFr)
})

Then("the French text is the proposed translation", async ({ page }) => {
	await expect(page.getByLabel("French summary")).toHaveValue(translatedFrom("The pilot landed firmly after the collapse."))
})

Then("the Translate to English button is not offered for it", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Translate to English" })).toHaveCount(0)
	await expect(page.getByRole("button", { name: "Translate to French" })).toBeVisible()
})

Then("the English text is labelled as edited by a reviewer", async ({ page }) => {
	await expect(page.locator('[data-source="en"]')).toHaveText("Written by a reviewer")
})

Then("the French text is labelled as machine-translated", async ({ page }) => {
	await expect(page.locator('[data-source="fr"]')).toHaveText("Machine-translated")
})

// ── A date, time, or yes/no answer in the reviewer's language (REQ-MOD-075, REQ-MOD-076) ──

const STORED_TYPE: Record<string, string> = { date: "date", time: "time", "yes/no": "yes_no", phone: "phone" }

Given(
	"a safety officer is signed in and a report with a {} answer stored as {string} exists",
	async ({ page }, type: string, stored: string) => {
		const detail = {
			...DETAIL,
			id: "reviewaaaaa",
			answers: [
				{
					questionKey: "occurred",
					labelEn: "When did it happen?",
					labelFr: "Quand est-ce arrivé?",
					type: STORED_TYPE[type],
					isPrivate: false,
					// A yes/no is a JSON boolean with no second language (ADR-0130). A
					// stub of any other type carries one, so the page is seen to hide it.
					values: [
						type === "yes/no"
							? { value: stored === "true", locale: "en-CA", translatedValue: null, translationSource: null }
							: { value: stored, locale: "en-CA", translatedValue: stored, translationSource: "auto" },
					],
				},
			],
		}
		await page.route(/\/api\/admin\/reports\/[^/?]+$/, (route) => route.fulfill({ json: detail }))
		await signInAs(page, "safety_officer")
	},
)

function occurredAnswer(page: Page) {
	return page.locator('[data-question-key="occurred"] dd')
}

Then("the answer reads {string}", async ({ page }, shown: string) => {
	await expect(occurredAnswer(page)).toHaveText(shown)
})

Then("no translation is shown beside it", async ({ page }) => {
	await expect(occurredAnswer(page)).not.toContainText(/Translation|Traduction/)
})

// REQ-MOD-078: a translation line only under an answer that has one (ADR-0112).

Then("a translated narrative answer shows its translation beneath it", async ({ page }) => {
	// A paragraph answer's translation is Markdown like the answer itself, under its own label.
	const translation = page.locator('[data-question-key="narrative"] [data-long-text-translation]')
	await expect(translation).toContainText("Translation:")
	await expect(translation).toContainText("Un atterrissage ferme synthétique.")
})

Then("a name or email answer shows no translation line", async ({ page }) => {
	await expect(page.locator('[data-question-key="pilot_name"]')).not.toContainText("Translation:")
	await expect(page.locator('[data-question-key="pilot_email"]')).toContainText("casey@example.test")
	await expect(page.locator('[data-question-key="pilot_email"]')).not.toContainText("Translation:")
})

// REQ-MOD-083: a published report's view links to its public address.
Then("the report view links to the report's public address", async ({ page }) => {
	await expect(page.getByRole("link", { name: "View the public page" })).toHaveAttribute("href", "/reports/reviewaaaaa")
})

Then("a report that is not published shows no such link", async ({ page }) => {
	// The later route wins, so the same report now reads back as pending.
	await stubReview(page, "pending", "pending")
	await page.reload()
	await expect(page.locator('[data-badge="status"]')).toBeVisible()
	await expect(page.locator("[data-public-link]")).toHaveCount(0)
})

// ── A multi-select answer listed as the form lists its choices (REQ-QB-153, ADR-0136) ──

Given(
	"a signed-in Safety Officer opens a report whose multi-select answer names {string}, {string} pinned last, and {string}",
	async ({ page }, first: string, last: string, second: string) => {
		const choice = (value: string, pin: string) => ({ value, locale: "en-CA", translatedValue: `${value} (fr)`, translationSource: "choice", pin })
		const detail = {
			...DETAIL,
			id: "choicesaaaa",
			answers: [
				{
					questionKey: "conditions",
					labelEn: "Which conditions applied?",
					labelFr: "Quelles conditions?",
					type: "multi_select",
					isPrivate: false,
					// In the order the reporter ticked them, which is not the form's order.
					values: [choice(first, "none"), choice(last, "last"), choice(second, "none")],
				},
			],
		}
		await page.route(/\/api\/admin\/reports\/[^/?]+$/, (route) => route.fulfill({ json: detail }))
		await signInAs(page, "safety_officer")
		await page.goto("/admin/reports/choicesaaaa")
	},
)

Then("the answer is listed {string}, {string}, {string}", async ({ page }, first: string, second: string, third: string) => {
	await expect(page.locator('[data-question-key="conditions"] dd > span:first-child')).toHaveText([first, second, third])
})


// ── Quick actions on each row of Manage reports (REQ-MOD-120..123) ──

Given("another reviewer has changed the pending report since the list was loaded", async ({ page }) => {
	listStubs.get(page)!.stale.add(ROW_BY_WORD.pending)
})

Then("the {word} row offers {}", async ({ page }, word: string, list: string) => {
	const expected = list.split(",").map((name) => name.trim())
	const buttons = rowButtons(page, word)
	await expect(buttons).toHaveCount(expected.length)
	for (const [index, name] of expected.entries()) {
		await expect(buttons.nth(index)).toHaveAccessibleName(name)
		await expect(buttons.nth(index)).toHaveAttribute("title", name)
	}
	await expect(row(page, word).getByRole("group")).toHaveAccessibleName(/^Actions for the report submitted /)
})

When("the safety officer publishes the pending row", async ({ page }) => {
	await rowButtons(page, "pending").and(page.getByRole("button", { name: "Publish", exact: true })).click()
})

When("the safety officer unpublishes the published row", async ({ page }) => {
	await rowButtons(page, "published").and(page.getByRole("button", { name: "Unpublish", exact: true })).click()
})

When("the safety officer chooses Delete on the pending row", async ({ page }) => {
	await rowButtons(page, "pending").and(page.getByRole("button", { name: "Delete", exact: true })).click()
})

When("the safety officer keeps the report", async ({ page }) => {
	await page.getByRole("dialog").getByRole("button", { name: "Keep report" }).click()
})

Then("the {word} row shows the {string} badge and offers {word}", async ({ page }, word: string, badge: string, action: string) => {
	await expect(row(page, word).locator('[data-badge="status"]')).toHaveText(badge)
	await expect(rowButtons(page, word).first()).toHaveAccessibleName(action)
})

Then("each row action sent the version its row was listed with", async ({ page }) => {
	expect(listStubs.get(page)!.sent).toEqual([
		{ method: "POST", path: "/api/admin/reports/pendingaaaa/publish", version: "11.1" },
		{ method: "POST", path: "/api/admin/reports/publishedaa/unpublish", version: "13.1" },
	])
})

Then("a confirmation asks whether to delete it", async ({ page }) => {
	await expect(page.getByRole("dialog", { name: "Delete this report?" })).toBeVisible()
	expect(listStubs.get(page)!.sent).toEqual([])
})

Then("the pending row is still listed and nothing was deleted", async ({ page }) => {
	await expect(page.getByRole("dialog")).toHaveCount(0)
	await expect(row(page, "pending")).toBeVisible()
	expect(listStubs.get(page)!.sent).toEqual([])
})

Then("the pending row is no longer listed and it was deleted", async ({ page }) => {
	await expect(row(page, "pending")).toHaveCount(0)
	expect(listStubs.get(page)!.sent).toEqual([{ method: "DELETE", path: "/api/admin/reports/pendingaaaa", version: undefined }])
})

Then("a message says the report changed and offers to reload the list", async ({ page }) => {
	await expect(page.getByRole("alert")).toContainText("Another reviewer changed this report since the list was loaded")
	await expect(page.getByRole("button", { name: "Reload list" })).toBeVisible()
})

Then("the pending row still shows the {string} badge", async ({ page }, badge: string) => {
	await expect(row(page, "pending").locator('[data-badge="status"]')).toHaveText(badge)
})

// ── Manage reports' own infinite scroll (issue no. 572, REQ-MOD-129, REQ-MOD-128) ──

const PAGE_TWO_CURSOR = "cGFnZS10d28"
const PAGE_THREE_CURSOR = "cGFnZS10aHJlZQ"
const NEWER = ROWS[0]
const OLDER_ROW = ROWS[1]

/**
 * Three pages: the second always has a further one so it never runs out of
 * "Load more" on its own; the third either loads empty or fails, per
 * `failThirdPage` — the fallback button and the retry it offers stay two
 * separate things to prove, not the same click.
 */
async function stubPagedReports(page: Page, failThirdPage: boolean) {
	await page.route(/\/api\/admin\/reports(\?.*)?$/, async (route) => {
		const after = new URL(route.request().url()).searchParams.get("after")

		if (after === PAGE_THREE_CURSOR) {
			if (failThirdPage) {
				return route.fulfill({ status: 500, body: "" })
			}
			return route.fulfill({ json: { items: [], next: null } })
		}

		if (after === PAGE_TWO_CURSOR) {
			return route.fulfill({ json: { items: [OLDER_ROW], next: PAGE_THREE_CURSOR } })
		}

		return route.fulfill({ json: { items: [NEWER], next: PAGE_TWO_CURSOR } })
	})
}

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

Given("a safety officer is signed in and more reports exist than fit on one page", async ({ page }) => {
	await disableAutoLoad(page)
	await stubPagedReports(page, false)
	await signInAs(page, "safety_officer")
	await page.goto("/admin/reports")
	await expect(row(page, "pending")).toBeVisible()
})

// --- REQ-MOD-179: opening Manage reports afresh never restores a list kept from earlier ---

const firstPageRequests = new WeakMap<Page, { count: number }>()

When(
	"the safety officer goes to another page and opens Manage reports again from the Admin menu",
	async ({ page }) => {
		await page.getByRole("contentinfo").getByRole("link", { name: "Contact", exact: true }).click()
		await expect(page).toHaveURL(/\/contact$/)

		// Counted only from here, so the first visit's own request does not count.
		const counter = { count: 0 }
		firstPageRequests.set(page, counter)
		page.on("request", (request) => {
			const url = new URL(request.url())
			if (url.pathname === "/api/admin/reports" && !url.searchParams.has("after")) counter.count += 1
		})

		await page.getByRole("button", { name: /^Admin/ }).click()
		await page.getByRole("menu", { name: "Admin" }).getByRole("menuitem", { name: /^Manage reports/ }).click()
		await expect(page).toHaveURL(/\/admin\/reports$/)
	},
)

Then("Manage reports asks for its first page again", async ({ page }) => {
	await expect.poll(() => firstPageRequests.get(page)?.count ?? 0).toBeGreaterThan(0)
	await expect(row(page, "pending")).toBeVisible()
})

Given("the next report page fails to load", async ({ page }) => {
	await stubPagedReports(page, true)
})

/**
 * Tabs to the named button, bounded rather than one fixed Tab count, since
 * it may already be visible (nothing to tab past) or still hidden behind
 * this row's own quick actions.
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

When("the safety officer activates the {string} action", async ({ page }, name: string) => {
	// The control is hidden until keyboard focus (ADR-0155); reach it the same
	// way a keyboard visitor would rather than force-clicking past that.
	await tabToButton(page, name)
	await page.keyboard.press("Enter")
})

Then("the older reports load without leaving Manage reports", async ({ page }) => {
	await expect(row(page, "private-unpublished")).toBeVisible()
	await expect(page).toHaveURL(/\/admin\/reports$/)
})

Then("the list offers a visible {string} action instead of failing silently", async ({ page }, name: string) => {
	await expect(page.getByRole("button", { name })).toBeVisible()
})
