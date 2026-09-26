import { createBdd } from "playwright-bdd"
import { expect, type Download, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the Private attachments section of the report view,
 * and a private note that refers to one (REQ-MOD-115, REQ-MOD-116, ADR-0135).
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

const attachmentsByPage = new WeakMap<Page, StubAttachment[]>()
const downloadsByPage = new WeakMap<Page, Promise<Download>>()
const slowStorage = new WeakSet<Page>()
const erasedByPage = new WeakMap<Page, string[]>()
const putsByPage = new WeakMap<Page, { contentType: string | null; size: number }[]>()

async function stubAttachments(page: Page, attachments: StubAttachment[]) {
	attachmentsByPage.set(page, attachments)
	const puts: { contentType: string | null; size: number }[] = []
	putsByPage.set(page, puts)
	const pending = new Map<string, number>()
	let sequence = 0

	await page.route(/\/api\/admin\/reports\/[^/]+\/private-attachments(\/.*)?$/, async (route) => {
		const request = route.request()
		const [first, tail] = new URL(request.url()).pathname.split("/private-attachments")[1].split("/").filter(Boolean)

		if (first === "uploads") {
			const body = request.postDataJSON()
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
			const body = request.postDataJSON()
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
			// Never answers: the upload stays in flight until the page cancels it.
			return
		}

		if (request.method() === "PUT") {
			puts.push({ contentType: request.headers()["content-type"] ?? null, size: request.postDataBuffer()?.length ?? 0 })
			return route.fulfill({ status: 200, body: "" })
		}

		const id = request.url().split("/").pop()
		const attachment = attachments.find((candidate) => candidate.id === id)!
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

function section(page: Page) {
	return page.getByRole("region", { name: "Private attachments" })
}

function attachmentNamed(page: Page, fileName: string) {
	return section(page).getByRole("list", { name: "Private attachments on this report" }).getByRole("listitem").filter({
		has: page.locator("[data-private-attachment-name]", { hasText: fileName }),
	})
}

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
			const attachment = attachmentsByPage.get(page)!.find((candidate) => candidate.id === note.attachmentId)
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

		const body = request.postDataJSON()
		const note = { id: `note${String(notes.length + 1).padStart(7, "a")}`, text: body.text, attachmentId: body.attachmentId }
		notes.push(note)
		return route.fulfill({ status: 201, json: view(note) })
	})
})

When(
	"the safety officer adds the private attachment {string} with the description {string}",
	async ({ page }, fileName: string, description: string) => {
		if (!attachmentsByPage.has(page)) await stubAttachments(page, [])

		await section(page).getByLabel("Add a private attachment").setInputFiles({
			name: fileName,
			mimeType: "application/zip",
			buffer: Buffer.from(CONTENT),
		})
		await section(page).getByLabel("Description (optional)").fill(description)
		await section(page).getByRole("button", { name: "Add attachment" }).click()
	},
)

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
		const puts = putsByPage.get(page)!
		expect(puts).toHaveLength(1)
		expect(puts[0]).toEqual({ contentType: "application/zip", size: CONTENT.length })
		await expect(section(page).getByLabel("Description (optional)")).toHaveValue("")
	},
)

When("the safety officer downloads the private attachment {string}", async ({ page }, fileName: string) => {
	downloadsByPage.set(page, page.waitForEvent("download"))
	await attachmentNamed(page, fileName).getByRole("button", { name: "Download" }).click()
})

Then("the browser saves a file named {string}", async ({ page }, fileName: string) => {
	const download = await downloadsByPage.get(page)!
	expect(download.suggestedFilename()).toBe(fileName)
})

When(
	"the safety officer removes the private attachment {string} and confirms",
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
	"the safety officer adds the private note {string} referring to {string}",
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

	// A minted upload the page gives up on is erased through the reporter upload route.
	const erased: string[] = []
	erasedByPage.set(page, erased)
	await page.route(/\/api\/v1\/uploads\/[^/]+$/, (route) => {
		erased.push(route.request().url().split("/").pop()!)
		return route.fulfill({ status: 204, body: "" })
	})
})

Then("the private attachments section shows the upload's progress and offers to cancel it", async ({ page }) => {
	await expect(section(page).getByRole("progressbar", { name: "Upload progress" })).toBeVisible()
	await expect(section(page).getByRole("button", { name: "Cancel upload" })).toBeVisible()
	await expect(section(page).getByRole("button", { name: "Add attachment" })).toBeDisabled()
})

When("the safety officer cancels the upload", async ({ page }) => {
	await section(page).getByRole("button", { name: "Cancel upload" }).click()
})

Then("the private attachments section says the upload was cancelled and lists no attachments", async ({ page }) => {
	await expect(section(page).getByRole("alert")).toHaveText("The upload was cancelled.")
	await expect(section(page).getByRole("progressbar")).toHaveCount(0)
	await expect(section(page).locator("[data-private-attachments-empty]")).toBeVisible()
	expect(attachmentsByPage.get(page)).toHaveLength(0)
})

Then("the cancelled upload is erased", async ({ page }) => {
	await expect.poll(() => erasedByPage.get(page)!.length).toBe(1)
})
