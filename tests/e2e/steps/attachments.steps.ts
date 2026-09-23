import { createBdd } from "playwright-bdd"
import { expect, type Page, type Route } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { defaultFormQuestions, forgetDraftInBrowser, stubCurrentQuestions } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for uploading attachments as they are attached (ADR-0096,
 * REQ-SUB-045..052). The API is stubbed at the network boundary: an upload is
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
	await expect(page.getByLabel("Photos or videos")).toBeVisible()
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

// --- REQ-SUB-052: not restored after reload ---

Given("the reporter has uploaded files and the browser holds a saved report", async ({ page }) => {
	await stubUploads(page)
	await reachAttachmentsPage(page)
	await attach(page, "launch-site.png")
	await expect(page.getByRole("button", { name: "Remove launch-site.png" })).toBeVisible()
})

When("the reporter reloads the form and continues the saved report", async ({ page }) => {
	await page.reload()
	await page.getByRole("dialog", { name: "Continue where you left off?" }).getByRole("button", { name: "Yes, continue" }).click()
	// Continue reopens the page the reporter was on — the attachments page.
	await expect(page.getByLabel("Photos or videos")).toBeVisible()
})

Then("no file is listed as attached", async ({ page }) => {
	await expect(page.getByText("launch-site.png")).toHaveCount(0)
})

Then("the reporter is told to attach the files again", async ({ page }) => {
	await expect(page.getByText(/you will need to attach them again/i)).toBeVisible()
	await forgetDraftInBrowser(page)
})
