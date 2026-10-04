import { createBdd } from "playwright-bdd"
import { expect, type Dialog, type Download, type Page } from "@playwright/test"
import { present } from "./present"
import { tryToReload } from "./unload"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the Private attachments section of the report view —
 * its staging area (issue #658: drop or choose several files, each uploads on
 * staging, its own description, "Add N attachments"), and a private note that
 * refers to an already-added attachment (REQ-MOD-115..117, REQ-MOD-173..177,
 * REQ-MOD-180..181, REQ-MED-071..082, ADR-0135).
 *
 * The private-attachment endpoints, and the storage URLs they hand out, are
 * stubbed at the network boundary and keep their files in memory, so what is
 * asserted is what the page shows, sends, and saves. Who may use them, what is
 * stored, and that bytes arrive unchanged are proven against a real database
 * and S3-compatible server by the REQ-MED-046..052 and REQ-MOD-107..114
 * Reqnroll scenarios (ADR-0045).
 *
 * Registered after sign-in, so these routes win over the empty default in
 * auth.ts and over the review stub's catch-all for the report.
 *
 * Every file is synthetic.
 */

interface StubAttachment {
	id: string
	fileName: string
	contentType: string
	byteSize: number
	description: string | null
	addedBy: string
	addedAt: string
	isMine: boolean
}

interface StubNote {
	id: string
	text: string
	attachmentId: string | null
}

const ME = "dev:officer"
const STORAGE = "/__storage-stub"
const CONTENT = "Synthetic private attachment content."
// HpacSafety:Media:PrivateAttachments:MaxByteSize's default (ADR-0135); faked
// onto a synthetic File's `size` rather than actually built, so the too-large
// scenario stays fast.
const PRIVATE_CAP_BYTES = 1024 * 1024 * 1024

const attachmentsByPage = new WeakMap<Page, StubAttachment[]>()
const downloadsByPage = new WeakMap<Page, Promise<Download>>()
const slowStorage = new WeakSet<Page>()
const pendingPutsByPage = new WeakMap<Page, (() => void)[]>()
const slowClaim = new WeakSet<Page>()
const pendingClaimsByPage = new WeakMap<Page, (() => void)[]>()
const erasedByPage = new WeakMap<Page, string[]>()
const putsByPage = new WeakMap<Page, { contentType: string | null; size: number }[]>()

async function stubAttachments(page: Page, attachments: StubAttachment[]) {
	attachmentsByPage.set(page, attachments)
	const puts: { contentType: string | null; size: number }[] = []
	putsByPage.set(page, puts)
	const erased: string[] = []
	erasedByPage.set(page, erased)
	pendingPutsByPage.set(page, [])
	const pending = new Map<string, number>()
	let sequence = 0

	// A minted-but-unclaimed upload is erased through the reporter upload
	// route; private uploads share the same quarantine (ADR-0126).
	await page.route(/\/api\/v1\/uploads\/[^/]+$/, (route) => {
		erased.push(present(route.request().url().split("/").pop()))
		return route.fulfill({ status: 204, body: "" })
	})

	await page.route(/\/api\/admin\/reports\/[^/]+\/private-attachments(\/.*)?$/, async (route) => {
		const request = route.request()
		const [first, tail] = new URL(request.url()).pathname.split("/private-attachments")[1].split("/").filter(Boolean)

		if (first === "uploads") {
			const body = request.postDataJSON() as { byteSize: number; contentType?: string }
			const uploadId = `upload${String((sequence += 1)).padStart(16, "a")}`
			pending.set(uploadId, body.byteSize)
			return route.fulfill({
				status: 201,
				json: {
					uploadId,
					contentType: body.contentType || "application/octet-stream",
					uploadUrl: `${new URL(request.url()).origin}${STORAGE}/put/${uploadId}`,
					expiresAt: "2026-09-26T16:15:00Z",
				},
			})
		}

		if (!first && request.method() === "GET") {
			return route.fulfill({ json: [...attachments].reverse() })
		}

		if (!first && request.method() === "POST") {
			if (slowClaim.has(page)) {
				// Held open until a step releases it.
				await new Promise<void>((resolve) => {
					const waiting = pendingClaimsByPage.get(page) ?? []
					waiting.push(resolve)
					pendingClaimsByPage.set(page, waiting)
				})
			}
			const body = request.postDataJSON() as { fileName: string; uploadId: string; description: string | null }
			const added: StubAttachment = {
				id: `attach${String(attachments.length + 1).padStart(5, "a")}`,
				fileName: body.fileName,
				contentType: "application/zip",
				byteSize: pending.get(body.uploadId) ?? 0,
				description: body.description,
				addedBy: ME,
				addedAt: "2026-09-26T16:00:00Z",
				isMine: true,
			}
			attachments.push(added)
			return route.fulfill({ status: 201, json: added })
		}

		const attachment = attachments.find((candidate) => candidate.id === first)
		if (!attachment) return route.fulfill({ status: 404, json: {} })

		if (tail === "download") {
			return route.fulfill({
				json: {
					url: `${new URL(request.url()).origin}${STORAGE}/get/${attachment.id}`,
					expiresAt: "2026-09-26T16:15:00Z",
					fileName: attachment.fileName,
				},
			})
		}

		attachments.splice(attachments.indexOf(attachment), 1)
		return route.fulfill({ status: 204 })
	})

	// Storage: the browser PUTs straight to it, and a download link forces a save.
	await page.route(new RegExp(`${STORAGE}/(put|get)/`), async (route) => {
		const request = route.request()
		if (request.method() === "PUT" && slowStorage.has(page)) {
			// Held open until a step releases it (or the page aborts it).
			await new Promise<void>((resolve) => {
				const waiting = pendingPutsByPage.get(page) ?? []
				waiting.push(resolve)
				pendingPutsByPage.set(page, waiting)
			})
		}

		if (request.method() === "PUT") {
			puts.push({ contentType: request.headers()["content-type"] ?? null, size: request.postDataBuffer()?.length ?? 0 })
			return route.fulfill({ status: 200, body: "" }).catch(() => {}) // The browser may already have cancelled it.
		}

		const id = request.url().split("/").pop()
		const attachment = present(attachments.find((candidate) => candidate.id === id))
		return route.fulfill({
			status: 200,
			headers: {
				"Content-Type": attachment.contentType,
				"Content-Disposition": `attachment; filename="${attachment.fileName}"`,
			},
			body: CONTENT,
		})
	})
}

function releasePendingPuts(page: Page) {
	const waiting = pendingPutsByPage.get(page) ?? []
	pendingPutsByPage.set(page, [])
	for (const resolve of waiting) resolve()
}

function section(page: Page) {
	return page.getByRole("region", { name: "Private attachments" })
}

function attachmentNamed(page: Page, fileName: string) {
	return section(page).getByRole("list", { name: "Private attachments on this report" }).getByRole("listitem").filter({
		has: page.locator("[data-private-attachment-name]", { hasText: fileName }),
	})
}

function stagingList(page: Page) {
	return section(page).getByRole("list", { name: "Files being added" })
}

function stagedRow(page: Page, fileName: string) {
	return stagingList(page).getByRole("listitem").filter({ hasText: fileName })
}

function addAllButton(page: Page) {
	return section(page).getByRole("button", { name: /^Add \d+ attachments?$/ })
}

/**
 * Opens the pending report the review stub serves, as manage-reports.steps.ts's
 * "the Safety Officer opens that report" does, for a Given that starts there.
 */
async function openThatReport(page: Page) {
	await page.goto("/admin/reports/reviewaaaaa")
	await expect(page.getByRole("heading", { level: 1, name: /^(Report|Signalement)$/ })).toBeVisible()
	await expect(page.locator('[data-badge="status"]')).toBeVisible()
}

async function stage(page: Page, fileName: string) {
	if (!attachmentsByPage.has(page)) await stubAttachments(page, [])
	await section(page).getByLabel("Add a private attachment").setInputFiles({
		name: fileName,
		mimeType: "application/zip",
		buffer: Buffer.from(CONTENT),
	})
}

async function expectFinishedUploading(page: Page, fileName: string) {
	const row = stagedRow(page, fileName)
	await expect(row.getByLabel("Description (optional)")).toBeVisible()
	await expect(row.getByRole("progressbar")).toHaveCount(0)
}

Given("the Safety Officer has that report open", async ({ page }) => {
	await openThatReport(page)
})

Given(
	"the Safety Officer has staged the private attachment {string} on that report and described it as {string}",
	async ({ page }, fileName: string, description: string) => {
		await openThatReport(page)
		await stage(page, fileName)
		await expectFinishedUploading(page, fileName)
		await stagedRow(page, fileName).getByLabel("Description (optional)").fill(description)
	},
)

Given("the Safety Officer has added the private attachment {string} to that report", async ({ page }, fileName: string) => {
	await stubAttachments(page, [
		{
			id: "attachmine1",
			fileName,
			contentType: "application/zip",
			byteSize: CONTENT.length,
			description: "Received from the coroner",
			addedBy: ME,
			addedAt: "2026-09-26T16:00:00Z",
			isMine: true,
		},
	])
	await openThatReport(page)
	await expect(attachmentNamed(page, fileName)).toHaveCount(1)
})

Given(
	"the Safety Officer has staged the private attachment {string} on that report, still uploading",
	async ({ page }, fileName: string) => {
		await openThatReport(page)
		await stage(page, fileName)
		await expect(stagedRow(page, fileName).getByRole("progressbar")).toBeVisible()
		// Storage holds the PUT open; wait until it has arrived, so a step that
		// releases it has something to release.
		await expect.poll(() => (pendingPutsByPage.get(page) ?? []).length).toBe(1)
	},
)

Given(
	"the Safety Officer has staged the private attachment {string} on that report, finished uploading",
	async ({ page }, fileName: string) => {
		await openThatReport(page)
		await stage(page, fileName)
		await expectFinishedUploading(page, fileName)
	},
)

Given("the Safety Officer is asked whether to leave after following a link away from the report", async ({ page }) => {
	await page.getByRole("link", { name: "Back to reports" }).click()
	await expect(page.getByRole("dialog", { name: "Leave without saving?" })).toBeVisible()
})

Given(
	"the Safety Officer is adding the staged private attachment {string} to that report",
	async ({ page }, fileName: string) => {
		await openThatReport(page)
		await stage(page, fileName)
		await expectFinishedUploading(page, fileName)
		await addAllButton(page).click()
		// The report holds the request open until a step releases it.
		await expect.poll(() => (pendingClaimsByPage.get(page) ?? []).length).toBe(1)
	},
)

Given("the report carries the private attachment {string}", async ({ page }, fileName: string) => {
	await stubAttachments(page, [
		{
			id: "attachother",
			fileName,
			contentType: "application/pdf",
			byteSize: 2048,
			description: null,
			addedBy: "auth0|reviewer-synthetic",
			addedAt: "2026-09-25T09:30:00Z",
			isMine: false,
		},
	])

	// The note it is referred to by, kept in memory like the notes stub.
	const notes: StubNote[] = []
	await page.route(/\/api\/admin\/reports\/[^/]+\/private-notes$/, async (route) => {
		const request = route.request()
		const view = (note: StubNote) => {
			const attachment = present(attachmentsByPage.get(page)).find((candidate) => candidate.id === note.attachmentId)
			return {
				id: note.id,
				text: note.text,
				revision: 1,
				writtenBy: ME,
				writtenAt: "2026-09-26T16:05:00Z",
				createdAt: "2026-09-26T16:05:00Z",
				edited: false,
				isMine: true,
				attachment: attachment ? { id: attachment.id, fileName: attachment.fileName, removed: false } : null,
			}
		}

		if (request.method() === "GET") return route.fulfill({ json: [...notes].reverse().map(view) })

		const body = request.postDataJSON() as { text: string; attachmentId: string | null }
		const note = { id: `note${String(notes.length + 1).padStart(7, "a")}`, text: body.text, attachmentId: body.attachmentId }
		notes.push(note)
		return route.fulfill({ status: 201, json: view(note) })
	})
})

// --- Staging one file (REQ-MOD-115, REQ-MOD-117, REQ-MED-071..074) ---

When("the Safety Officer stages the private attachment {string}", async ({ page }, fileName: string) => {
	await stage(page, fileName)
})

Then("the staged attachment {string} finishes uploading and offers a description box", async ({ page }, fileName: string) => {
	await expectFinishedUploading(page, fileName)
})

When("the Safety Officer adds the staged private attachments", async ({ page }) => {
	await addAllButton(page).click()
})

Then(
	"the private attachments section lists {string} with its description, its adder, and when it was added",
	async ({ page }, fileName: string) => {
		const item = attachmentNamed(page, fileName)
		await expect(item).toHaveCount(1)
		await expect(item.locator("[data-private-attachment-description]")).toHaveText("Received from the coroner")
		await expect(item.locator("[data-private-attachment-added]")).toContainText("Added by You")
		await expect(item.locator("[data-private-attachment-added]")).toContainText("2026")
		await expect(item.locator("[data-private-attachment-size]")).not.toBeEmpty()

		// The file went straight to storage, once, under the type the mint signed.
		const puts = present(putsByPage.get(page))
		expect(puts).toHaveLength(1)
		expect(puts[0]).toEqual({ contentType: "application/zip", size: CONTENT.length })
		// Added: gone from the staging list entirely, not just cleared.
		await expect(stagingList(page)).toHaveCount(0)
	},
)

When("the Safety Officer downloads the private attachment {string}", async ({ page }, fileName: string) => {
	downloadsByPage.set(page, page.waitForEvent("download"))
	await attachmentNamed(page, fileName).getByRole("button", { name: "Download" }).click()
})

Then("the browser saves a file named {string}", async ({ page }, fileName: string) => {
	const download = await present(downloadsByPage.get(page))
	expect(download.suggestedFilename()).toBe(fileName)
})

When(
	"the Safety Officer removes the private attachment {string} and confirms",
	async ({ page }, fileName: string) => {
		const item = attachmentNamed(page, fileName)
		await item.getByRole("button", { name: "Remove" }).click()
		await expect(item).toContainText("Remove this attachment? It cannot be restored.")
		await item.getByRole("button", { name: "Remove" }).click()
	},
)

Then("the private attachments section lists no attachments", async ({ page }) => {
	await expect(section(page).locator("[data-private-attachments-empty]")).toHaveText("No private attachments yet.")
	expect(attachmentsByPage.get(page)).toHaveLength(0)
})

When(
	"the Safety Officer adds the private note {string} referring to {string}",
	async ({ page }, text: string, fileName: string) => {
		const notes = page.getByRole("region", { name: "Private notes" })
		await notes.getByLabel("Add a private note").fill(text)
		await notes.getByLabel("Refers to a private attachment (optional)").selectOption({ label: fileName })
		await notes.getByRole("button", { name: "Add note" }).click()
	},
)

Then("that private note shows that it refers to {string}", async ({ page }, fileName: string) => {
	const note = page.getByRole("region", { name: "Private notes" }).getByRole("list", { name: "Private notes on this report" }).getByRole("listitem").first()
	await expect(note.locator("[data-private-note-attachment]")).toContainText("Refers to")
	await expect(note.locator("[data-private-note-attachment]").getByRole("button", { name: fileName })).toBeVisible()
})

Given("storage is slow to accept a private attachment", async ({ page }) => {
	slowStorage.add(page)
	await stubAttachments(page, [])
})

Then(
	"the staged attachment {string} shows its upload progress, offers to cancel it, and {string} stays disabled",
	async ({ page }, fileName: string, buttonLabel: string) => {
		const row = stagedRow(page, fileName)
		await expect(row.getByRole("progressbar")).toBeVisible()
		await expect(row.getByRole("button", { name: `Cancel uploading ${fileName}` })).toBeVisible()
		await expect(section(page).getByRole("button", { name: buttonLabel })).toBeDisabled()
	},
)

When("the Safety Officer cancels the staged upload {string}", async ({ page }, fileName: string) => {
	await stagedRow(page, fileName).getByRole("button", { name: `Cancel uploading ${fileName}` }).click()
})

Then("the staged attachment {string} is gone from the staging list", async ({ page }, fileName: string) => {
	await expect(stagedRow(page, fileName)).toHaveCount(0)
})

Then("the cancelled upload is erased", async ({ page }) => {
	await expect.poll(() => present(erasedByPage.get(page)).length).toBe(1)
})

// --- Several files staged at once (REQ-MOD-173, REQ-MOD-174, REQ-MED-075, REQ-MED-076) ---

/** Builds a DataTransfer carrying synthetic files in the page, as a real drag would. */
async function filesTransfer(page: Page, names: string[]) {
	return page.evaluateHandle((fileNames) => {
		const transfer = new DataTransfer()
		for (const name of fileNames) transfer.items.add(new File([`synthetic ${name}`], name, { type: "application/octet-stream" }))
		return transfer
	}, names)
}

async function dropOnZone(page: Page, ...names: string[]) {
	const dataTransfer = await filesTransfer(page, names)
	const zone = section(page).getByTestId("attachment-drop-zone")
	for (const type of ["dragenter", "dragover", "drop"]) await zone.dispatchEvent(type, { dataTransfer })
}

When(
	/^the Safety Officer (drops|chooses, through the picker,) the private attachments "([^"]+)" and "([^"]+)" at once$/,
	async ({ page }, method: string, first: string, second: string) => {
		await stageTwo(page, method === "drops", first, second)
	},
)

/** Stages two files at once, dropped on the zone or chosen through the picker. */
async function stageTwo(page: Page, dropped: boolean, first: string, second: string) {
	if (!attachmentsByPage.has(page)) await stubAttachments(page, [])
	if (dropped) {
		await dropOnZone(page, first, second)
	} else {
		await section(page).getByLabel("Add a private attachment").setInputFiles([
			{ name: first, mimeType: "application/octet-stream", buffer: Buffer.from(`synthetic ${first}`) },
			{ name: second, mimeType: "application/octet-stream", buffer: Buffer.from(`synthetic ${second}`) },
		])
	}
}

async function expectBothFinishedUploading(page: Page) {
	await expect(stagingList(page).getByLabel("Description (optional)")).toHaveCount(2)
	await expect(stagingList(page).getByRole("progressbar")).toHaveCount(0)
}

Then("both staged attachments finish uploading independently, each with its own progress", async ({ page }) => {
	await expectBothFinishedUploading(page)
})

async function dropBoth(page: Page, first: string, second: string, dropped = true) {
	await openThatReport(page)
	await stageTwo(page, dropped, first, second)
	await expectBothFinishedUploading(page)
}

Given(
	/^the Safety Officer has (dropped|chosen, through the picker,) "([^"]+)" described as "([^"]+)" and "([^"]+)" described as "([^"]+)" on that report$/,
	async ({ page }, staged: string, first: string, firstDescription: string, second: string, secondDescription: string) => {
		await dropBoth(page, first, second, staged === "dropped")
		await stagedRow(page, first).getByLabel("Description (optional)").fill(firstDescription)
		await stagedRow(page, second).getByLabel("Description (optional)").fill(secondDescription)
	},
)

Given(
	"the Safety Officer has dropped the private attachments {string} and {string} on that report at once, both finished uploading",
	async ({ page }, first: string, second: string) => {
		await dropBoth(page, first, second)
	},
)

Given(
	"the Safety Officer has dropped the private attachments {string} and {string} on that report at once, then removed the staged {string}",
	async ({ page }, first: string, second: string, removed: string) => {
		await dropBoth(page, first, second)
		await stagedRow(page, removed).getByRole("button", { name: `Remove ${removed}` }).click()
		await expect(stagedRow(page, removed)).toHaveCount(0)
	},
)

Then(
	"the private attachments section lists {string} and {string}, each with its own description",
	async ({ page }, first: string, second: string) => {
		for (const name of [first, second]) {
			const item = attachmentNamed(page, name)
			await expect(item).toHaveCount(1)
			await expect(item.locator("[data-private-attachment-description]")).not.toBeEmpty()
		}
	},
)

When("the Safety Officer removes the staged attachment {string}", async ({ page }, fileName: string) => {
	await stagedRow(page, fileName).getByRole("button", { name: `Remove ${fileName}` }).click()
})

Then(
	"only {string} remains in the staging list, and nothing erases the upload for {string}",
	async ({ page }, keep: string, removed: string) => {
		await expect(stagedRow(page, keep)).toHaveCount(1)
		await expect(stagedRow(page, removed)).toHaveCount(0)
		expect(erasedByPage.get(page)).toEqual([])
	},
)

Then("the private attachments section lists {string} only", async ({ page }, fileName: string) => {
	await expect(attachmentNamed(page, fileName)).toHaveCount(1)
	await expect(section(page).getByRole("list", { name: "Private attachments on this report" }).getByRole("listitem")).toHaveCount(1)
})

// --- A too-large file among several (REQ-MOD-175) ---

/** Drops synthetic files, the oversized one faking a size just past the private cap. */
async function dropWithOversized(page: Page, withOrdinary: boolean) {
	if (!attachmentsByPage.has(page)) await stubAttachments(page, [])
	const dataTransfer = await page.evaluateHandle(
		({ capBytes, ordinary }) => {
			const transfer = new DataTransfer()
			if (ordinary) transfer.items.add(new File(["synthetic ordinary content"], "ordinary.pdf", { type: "application/pdf" }))
			const oversized = new File(["synthetic oversized content"], "oversized.zip", { type: "application/zip" })
			Object.defineProperty(oversized, "size", { value: capBytes + 1 })
			transfer.items.add(oversized)
			return transfer
		},
		{ capBytes: PRIVATE_CAP_BYTES, ordinary: withOrdinary },
	)
	const zone = section(page).getByTestId("attachment-drop-zone")
	for (const type of ["dragenter", "dragover", "drop"]) await zone.dispatchEvent(type, { dataTransfer })
}

When("the Safety Officer drops one ordinary private attachment and one larger than the private cap, at once", async ({ page }) => {
	await dropWithOversized(page, true)
})

Given(
	"the Safety Officer has dropped one ordinary private attachment and one larger than the private cap on that report at once, the ordinary one finished uploading",
	async ({ page }) => {
		await openThatReport(page)
		await dropWithOversized(page, true)
		await expect(stagedRow(page, "ordinary.pdf").getByLabel("Description (optional)")).toBeVisible()
	},
)

Then("the too-large attachment's staged row states the private cap and cannot be added", async ({ page }) => {
	const row = stagedRow(page, "oversized.zip")
	await expect(row.getByRole("alert")).toContainText("larger than")
	await expect(row.getByLabel("Description (optional)")).toHaveCount(0)
})

Then("the ordinary attachment finishes uploading and offers a description box", async ({ page }) => {
	await expect(stagedRow(page, "ordinary.pdf").getByLabel("Description (optional)")).toBeVisible()
})

Then("the private attachments section lists only the ordinary attachment", async ({ page }) => {
	await expect(attachmentNamed(page, "ordinary.pdf")).toHaveCount(1)
	await expect(section(page).getByRole("list", { name: "Private attachments on this report" }).getByRole("listitem")).toHaveCount(1)
})

// --- "Add N attachments" disabled until settled (REQ-MOD-176) ---

Then("{string} stays disabled while {string} uploads", async ({ page }, buttonLabel: string, fileName: string) => {
	await expect(section(page).getByRole("button", { name: buttonLabel })).toBeDisabled()
	await expect(stagedRow(page, fileName).getByRole("progressbar")).toBeVisible()
})

When("storage finishes accepting the staged upload", ({ page }) => {
	releasePendingPuts(page)
})

Then("{string} becomes enabled", async ({ page }, buttonLabel: string) => {
	await expect(section(page).getByRole("button", { name: buttonLabel })).toBeEnabled()
})

// --- Leaving with staged, un-added uploads warns (REQ-MOD-177, REQ-MED-079..081), through the
// shared useUnsavedChangesGuard (issue 659) rather than this page's own
// mechanism — see unsaved-changes.steps.ts for the beforeunload and
// bilingual-dialog steps this scenario reuses. ---

When("the Safety Officer tries to close or reload the tab", ({ page }) => {
	tryToReload(page)
})

When("the Safety Officer navigates away from the report through a link", async ({ page }) => {
	await page.getByRole("link", { name: "Back to reports" }).click()
})

When("they keep the page", async ({ page }) => {
	await page.getByRole("dialog", { name: "Leave without saving?" }).getByRole("button", { name: "Stay" }).click()
})

Then("the Safety Officer stays on the report page", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports\/[^/]+$/)
})

Then("the Safety Officer leaves the report page", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports$/)
})

// --- Rows locked while adding (REQ-MOD-180) ---

Given("the report is slow to accept a private attachment", async ({ page }) => {
	slowClaim.add(page)
	await stubAttachments(page, [])
})

Then(
	"the staged attachment {string} can be neither removed nor re-described while it is added",
	async ({ page }, fileName: string) => {
		const row = stagedRow(page, fileName)
		await expect(row.getByRole("button", { name: `Remove ${fileName}` })).toBeDisabled()
		await expect(row.getByLabel("Description (optional)")).toBeDisabled()
		await expect(addAllButton(page)).toBeDisabled()
	},
)

When("the report finishes accepting the private attachment", ({ page }) => {
	const waiting = pendingClaimsByPage.get(page) ?? []
	pendingClaimsByPage.set(page, [])
	for (const resolve of waiting) resolve()
})

// --- Only refused files staged: no leave warning (REQ-MOD-181) ---

Given(
	"the Safety Officer has dropped only a private attachment larger than the private cap on that report, its row refused",
	async ({ page }) => {
		await openThatReport(page)
		await dropWithOversized(page, false)
		await expect(stagedRow(page, "oversized.zip").getByRole("alert")).toContainText("larger than")
	},
)

When("the Safety Officer reloads the report page", async ({ page }) => {
	const dialogs: Dialog[] = []
	page.on("dialog", (dialog) => {
		dialogs.push(dialog)
		void dialog.dismiss()
	})
	await page.reload()
	expect(dialogs).toEqual([])
})

Then("the page reloads without warning, and the refused row is gone", async ({ page }) => {
	await expect(section(page).getByTestId("attachment-drop-zone")).toBeVisible()
	await expect(stagingList(page)).toHaveCount(0)
})
