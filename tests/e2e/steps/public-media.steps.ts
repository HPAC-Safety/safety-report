import { readFileSync } from "node:fs"
import { createBdd } from "playwright-bdd"
import { expect, type Page, type Route } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { mediaConsentFormQuestions, stubCurrentQuestions } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for a published report's photos, video, and documents
 * (REQ-MED-032..036, REQ-MED-041/042, ADR-0117, ADR-0119) and the form asking
 * media consent (REQ-QB-113).
 *
 * The API and the storage links are stubbed at the network boundary, so each
 * scenario controls when a link works, stops working, or answers 404. Which
 * files are public, the link's lifetime and headers, and the audit rows are
 * proven against a real database and S3 server by the Reqnroll scenarios
 * REQ-MED-025..031 and REQ-QB-114/115 (ADR-0045).
 *
 * Every report and file below is synthetic. The clip is WebM because the
 * Chromium Playwright runs cannot decode H.264.
 */

const PHOTO = readFileSync(new URL("../fixtures/synthetic-photo.png", import.meta.url))
const PDF = Buffer.from("%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n")
const CLIP = readFileSync(new URL("../fixtures/synthetic-clip.webm", import.meta.url))

const REPORT = {
	id: "mediaaaaaa1",
	aiSummaryEn: "The pilot landed in a field after a crosswind launch.",
	aiSummaryFr: "Le pilote s'est posé dans un champ après un décollage par vent de travers.",
	publishedAt: "2026-09-20T15:30:00Z",
	commentCount: 0,
}

const IMAGE = { id: "imageaaaaa1", kind: "image", format: null }
const VIDEO = { id: "videoaaaaa1", kind: "video", format: null }
const DOCUMENT = { id: "documentaa1", kind: "document", format: "pdf" }
const STORAGE = "https://storage.example.test"

interface StubMedia {
	id: string
	kind: string
	format: string | null
}

interface MediaStub {
	media: StubMedia[]
	/** How many links each file has been issued. */
	issued: Record<string, number>
	/** Files whose link endpoint now answers 404. */
	gone: Set<string>
	/** Files whose link endpoint answers 404 once it has issued this many links. */
	goneAfter: Record<string, number>
	/** Link generations the storage stub refuses, as `<id>-<generation>`. */
	expired: Set<string>
	hidden: string[]
}

const stubs = new WeakMap<Page, MediaStub>()

async function stubReport(page: Page, media: StubMedia[]) {
	const stub: MediaStub = { media: [...media], issued: {}, gone: new Set(), goneAfter: {}, expired: new Set(), hidden: [] }
	stubs.set(page, stub)

	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/comments\/?$/, (route) => route.fulfill({ json: [] }))

	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/media\/[^/?]+$/, async (route) => {
		const id = new URL(route.request().url()).pathname.split("/").pop()!
		if (stub.gone.has(id) || (stub.issued[id] ?? 0) >= (stub.goneAfter[id] ?? Infinity)) {
			await route.fulfill({ status: 404, body: "" })
			return
		}
		const generation = (stub.issued[id] ?? 0) + 1
		stub.issued[id] = generation
		await route.fulfill({
			json: { url: `${STORAGE}/${id}-${generation}`, expiresAt: "2026-09-20T15:45:00Z" },
			headers: { "X-Content-Type-Options": "nosniff" },
		})
	})

	await page.route(/\/api\/v1\/public\/reports\/[^/?]+$/, (route) => route.fulfill({ json: { ...REPORT, media: stub.media } }))

	await page.route(`${STORAGE}/**`, async (route: Route) => {
		const name = new URL(route.request().url()).pathname.slice(1)
		if (stub.expired.has(name)) {
			await route.fulfill({ status: 403, body: "" })
			return
		}
		// A document is only ever a forced download, as S3 serves it (ADR-0119).
		if (name.startsWith(DOCUMENT.id)) {
			await route.fulfill({
				status: 200,
				contentType: "application/pdf",
				headers: { "Content-Disposition": `attachment; filename="${DOCUMENT.id}.pdf"` },
				body: PDF,
			})
			return
		}

		const isVideo = name.startsWith(VIDEO.id)
		const body = isVideo ? CLIP : PHOTO
		const contentType = isVideo ? "video/webm" : "image/png"

		// Range requests, as S3 answers them: without them a browser cannot
		// seek, and a video cannot be part-way through.
		const range = /bytes=(\d+)-(\d*)/.exec(route.request().headers()["range"] ?? "")
		if (range) {
			const start = Number(range[1])
			const end = range[2] ? Math.min(Number(range[2]), body.length - 1) : body.length - 1
			await route.fulfill({
				status: 206,
				contentType,
				headers: { "Accept-Ranges": "bytes", "Content-Range": `bytes ${start}-${end}/${body.length}` },
				body: body.subarray(start, end + 1),
			})
			return
		}

		await route.fulfill({ status: 200, contentType, headers: { "Accept-Ranges": "bytes" }, body })
	})

	await page.route(/\/api\/admin\/reports\/[^/?]+\/attachments\/[^/?]+\/hide$/, async (route) => {
		const id = new URL(route.request().url()).pathname.split("/").at(-2)!
		stub.hidden.push(id)
		stub.gone.add(id)
		stub.media = stub.media.filter((item) => item.id !== id)
		await route.fulfill({ status: 204 })
	})

	return stub
}

function stubOf(page: Page): MediaStub {
	const stub = stubs.get(page)
	if (!stub) throw new Error("The report was not stubbed.")
	return stub
}

function video(page: Page) {
	return page.locator('video[aria-label="Video 1 of 1"]')
}

// The Attachments feature's Background, which states limits the API enforces
// (REQ-MED-001 and the upload scenarios prove them); nothing for a browser to set.
Given("the maximum attachment count is configurable and defaults to five across all attachment kinds", async () => {})
Given("each file is limited to 250 MB for a video and 25 MB for an image or a document", async () => {})

// --- REQ-MED-032: embedded photos and video with a generic label ---

Given("a published report shows an image and a video", async ({ page }) => {
	await stubReport(page, [IMAGE, VIDEO])
})

When("a visitor opens the report", async ({ page }) => {
	await page.goto(`/reports/${REPORT.id}`)
})

Then("the image is shown in the page, labelled {string}", async ({ page }, label: string) => {
	const image = page.getByRole("img", { name: label })
	await expect(image).toBeVisible()
	await expect.poll(() => image.evaluate((element: HTMLImageElement) => element.naturalWidth)).toBeGreaterThan(0)
})

Then("the video can be played in the page with its controls, labelled {string}", async ({ page }, label: string) => {
	const player = page.locator(`video[aria-label="${label}"]`)
	await expect(player).toBeVisible()
	await expect(player).toHaveAttribute("controls", "")
	await expect.poll(() => player.evaluate((element: HTMLVideoElement) => element.readyState)).toBeGreaterThan(0)
})

// --- REQ-MED-033: an expired link is replaced and the video resumes ---

Given("a visitor is part-way through a public video", async ({ page }) => {
	await stubReport(page, [VIDEO])
	await page.goto(`/reports/${REPORT.id}`)
	await expect.poll(() => video(page).evaluate((element: HTMLVideoElement) => element.readyState)).toBeGreaterThan(0)
	await video(page).evaluate((element: HTMLVideoElement) => {
		element.currentTime = 1.2
	})
	await expect.poll(() => video(page).evaluate((element: HTMLVideoElement) => element.currentTime)).toBeCloseTo(1.2, 1)
})

When("the video's link stops working", async ({ page }) => {
	stubOf(page).expired.add(`${VIDEO.id}-1`)
	// The browser asks for the bytes again, as it does when it seeks into a
	// range it has not buffered; the expired link now refuses it.
	await video(page).evaluate((element: HTMLVideoElement) => element.load())
})

Then("the page fetches a new link", async ({ page }) => {
	await expect(video(page)).toHaveAttribute("src", `${STORAGE}/${VIDEO.id}-2`)
})

Then("the video resumes from where it was", async ({ page }) => {
	await expect.poll(() => video(page).evaluate((element: HTMLVideoElement) => element.currentTime)).toBeCloseTo(1.2, 1)
})

// --- REQ-MED-034: media no longer public is removed ---

Given("a visitor opens a published report showing an image", async ({ page }) => {
	const stub = await stubReport(page, [IMAGE])
	// Its bytes are refused from the first request, as they are once a hidden
	// file's link has expired.
	stub.expired.add(`${IMAGE.id}-1`)
})

When("the image's link stops working because the image is no longer public", async ({ page }) => {
	// The first link is issued; asked again after it fails, the API says the
	// file is no longer public.
	stubOf(page).goneAfter[IMAGE.id] = 1
	await page.goto(`/reports/${REPORT.id}`)
})

Then("the page removes the image", async ({ page }) => {
	await expect.poll(() => stubOf(page).issued[IMAGE.id]).toBe(1)
	await expect(page.locator('[data-media="image"]')).toHaveCount(0)
	await expect(page.getByRole("heading", { name: "Photos, video, and documents" })).toHaveCount(0)
	await expect(page.locator("[data-summary]")).toBeVisible()
})

// --- REQ-MED-035: a reviewer hides a file from the public page ---

Given("a safety officer is signed in and a published report shows an image", async ({ page }) => {
	await stubReport(page, [IMAGE])
	await signInAs(page, "safety_officer")
})

// "the safety officer opens the report" is comments.steps.ts's step: it opens a
// report page, and the stubs above answer for any report.

Then("the image offers to hide it", async ({ page }) => {
	await expect(page.getByRole("img", { name: "Photo 1 of 1" })).toBeVisible()
	await expect(page.locator('[data-media="image"]').getByRole("button", { name: "Hide" })).toBeVisible()
})

When("the safety officer hides the image and confirms", async ({ page }) => {
	const item = page.locator('[data-media="image"]')
	await item.getByRole("button", { name: "Hide" }).click()
	await expect(item.getByText("Hide this from everyone?")).toBeVisible()
	await item.getByRole("button", { name: "Hide" }).click()
})

Then("the image is no longer shown", async ({ page }) => {
	await expect(page.getByRole("img", { name: "Photo 1 of 1" })).toHaveCount(0)
	expect(stubOf(page).hidden).toEqual([IMAGE.id])
})

// --- REQ-MED-041: a public document is offered as a download, never inline ---

Given("a published report offers a validated PDF document", async ({ page }) => {
	await stubReport(page, [DOCUMENT])
})

Then("the document is offered as a download labelled {string}", async ({ page }, label: string) => {
	const item = page.locator('[data-media="document"]')
	await expect(item.getByText(label)).toBeVisible()

	const download = page.waitForEvent("download")
	await item.getByRole("button", { name: `Download ${label}` }).click()
	expect((await download).suggestedFilename()).toBe(`${DOCUMENT.id}.pdf`)
	expect(stubOf(page).issued[DOCUMENT.id]).toBe(1)
})

Then("the page never embeds the document's content", async ({ page }) => {
	await expect(page.locator("iframe, embed, object")).toHaveCount(0)
	await expect(page).toHaveURL(new RegExp(`/reports/${REPORT.id}$`))
})

// --- REQ-MED-036/042: the admin page shows whether each file is public ---

const ADMIN_REPORT = {
	id: "adminmedia1",
	submittedAt: "2026-09-20T15:30:00Z",
	status: "published",
	language: "en-CA",
	consent: true,
	mediaConsent: true,
	isStuck: false,
	summaryError: null,
	answers: [],
	summary: {
		aiSummaryEn: "The pilot made a firm landing.",
		aiSummaryFr: "Le pilote a fait un atterrissage ferme.",
		model: "gemini-3.7-flash",
		promptVersion: "summarize-anonymize.v3",
		generatedAt: "2026-09-20T15:35:00Z",
		updatedAt: "2026-09-20T15:35:00Z",
		approvedBySubject: "dev:officer",
		approvedAt: "2026-09-20T16:00:00Z",
		sourceEn: "generated",
		sourceFr: "generated",
	},
	version: "1",
	unpublishNote: null,
	publishedAt: "2026-09-20T16:00:00Z",
}

Given("a published report has a public {word} and a hidden {word}", async ({ page }, kind: string, _hidden: string) => {
	const files = [
		{ id: "publicfile1", kind, state: "ready", visibility: "public" },
		{ id: "hiddenfile1", kind, state: "ready", visibility: "hidden" },
	]
	const changed: string[] = []

	await page.route(/\/api\/admin\/reports\/[^/?]+\/attachments\/[^/?]+\/(hide|show)$/, async (route) => {
		const parts = new URL(route.request().url()).pathname.split("/")
		const file = files.find((candidate) => candidate.id === parts.at(-2))!
		file.visibility = parts.at(-1) === "hide" ? "hidden" : "public"
		changed.push(`${parts.at(-1)} ${file.id}`)
		await route.fulfill({ status: 204 })
	})
	await page.route(/\/api\/admin\/reports\/[^/?]+$/, (route) => route.fulfill({ json: { ...ADMIN_REPORT, attachments: files } }))
	await signInAs(page, "safety_officer")
})

When("a safety officer opens the report in the admin area", async ({ page }) => {
	await page.goto(`/admin/reports/${ADMIN_REPORT.id}`)
	await expect(page.getByRole("heading", { level: 2, name: "Attachments", exact: true })).toBeVisible()
})

Then("the public {word} reads as shown publicly and offers to hide it", async ({ page }, _kind: string) => {
	const row = page.getByRole("listitem").filter({ has: page.locator('[data-visibility="public"]') })
	await expect(row.locator('[data-visibility="public"]')).toHaveText("Shown on the public report")
	await expect(row.getByRole("button", { name: "Hide from the public" })).toBeVisible()
})

Then("the hidden {word} reads as hidden from the public and offers to show it", async ({ page }, _kind: string) => {
	const row = page.getByRole("listitem").filter({ has: page.locator('[data-visibility="hidden"]') })
	await expect(row.locator('[data-visibility="hidden"]')).toHaveText("Hidden from the public")
	await row.getByRole("button", { name: "Show on the public report" }).click()
	await expect(page.locator('[data-visibility="hidden"]')).toHaveCount(0)
	await expect(page.locator('[data-visibility="public"]')).toHaveCount(2)
})

// --- REQ-QB-113: the form asks media consent only when there is media ---

interface FormStub {
	submissions: { answers: { questionRevisionId: string }[] }[]
}

const forms = new WeakMap<Page, FormStub>()

function consentGroup(page: Page, name: string) {
	return page.getByRole("group", { name })
}

async function next(page: Page) {
	await page.getByRole("button", { name: "Next" }).click()
}

Given("a reporter is filling in the form", async ({ page }) => {
	const form: FormStub = { submissions: [] }
	forms.set(page, form)

	await stubAuth(page)
	await stubCurrentQuestions(page, mediaConsentFormQuestions())
	await page.route(
		(url) => url.pathname.startsWith("/api/v1/uploads/"),
		(route) =>
			route.request().method() === "DELETE"
				? route.fulfill({ status: 204 })
				: route.fulfill({
						status: 201,
						json: {
							uploadId: "synthetic-upload-media01",
							kind: "image",
							uploadUrl: "https://storage.hpac-safety.test/hpac-safety-uploads/quarantine/synthetic-upload-media01",
							expiresAt: "2026-09-26T12:15:00Z",
						},
					}),
	)
	// The file itself goes straight to storage, another origin (ADR-0126).
	await page.route("https://storage.hpac-safety.test/**", (route) =>
		route.fulfill({ status: 200, headers: { "access-control-allow-origin": "*" }, body: "" }),
	)
	await page.route("**/api/v1/reports/", async (route) => {
		form.submissions.push(JSON.parse(route.request().postData() ?? "{}"))
		await route.fulfill({ status: 202, json: { id: "synthetic-report-id", status: "submitted" } })
	})
	await signInAs(page, "user")

	await page.goto("/report")
	await next(page) // intro -> narrative
	await page.getByLabel("What happened?").fill("A synthetic occurrence narrative.")
	await next(page) // -> injured
	await consentGroup(page, "Was anyone injured?").getByRole("radio", { name: "No" }).click()
	await next(page) // -> aircraft group
	await next(page) // -> attachments
	await expect(page.getByLabel("Photos or videos")).toBeAttached()
})

When("they answer yes to publication consent and attach an image", async ({ page }) => {
	await attachAndConsent(page, { name: "launch-site.png", mimeType: "image/png", buffer: PHOTO })
})

When("they answer yes to publication consent and attach a document", async ({ page }) => {
	// Media consent covers documents too (ADR-0119).
	await attachAndConsent(page, { name: "checklist.pdf", mimeType: "application/pdf", buffer: PDF })
})

async function attachAndConsent(page: Page, file: { name: string; mimeType: string; buffer: Buffer }) {
	await page.getByLabel("Photos or videos").setInputFiles(file)
	await expect(page.getByRole("button", { name: /Remove/ })).toBeVisible()
	await next(page) // -> publication consent
	await consentGroup(page, "May we publish a summary of this report?").getByRole("radio", { name: "Yes" }).click()
}

Then("the form asks the media consent question, and it must be answered to submit", async ({ page }) => {
	await next(page) // -> media consent, which now follows
	const media = consentGroup(page, "Photo, video, and document consent")
	await expect(media).toBeVisible()
	await page.getByRole("button", { name: "Submit report" }).click()
	await expect(page.getByText("This question is required.").first()).toBeVisible()
	expect(forms.get(page)!.submissions).toHaveLength(0)
})

When("they remove the file, or answer no to publication consent", async ({ page }) => {
	// Answering no: publication consent becomes the last page.
	await page.getByRole("button", { name: "Back" }).click()
	await consentGroup(page, "May we publish a summary of this report?").getByRole("radio", { name: "No" }).click()
	await expect(page.getByRole("button", { name: "Submit report" })).toBeVisible()

	// Yes again, but with the file removed: still the last page.
	await consentGroup(page, "May we publish a summary of this report?").getByRole("radio", { name: "Yes" }).click()
	await expect(page.getByRole("button", { name: "Next" })).toBeVisible()
	await page.getByRole("button", { name: "Back" }).click()
	await page.getByRole("button", { name: /Remove/ }).click()
	await next(page)
})

Then("the form no longer asks it, and submits no answer to it", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Submit report" })).toBeVisible()
	await page.getByRole("button", { name: "Submit report" }).click()
	await expect.poll(() => forms.get(page)!.submissions.length).toBe(1)
	const answered = forms.get(page)!.submissions[0].answers.map((answer) => answer.questionRevisionId)
	expect(answered).toContain("rev-consent")
	expect(answered).not.toContain("rev-media_consent")
})
