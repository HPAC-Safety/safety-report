import { createBdd } from "playwright-bdd"
import { expect, type Page, type Route } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import {
	SAVED_UPLOAD,
	defaultFormQuestions,
	readDraftFromBrowser,
	stubCurrentQuestions,
	writeSavedDraftToBrowser,
	writeStaleDraftToBrowser,
} from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for uploading attachments as they are attached (ADR-0096,
 * REQ-SUB-045..051), for the drop zone that chooses them (REQ-SUB-058..062),
 * and for keeping them as long as the saved report (ADR-0100,
 * REQ-SUB-063..067). The API is stubbed at the network boundary: an upload is
 * held open until a step releases it, so "still uploading" is a state a step
 * can observe rather than a race. The server side of the same contract is
 * HpacSafety.Api.Tests' UploadEndpointTests and the acceptance steps. Every
 * file here is synthetic.
 */

interface UploadStub {
	/** Upload ids handed out, in order. */
	issued: string[]
	/** Ids the browser asked to delete. */
	deleted: string[]
	/** How many upload requests reached the stub. */
	requests: number
	/** Upload requests the browser aborted. */
	aborted: number
	/** Submission bodies, parsed. */
	submissions: { answers: { questionRevisionId: string; attachments: { uploadId: string; fileName: string }[] | null }[] }[]
	/** Releases every upload currently held open. */
	release: () => void
	/** When true, new uploads are held open until `release`. */
	hold: boolean
	/** A refusal reason to answer the next upload with, once. */
	refuseNext: string | null
	/** Upload ids the next submission reports as expired, once. */
	expireNext: string[]
	/** The uploads the saved report names, which abandoning it must delete. */
	saved: string[]
}

const stubs = new WeakMap<Page, UploadStub>()

async function stubUploads(page: Page): Promise<UploadStub> {
	const waiting: (() => void)[] = []
	const stub: UploadStub = {
		issued: [],
		deleted: [],
		requests: 0,
		aborted: 0,
		submissions: [],
		hold: false,
		refuseNext: null,
		expireNext: [],
		saved: [],
		release: () => {
			for (const resume of waiting.splice(0)) resume()
		},
	}
	stubs.set(page, stub)

	page.on("requestfailed", (request) => {
		if (request.url().includes("/api/v1/uploads/") && request.method() === "POST") stub.aborted += 1
	})

	await page.route(
		(url) => url.pathname.startsWith("/api/v1/uploads/"),
		async (route: Route) => {
			const request = route.request()

			if (request.method() === "DELETE") {
				stub.deleted.push(decodeURIComponent(new URL(request.url()).pathname.split("/").pop() ?? ""))
				await route.fulfill({ status: 204 })
				return
			}

			stub.requests += 1

			if (stub.refuseNext) {
				const reason = stub.refuseNext
				stub.refuseNext = null
				await route.fulfill({ status: 400, contentType: "application/problem+json", body: JSON.stringify({ reason }) })
				return
			}

			if (stub.hold) await new Promise<void>((resume) => waiting.push(resume))

			const uploadId = `synthetic-upload-${String(stub.issued.length).padStart(4, "0")}xxxxxx`.slice(0, 22)
			stub.issued.push(uploadId)
			await route
				.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify({ uploadId, kind: "image" }) })
				.catch(() => {}) // The browser may already have cancelled it.
		},
	)

	await page.route("**/api/v1/reports/", async (route) => {
		const body = JSON.parse(route.request().postData() ?? "{}")
		stub.submissions.push(body)

		if (stub.expireNext.length > 0) {
			const expiredUploadIds = stub.expireNext
			stub.expireNext = []
			await route.fulfill({
				status: 400,
				contentType: "application/problem+json",
				body: JSON.stringify({ title: "That submission was not accepted.", expiredUploadIds }),
			})
			return
		}

		await route.fulfill({
			status: 202,
			contentType: "application/json",
			body: JSON.stringify({ id: "synthetic-report-id", status: "submitted" }),
		})
	})

	return stub
}

function stubFor(page: Page): UploadStub {
	const stub = stubs.get(page)
	if (!stub) throw new Error("Uploads were not stubbed for this page.")
	return stub
}

function photo(name: string) {
	return { name, mimeType: "image/png", buffer: Buffer.from(`synthetic ${name}`) }
}

function attachmentList(page: Page) {
	return page.getByRole("list", { name: "Attached files" })
}

function nextOrSubmit(page: Page) {
	return page.getByRole("button", { name: /^(Next|Submit report)$/ })
}

/** Opens the form and walks to the file-upload page, filling the narrative on the way. */
async function reachAttachmentsPage(page: Page) {
	await stubAuth(page)
	await stubCurrentQuestions(page, defaultFormQuestions())
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
	await page.getByRole("button", { name: "Next" }).click() // intro -> narrative
	await page.getByLabel("What happened?").fill("A synthetic occurrence narrative.")
	await page.getByRole("button", { name: "Next" }).click() // -> injured
	await page.getByRole("group", { name: "Was anyone injured?" }).getByRole("radio", { name: "No" }).click()
	await page.getByRole("button", { name: "Next" }).click() // -> aircraft group
	await page.getByRole("button", { name: "Next" }).click() // -> attachments
	await expect(page.getByLabel("Photos or videos")).toBeAttached()
}

async function attach(page: Page, ...names: string[]) {
	await page.getByLabel("Photos or videos").setInputFiles(names.map(photo))
}

async function submitFromAttachments(page: Page) {
	await page.getByRole("button", { name: "Next" }).click() // -> consent
	await page.getByRole("group", { name: "May we publish a summary of this report?" }).getByRole("radio", { name: "Yes" }).click()
	await page.getByRole("button", { name: "Submit report" }).click()
}

function namedAttachments(stub: UploadStub) {
	const last = stub.submissions.at(-1)
	return last?.answers.find((answer) => answer.questionRevisionId === "rev-attachments")?.attachments ?? []
}

// --- REQ-SUB-045: attaching uploads at once, with an indicator ---

Given("the current page shows a file-upload question", async ({ page }) => {
	const stub = await stubUploads(page)
	stub.hold = true
	await reachAttachmentsPage(page)
})

When("the reporter attaches a file", async ({ page }) => {
	await attach(page, "launch-site.png")
})

Then("the file appears in a list of attached files under its own name", async ({ page }) => {
	await expect(attachmentList(page).getByText("launch-site.png")).toBeVisible()
})

Then("an indeterminate activity indicator shows on that file's row while it uploads", async ({ page }) => {
	const indicator = page.getByRole("progressbar", { name: "Uploading launch-site.png" })
	await expect(indicator).toBeVisible()
	// Indeterminate: no value, because the form does not report a percentage.
	await expect(indicator).not.toHaveAttribute("aria-valuenow")
	// Encapsulated in the field: the indicator lives inside the attachment list.
	await expect(attachmentList(page).getByRole("progressbar")).toHaveCount(1)
})

Then("once the upload finishes the indicator is replaced by a Remove control", async ({ page }) => {
	stubFor(page).release()
	await expect(page.getByRole("button", { name: "Remove launch-site.png" })).toBeVisible()
	await expect(page.getByRole("progressbar")).toHaveCount(0)
})

// --- REQ-SUB-046 / 047: Next waits; Cancel aborts ---

Given("a file on the current page is still uploading", async ({ page }) => {
	const stub = await stubUploads(page)
	stub.hold = true
	await reachAttachmentsPage(page)
	await attach(page, "launch-site.png")
	await expect(page.getByRole("progressbar", { name: "Uploading launch-site.png" })).toBeVisible()
})

Then("the Next or Submit control is disabled", async ({ page }) => {
	await expect(nextOrSubmit(page)).toBeDisabled()
})

When("the upload finishes", async ({ page }) => {
	stubFor(page).release()
	await expect(page.getByRole("button", { name: "Remove launch-site.png" })).toBeVisible()
})

Then("the Next or Submit control is enabled again", async ({ page }) => {
	await expect(nextOrSubmit(page)).toBeEnabled()
})

When("the reporter presses that file's Cancel control", async ({ page }) => {
	await page.getByRole("button", { name: "Cancel uploading launch-site.png" }).click()
})

Then("the upload request is aborted", async ({ page }) => {
	await expect.poll(() => stubFor(page).aborted).toBeGreaterThan(0)
})

Then("the file is removed from the list", async ({ page }) => {
	await expect(page.getByText("launch-site.png")).toHaveCount(0)
	await expect(nextOrSubmit(page)).toBeEnabled()
})

// --- REQ-SUB-048: removing an uploaded file ---

Given("a file on the current page has finished uploading", async ({ page }) => {
	await stubUploads(page)
	await reachAttachmentsPage(page)
	await attach(page, "launch-site.png")
	await expect(page.getByRole("button", { name: "Remove launch-site.png" })).toBeVisible()
})

When("the reporter presses that file's Remove control", async ({ page }) => {
	await page.getByRole("button", { name: "Remove launch-site.png" }).click()
})

Then("the browser asks the API to delete that upload", async ({ page }) => {
	const stub = stubFor(page)
	await expect.poll(() => stub.deleted).toEqual([stub.issued[0]])
})

Then("the file is removed from the list and is not named by the submission", async ({ page }) => {
	await expect(page.getByText("launch-site.png")).toHaveCount(0)
	await submitFromAttachments(page)
	await expect(page.getByRole("heading", { name: "Report submitted" })).toBeVisible()
	expect(namedAttachments(stubFor(page))).toEqual([])
})

// --- REQ-SUB-049: the attachment limit ---

Given("the reporter has already attached as many files as the attachment limit allows", async ({ page }) => {
	await stubUploads(page)
	await reachAttachmentsPage(page)
	await attach(page, "one.png", "two.png", "three.png", "four.png", "five.png")
	await expect(page.getByRole("button", { name: /^Remove / })).toHaveCount(5)
})

When("the reporter attaches one more", async ({ page }) => {
	await attach(page, "six.png")
})

Then("that file is not uploaded", async ({ page }) => {
	expect(stubFor(page).requests).toBe(5)
})

Then("an inline, localized message states the limit", async ({ page }) => {
	await expect(page.getByText("No more files can be attached to this report.")).toBeVisible()
})

// --- REQ-SUB-050: a refused upload ---

Given("the API refuses an uploaded file", async ({ page }) => {
	const stub = await stubUploads(page)
	await reachAttachmentsPage(page)
	stub.refuseNext = "declared_type_mismatch"
	await attach(page, "not-really.png")
})

Then("that file's row shows a localized reason matching the refusal", async ({ page }) => {
	await expect(attachmentList(page).getByText("This file's contents do not match its type.")).toBeVisible()
})

Then("the file is not named by the submission", async ({ page }) => {
	await submitFromAttachments(page)
	await expect(page.getByRole("heading", { name: "Report submitted" })).toBeVisible()
	expect(namedAttachments(stubFor(page))).toEqual([])
})

// --- REQ-SUB-051: expired uploads ---

Given("the API refuses a submission because some of its uploads expired", async ({ page }) => {
	const stub = await stubUploads(page)
	await reachAttachmentsPage(page)
	await attach(page, "kept.png", "stale.png")
	await expect(page.getByRole("button", { name: /^Remove / })).toHaveCount(2)
	const stale = stub.issued[1]!
	stub.expireNext = [stale]
	await submitFromAttachments(page)
	await expect(page.getByRole("alert").filter({ hasText: "Some attached files are no longer available" })).toBeVisible()
})

Then("each of those files is marked expired with a prompt to attach it again", async ({ page }) => {
	await page.getByRole("button", { name: "Back" }).click() // consent -> attachments
	const staleRow = attachmentList(page).getByRole("listitem").filter({ hasText: "stale.png" })
	await expect(staleRow.getByText("This file is no longer available. Remove it and attach it again.")).toBeVisible()
})

Then("every other answer and upload is kept", async ({ page }) => {
	const keptRow = attachmentList(page).getByRole("listitem").filter({ hasText: "kept.png" })
	await expect(keptRow.getByRole("alert")).toHaveCount(0)
	await page.getByRole("button", { name: "Back" }).click() // -> group
	await page.getByRole("button", { name: "Back" }).click() // -> injured
	await page.getByRole("button", { name: "Back" }).click() // -> narrative
	await expect(page.getByLabel("What happened?")).toHaveValue("A synthetic occurrence narrative.")
	for (let step = 0; step < 3; step++) await page.getByRole("button", { name: "Next" }).click()
})

Then("the reporter can submit again once the files are re-attached", async ({ page }) => {
	const stub = stubFor(page)
	await page.getByRole("button", { name: "Remove stale.png" }).click()
	await attach(page, "stale.png")
	await expect(page.getByRole("button", { name: /^Remove / })).toHaveCount(2)
	await submitFromAttachments(page)
	await expect(page.getByRole("heading", { name: "Report submitted" })).toBeVisible()
	expect(namedAttachments(stub).map((attachment) => attachment.fileName).sort()).toEqual(["kept.png", "stale.png"])
	expect(namedAttachments(stub).map((attachment) => attachment.uploadId)).not.toContain(stub.issued[1])
})

// --- REQ-SUB-063..067: kept as long as the saved report ---

interface SavedDraft {
	answers: Record<string, unknown>
	attachments?: Record<string, { uploadId: string; name: string }[]>
}

function discardDialog(page: Page) {
	return page.getByRole("dialog", { name: "Discard this report?" })
}

Given("the reporter has uploaded files and the browser holds a saved report", async ({ page }) => {
	const stub = await stubUploads(page)
	await reachAttachmentsPage(page)
	await attach(page, "launch-site.png")
	await expect(page.getByRole("button", { name: "Remove launch-site.png" })).toBeVisible()
	stub.saved = [...stub.issued]
	await expect
		.poll(async () => ((await readDraftFromBrowser(page)) as SavedDraft | null)?.attachments?.["rev-attachments"]?.map((file) => file.uploadId))
		.toEqual(stub.saved)
})

Given("this browser holds a saved report naming uploaded files", async ({ page }) => {
	const stub = await stubUploads(page)
	stub.saved = [SAVED_UPLOAD.uploadId]
	await stubAuth(page)
	await stubCurrentQuestions(page, defaultFormQuestions())
	await writeSavedDraftToBrowser(page)
})

Given("this browser holds a saved report started more than 15 days ago that names uploaded files", async ({ page }) => {
	const stub = await stubUploads(page)
	stub.saved = [SAVED_UPLOAD.uploadId]
	await stubAuth(page)
	await stubCurrentQuestions(page, defaultFormQuestions())
	await writeStaleDraftToBrowser(page)
})

When("the reporter reloads the form and continues the saved report", async ({ page }) => {
	await page.reload()
	await page.getByRole("dialog", { name: "Continue where you left off?" }).getByRole("button", { name: "Yes, continue" }).click()
	// Continue reopens the page the reporter was on — the attachments page.
	await expect(page.getByLabel("Photos or videos")).toBeAttached()
})

When("the reporter discards the report and confirms", async ({ page }) => {
	await page.getByRole("button", { name: "Discard report" }).click()
	await discardDialog(page).getByRole("button", { name: "Yes, discard it" }).click()
	await expect(discardDialog(page)).toHaveCount(0)
})

When("the reporter presses Discard report and then keeps the report", async ({ page }) => {
	await page.getByRole("button", { name: "Discard report" }).click()
	await expect(discardDialog(page).getByRole("button", { name: "No, keep it" })).toBeFocused()
	await discardDialog(page).getByRole("button", { name: "No, keep it" }).click()
	await expect(discardDialog(page)).toHaveCount(0)
})

Then("each uploaded file is listed as attached under its own name, with a Remove control", async ({ page }) => {
	await expect(attachmentList(page).getByText("launch-site.png")).toBeVisible()
	await expect(page.getByRole("button", { name: "Remove launch-site.png" })).toBeVisible()
	await expect(attachmentList(page).getByRole("alert")).toHaveCount(0)
})

Then("the submission names each restored file by its upload ID", async ({ page }) => {
	const stub = stubFor(page)
	await submitFromAttachments(page)
	await expect(page.getByRole("heading", { name: "Report submitted" })).toBeVisible()
	expect(namedAttachments(stub)).toEqual([{ uploadId: stub.saved[0], fileName: "launch-site.png" }])
})

Then("the browser asks the API to delete each of those uploads", async ({ page }) => {
	const stub = stubFor(page)
	await expect.poll(() => [...stub.deleted].sort()).toEqual([...stub.saved].sort())
})

Then("no upload is deleted", async ({ page }) => {
	expect(stubFor(page).deleted).toEqual([])
})

Then("the saved report and its answers are kept", async ({ page }) => {
	const stub = stubFor(page)
	const draft = (await readDraftFromBrowser(page)) as SavedDraft | null
	expect(draft?.answers).toHaveProperty("rev-narrative")
	expect(draft?.attachments?.["rev-attachments"]?.map((file) => file.uploadId)).toEqual(stub.saved)
	await expect(page.getByRole("button", { name: "Remove launch-site.png" })).toBeVisible()
})

// --- REQ-SUB-058..062: the drop zone ---

function dropZone(page: Page) {
	return page.getByTestId("attachment-drop-zone")
}

function chooseFilesButton(page: Page) {
	return dropZone(page).getByRole("button", { name: "Drag files here, or choose files" })
}

/** Builds a DataTransfer carrying synthetic files in the page, as a real drag would. */
async function filesTransfer(page: Page, names: string[]) {
	return page.evaluateHandle((fileNames) => {
		const transfer = new DataTransfer()
		for (const name of fileNames) transfer.items.add(new File([`synthetic ${name}`], name, { type: "image/png" }))
		return transfer
	}, names)
}

async function dropOnZone(page: Page, ...names: string[]) {
	const dataTransfer = await filesTransfer(page, names)
	for (const type of ["dragenter", "dragover", "drop"]) await dropZone(page).dispatchEvent(type, { dataTransfer })
}

Then(
	"the field shows a drop zone with a large upload icon and a localized {string} prompt",
	async ({ page }, _prompt: string) => {
		await expect(chooseFilesButton(page)).toBeVisible()
		const icon = chooseFilesButton(page).locator("svg")
		await expect(icon).toHaveAttribute("aria-hidden", "true")
		const box = await icon.boundingBox()
		expect(box?.width ?? 0).toBeGreaterThanOrEqual(48)
	},
)

Then("the type, count, and size guidance sits inside the drop zone", async ({ page }) => {
	await expect(dropZone(page).getByText(/up to 5 files in all, 50 MB each/)).toBeVisible()
})

When("the reporter activates the drop zone's choose-files control by pointer or keyboard", async ({ page }) => {
	const byPointer = page.waitForEvent("filechooser")
	await chooseFilesButton(page).click()
	const pointerChooser = await byPointer
	expect(pointerChooser.isMultiple()).toBe(true)
	await pointerChooser.setFiles([])

	const byKeyboard = page.waitForEvent("filechooser")
	await chooseFilesButton(page).focus()
	await page.keyboard.press("Enter")
	const keyboardChooser = await byKeyboard
	await keyboardChooser.setFiles(photo("keyboard.png"))
})

Then("the browser's file chooser opens for that question", async ({ page }) => {
	// The file chosen through the keyboard-opened chooser landed in this field.
	await expect(attachmentList(page).getByText("keyboard.png")).toBeVisible()
})

When("the reporter drops two files on the drop zone", async ({ page }) => {
	await dropOnZone(page, "dropped-one.png", "dropped-two.png")
})

Then("both files appear in the list of attached files under their own names", async ({ page }) => {
	await expect(attachmentList(page).getByText("dropped-one.png")).toBeVisible()
	await expect(attachmentList(page).getByText("dropped-two.png")).toBeVisible()
})

Then("each shows its own activity indicator while it uploads", async ({ page }) => {
	await expect(page.getByRole("progressbar", { name: "Uploading dropped-one.png" })).toBeVisible()
	await expect(page.getByRole("progressbar", { name: "Uploading dropped-two.png" })).toBeVisible()
	stubFor(page).release()
	await expect(page.getByRole("button", { name: /^Remove dropped-/ })).toHaveCount(2)
})

Given("the reporter has attached one file fewer than the attachment limit allows", async ({ page }) => {
	await stubUploads(page)
	await reachAttachmentsPage(page)
	await attach(page, "one.png", "two.png", "three.png", "four.png")
	await expect(page.getByRole("button", { name: /^Remove / })).toHaveCount(4)
})

When("the reporter drops two more files on the drop zone", async ({ page }) => {
	await dropOnZone(page, "five.png", "six.png")
})

Then("only one of them is uploaded", async ({ page }) => {
	await expect(page.getByRole("button", { name: /^Remove / })).toHaveCount(6)
	expect(stubFor(page).requests).toBe(5)
})

When("the reporter drops a file on the page outside the drop zone", async ({ page }) => {
	const cancelled = await page.evaluate(() => {
		const transfer = new DataTransfer()
		transfer.items.add(new File(["synthetic stray"], "stray.png", { type: "image/png" }))
		const target = document.querySelector("h1") ?? document.body
		const fire = (type: string) =>
			!target.dispatchEvent(new DragEvent(type, { bubbles: true, cancelable: true, dataTransfer: transfer }))
		return { dragover: fire("dragover"), drop: fire("drop") }
	})
	// A cancelled dragover and drop are what stop the browser opening the file in place of the form.
	expect(cancelled).toEqual({ dragover: true, drop: true })
})

Then("the browser stays on the form", async ({ page }) => {
	await expect(page).toHaveURL(/\/report/)
	await expect(chooseFilesButton(page)).toBeVisible()
})

Then("no file is attached or uploaded", async ({ page }) => {
	await expect(page.getByText("stray.png")).toHaveCount(0)
	expect(stubFor(page).requests).toBe(0)
})
