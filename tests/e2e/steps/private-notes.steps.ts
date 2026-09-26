import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenario for the Private notes section of the report view
 * (REQ-MOD-106, ADR-0133).
 *
 * The private-note endpoints are stubbed at the network boundary and keep
 * their notes in memory, so what is asserted is what the page shows and asks
 * for. Who may keep notes, revisions, and soft deletion are proven against a
 * real database by the REQ-MOD-098..104 Reqnroll scenarios (ADR-0045).
 *
 * Registered after sign-in, so these routes win over the empty default in
 * auth.ts and over the review stub's catch-all for the report.
 *
 * Every note is synthetic.
 */

interface StubRevision {
	number: number
	text: string
	writtenBy: string
	writtenAt: string
	isMine: boolean
}

interface StubNote {
	id: string
	createdAt: string
	revisions: StubRevision[]
}

const ME = "dev:officer"
const OTHER = "auth0|reviewer-synthetic"

const notesByPage = new WeakMap<Page, StubNote[]>()

function view(note: StubNote) {
	const current = note.revisions[note.revisions.length - 1]
	return {
		id: note.id,
		text: current.text,
		revision: current.number,
		writtenBy: current.writtenBy,
		writtenAt: current.writtenAt,
		createdAt: note.createdAt,
		edited: note.revisions.length > 1,
		isMine: current.isMine,
	}
}

async function stubNotes(page: Page, notes: StubNote[]) {
	notesByPage.set(page, notes)
	let clock = Date.parse("2026-09-26T15:00:00Z")
	const tick = () => new Date((clock += 60_000)).toISOString()

	await page.route(/\/api\/admin\/reports\/[^/]+\/private-notes(\/.*)?$/, async (route) => {
		const request = route.request()
		const parts = new URL(request.url()).pathname.split("/private-notes")[1].split("/").filter(Boolean)
		const [noteId, tail] = parts
		const note = notes.find((candidate) => candidate.id === noteId)

		if (!noteId && request.method() === "GET") {
			return route.fulfill({ json: [...notes].reverse().map(view) })
		}

		if (!noteId && request.method() === "POST") {
			const at = tick()
			const added: StubNote = {
				id: `note${String(notes.length + 1).padStart(7, "a")}`,
				createdAt: at,
				revisions: [{ number: 1, text: request.postDataJSON().text, writtenBy: ME, writtenAt: at, isMine: true }],
			}
			notes.push(added)
			return route.fulfill({ status: 201, json: view(added) })
		}

		if (!note) {
			return route.fulfill({ status: 404, json: {} })
		}

		if (tail === "revisions") {
			return route.fulfill({ json: note.revisions })
		}

		if (request.method() === "PUT") {
			const body = request.postDataJSON()
			expect(body.revision).toBe(note.revisions.length)
			note.revisions.push({ number: note.revisions.length + 1, text: body.text, writtenBy: ME, writtenAt: tick(), isMine: true })
			return route.fulfill({ json: view(note) })
		}

		notes.splice(notes.indexOf(note), 1)
		return route.fulfill({ status: 204 })
	})
}

function section(page: Page) {
	return page.getByRole("region", { name: "Private notes" })
}

function noteWith(page: Page, text: string) {
	return section(page).getByRole("list", { name: "Private notes on this report" }).getByRole("listitem").filter({
		has: page.locator("[data-private-note-text]", { hasText: text }),
	})
}

Given("another reviewer left the private note {string}", async ({ page }, text: string) => {
	await stubNotes(page, [
		{
			id: "noteothera1",
			createdAt: "2026-09-25T09:30:00Z",
			revisions: [{ number: 1, text, writtenBy: OTHER, writtenAt: "2026-09-25T09:30:00Z", isMine: false }],
		},
	])
})

Then("the private notes section lists {string} with its writer and time", async ({ page }, text: string) => {
	const note = noteWith(page, text)
	await expect(note).toHaveCount(1)
	await expect(note.locator("[data-private-note-writer]")).toHaveText(OTHER)
	await expect(note).toContainText("2026")
	await expect(note.locator("[data-private-note-edited]")).toHaveCount(0)
})

When("the safety officer adds the private note {string}", async ({ page }, text: string) => {
	await section(page).getByLabel("Add a private note").fill(text)
	await section(page).getByRole("button", { name: "Add note" }).click()
})

Then("{string} is listed first, marked as theirs", async ({ page }, text: string) => {
	const items = section(page).getByRole("list", { name: "Private notes on this report" }).getByRole("listitem")
	await expect(items).toHaveCount(2)
	await expect(items.first().locator("[data-private-note-text]")).toHaveText(text)
	await expect(items.first().locator("[data-private-note-writer]")).toHaveText("You")
	await expect(section(page).getByLabel("Add a private note")).toHaveValue("")
})

When(
	"the safety officer edits that private note to {string}",
	async ({ page }, text: string) => {
		const note = section(page).getByRole("list", { name: "Private notes on this report" }).getByRole("listitem").first()
		await note.getByRole("button", { name: "Edit" }).click()
		await note.getByLabel("Edit this note").fill(text)
		await note.getByRole("button", { name: "Save" }).click()
	},
)

Then("that private note reads {string} and is marked as edited", async ({ page }, text: string) => {
	const note = noteWith(page, text)
	await expect(note).toHaveCount(1)
	await expect(note.locator("[data-private-note-edited]")).toHaveText("Edited")
})

Then("its history shows both revisions", async ({ page }) => {
	const note = section(page).getByRole("list", { name: "Private notes on this report" }).getByRole("listitem").first()
	await note.getByRole("button", { name: "History" }).click()

	const history = note.getByRole("list", { name: "Every version of this note" }).getByRole("listitem")
	await expect(history).toHaveCount(2)
	await expect(history.nth(0)).toContainText("Version 1 — You")
	await expect(history.nth(0)).toContainText("Investigator report requested.")
	await expect(history.nth(1)).toContainText("Version 2 — You")
	await expect(history.nth(1)).toContainText("Investigator report received.")
})

When("the safety officer removes that private note and confirms", async ({ page }) => {
	const note = section(page).getByRole("list", { name: "Private notes on this report" }).getByRole("listitem").first()
	await note.getByRole("button", { name: "Remove" }).click()
	await expect(note).toContainText("Remove this note? It cannot be restored.")
	await note.getByRole("button", { name: "Remove" }).click()
})

Then("{string} is no longer listed", async ({ page }, text: string) => {
	await expect(noteWith(page, text)).toHaveCount(0)
	await expect(noteWith(page, "Called the pilot; follow up Monday.")).toHaveCount(1)
	expect(notesByPage.get(page)).toHaveLength(1)
})
