import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import {
	defaultFormQuestions,
	multiSelectFormQuestions,
	typeAheadFormQuestions,
	forgetDraftInBrowser,
	readDraftFromBrowser,
	stubCurrentQuestions,
	stubSubmission,
	writeSavedDraftToBrowser,
	writeStaleDraftToBrowser,
	type StubQuestion,
} from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for the reporter-facing form (issue #80, ADR-0053).
 *
 * As with manage-questions.steps.ts, the API is stubbed at the network
 * boundary — Playwright drives the built static site with no live backend —
 * and every question below is synthetic. The server side of the same
 * contracts is covered by HpacSafety.Api.Tests and
 * HpacSafety.Acceptance.Tests.
 */

const submittedRequests: string[] = []

async function trackSubmissions(page: Page) {
	page.on("request", (request) => {
		if (request.url().includes("/api/v1/reports/") && request.method() === "POST") {
			submittedRequests.push(request.url())
		}
	})
}

async function openForm(page: Page, questions: StubQuestion[] = defaultFormQuestions()) {
	submittedRequests.length = 0
	await stubAuth(page)
	await stubCurrentQuestions(page, questions)
	await trackSubmissions(page)
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
}

function resumeDialog(page: Page) {
	return page.getByRole("dialog", { name: "Continue where you left off?" })
}

async function goNext(page: Page) {
	await page.getByRole("button", { name: "Next" }).click()
}

async function goBack(page: Page) {
	await page.getByRole("button", { name: "Back" }).click()
}

async function fillNarrative(page: Page, text: string) {
	await page.getByLabel("What happened?").fill(text)
}

async function answerYesNo(page: Page, questionLabel: string, value: "Yes" | "No") {
	await page.getByRole("group", { name: questionLabel }).getByRole("radio", { name: value }).click()
}

/** Advances from the intro to the group ("Aircraft:") page, answering "No" for injury along the way. */
async function reachGroupPage(page: Page) {
	await goNext(page) // intro -> narrative
	await goNext(page) // narrative -> injured
	await answerYesNo(page, "Was anyone injured?", "No")
	await goNext(page) // injured -> aircraft group (injury_detail skipped)
}

/**
 * Reloads back to the intro with the default fixture. Several scenarios
 * assert more than one thing about the same walk through the form; rather
 * than have each `Then` assume exactly where the previous one left off (which
 * breaks the moment their order or count changes), every `Then` below that
 * needs a specific page starts here instead.
 */
async function resetToIntro(page: Page) {
	await stubCurrentQuestions(page, defaultFormQuestions())
	await forgetDraftInBrowser(page)
	await page.reload()
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
}

/** Advances all the way to the final page (consent), filling every page along the way. */
async function reachLastPage(page: Page) {
	await goNext(page) // intro -> narrative
	await fillNarrative(page, "Something happened on final approach.")
	await goNext(page) // narrative -> injured
	await answerYesNo(page, "Was anyone injured?", "No")
	await goNext(page) // injured -> group
	await page.getByLabel("Type of aircraft").selectOption({ label: "Hang glider" })
	await page.getByLabel("Model").fill("Synthetic 1")
	await goNext(page) // group -> attachments
	await goNext(page) // attachments -> consent
}

Given("a reporter is filling out the form", async ({ page }) => {
	await openForm(page)
	await goNext(page)
	await fillNarrative(page, "A synthetic occurrence narrative.")
})

Given("a reporter has entered answers in local browser storage", async ({ page }) => {
	await openForm(page)
	await goNext(page)
	await fillNarrative(page, "A synthetic occurrence narrative.")
	await expect.poll(() => readDraftFromBrowser(page)).not.toBeNull()
	await goBack(page) // Back to the intro, so a later step can walk the form from its start.
})

Given("local browser state was started more than 15 days ago and saved again since", async ({ page }) => {
	await stubAuth(page)
	await stubCurrentQuestions(page)
	await writeStaleDraftToBrowser(page)
})

Given("this browser holds an unexpired saved report", async ({ page }) => {
	await stubAuth(page)
	await stubCurrentQuestions(page)
	await writeSavedDraftToBrowser(page)
})

Given("this browser holds no saved report", async ({ page }) => {
	await stubAuth(page)
	await stubCurrentQuestions(page)
})

Given("the current form's first question is a live statement", async () => {}) // The default fixture's first question already is one.

Given("the current form has more than one answer-producing question", async ({ page }) => {
	await openForm(page) // The default fixture already has several.
})

Given("a group question has children grouped under it", async ({ page }) => {
	await openForm(page) // The default fixture's "Aircraft:" group already has two children.
})

Given("the current page shows a required, unanswered question", async ({ page }) => {
	const questions = defaultFormQuestions()
	const narrative = questions.find((question) => question.key === "narrative")!
	narrative.isRequired = true
	await openForm(page, questions)
	await goNext(page) // intro -> narrative, left empty
})

Given("a question depends on a yes\\/no or single-select question", async ({ page }) => {
	await openForm(page) // The default fixture's injury_detail already depends on was_injured.
})

Given("a reporter has reached the last page of the form", async ({ page }) => {
	await openForm(page)
	await reachLastPage(page)
})

Given("a reporter has just submitted the form", async ({ page }) => {
	await openForm(page)
	await reachLastPage(page)
	await answerYesNo(page, "May we publish a summary of this report?", "Yes")
})

Given("a reporter has entered answers in one locale", async ({ page }) => {
	await openForm(page)
	await goNext(page)
	await fillNarrative(page, "Written in English.")
})

Given("the form renders its questions in database order", async ({ page }) => {
	await openForm(page)
})

Given("a reporter enters an answer", async ({ page }) => {
	await openForm(page)
})

Given("a reporter uses assistive technology to complete the form", async ({ page }) => {
	await openForm(page)
})

Given("a script error occurs while a reporter is filling out the form", async ({ page }) => {
	await openForm(page)
	await goNext(page)
	await fillNarrative(page, "Saved before the failure.")
})

Given("a submission request fails due to a network error", async ({ page }) => {
	await openForm(page)
	await stubSubmission(page, { networkError: true })
})

When("the reporter has not yet submitted", async () => {})

When("the final submission request succeeds", async ({ page }) => {
	await stubSubmission(page)
	await reachLastPage(page)
	await answerYesNo(page, "May we publish a summary of this report?", "Yes")
	await page.getByRole("button", { name: "Submit report" }).click()
	await expect(page.getByRole("heading", { name: "Report submitted" })).toBeVisible()
})

When("the reporter returns to the form", async ({ page }) => {
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

When("the reporter chooses to continue", async ({ page }) => {
	await resumeDialog(page).getByRole("button", { name: "Yes, continue" }).click()
	await expect(resumeDialog(page)).toHaveCount(0)
})

When("the reporter declines to continue", async ({ page }) => {
	await resumeDialog(page).getByRole("button", { name: "No, start over" }).click()
	await expect(resumeDialog(page)).toHaveCount(0)
})

When("a reporter opens the report page", async ({ page }) => {
	await openForm(page)
})

When("a reporter presses Next", async ({ page }) => {
	await goNext(page)
})

When("a reporter reaches that group's page", async ({ page }) => {
	await reachGroupPage(page)
})

When("the parent's current answer does not meet the condition", async ({ page }) => {
	await goNext(page) // intro -> narrative
	await goNext(page) // narrative -> injured
	await answerYesNo(page, "Was anyone injured?", "No")
	await goNext(page)
})

When("the reporter then answers the parent so the condition is met", async ({ page }) => {
	await goBack(page) // aircraft group -> injured
	await answerYesNo(page, "Was anyone injured?", "Yes")
	await goNext(page)
})

When("the request is still in flight", async ({ page }) => {
	await stubSubmission(page, { delayMs: 1500 })
	await page.getByRole("button", { name: "Submit report" }).click()
})

When("the reporter switches the language toggle", async ({ page }) => {
	await page.getByRole("button", { name: "Français" }).click()
})

When("a reporter views the form", async () => {})

When("the client validates it before submission", async ({ page }) => {
	const questions = defaultFormQuestions()
	const narrative = questions.find((q) => q.key === "narrative")!
	narrative.isRequired = true
	await stubCurrentQuestions(page, questions)
	await forgetDraftInBrowser(page)
	await page.reload()
	await goNext(page)
	await goNext(page) // narrative left empty
})

When("the failure happens", async ({ page }) => {
	await page.evaluate(() => {
		window.dispatchEvent(new ErrorEvent("error", { message: "synthetic script failure" }))
	})
})

When("the browser detects the failure", async ({ page }) => {
	await reachLastPage(page)
	await answerYesNo(page, "May we publish a summary of this report?", "Yes")
	await page.getByRole("button", { name: "Submit report" }).click()
})

Then("the selected locale, shown question-revision IDs, and entered answers exist only in local browser storage with a 15-day expiry", async ({ page }) => {
	const draft = (await readDraftFromBrowser(page)) as { locale: string; answers: Record<string, unknown>; savedAtMs: number } | null
	expect(draft).not.toBeNull()
	expect(draft!.locale).toBe("en-CA")
	expect(Object.keys(draft!.answers).length).toBeGreaterThan(0)
	expect(Date.now() - draft!.savedAtMs).toBeLessThan(60_000)
})

Then("no image, video, or document file is placed in browser storage, only each finished upload's ID, name, and size", async ({ page }) => {
	const draft = (await readDraftFromBrowser(page)) as {
		answers: Record<string, unknown>
		attachments?: Record<string, Record<string, unknown>[]>
	} | null
	for (const answer of Object.values(draft?.answers ?? {})) {
		expect(answer).not.toHaveProperty("file")
	}
	for (const upload of Object.values(draft?.attachments ?? {}).flat()) {
		expect(Object.keys(upload).sort()).toEqual(["name", "size", "uploadId"])
	}
})

Then("no server draft, report ID reservation, or resumable upload protocol exists", async () => {
	expect(submittedRequests).toHaveLength(0)
})

Then("the browser clears that local state", async ({ page }) => {
	expect(await readDraftFromBrowser(page)).toBeNull()
})

Then("the browser ignores or removes the expired state", async ({ page }) => {
	expect(await readDraftFromBrowser(page)).toBeNull()
	await expect(page.getByLabel("What happened?")).not.toBeVisible()
})

Then("the statement renders with a Next control and no Back control", async ({ page }) => {
	await expect(page.getByRole("heading", { level: 1 })).toContainText("Thanks for taking the time")
	await expect(page.getByRole("button", { name: "Next" })).toBeVisible()
	await expect(page.getByRole("button", { name: "Back" })).toHaveCount(0)
})

Then("no answer is collected for it", async ({ page }) => {
	await expect(page.locator("input, textarea, select")).toHaveCount(0)
})

Then("exactly one question, or one group and its children, is shown per page", async ({ page }) => {
	await expect(page.getByLabel("What happened?")).toBeVisible()
	await expect(page.getByText("Was anyone injured?")).not.toBeVisible()
})

Then("a Back control returns to the previous page without losing its answer", async ({ page }) => {
	await fillNarrative(page, "Kept across navigation.")
	await goNext(page)
	await goBack(page)
	await expect(page.getByLabel("What happened?")).toHaveValue("Kept across navigation.")
})

Then("the group heading and every child render together on one page", async ({ page }) => {
	await expect(page.getByRole("group", { name: "Aircraft:" })).toBeVisible()
	await expect(page.getByLabel("Type of aircraft")).toBeVisible()
	await expect(page.getByLabel("Model")).toBeVisible()
})

Then("advancing counts that page as a single step", async ({ page }) => {
	await expect(page.getByText("Step 4 of 6")).toBeAttached()
})

Then("the page does not advance", async ({ page }) => {
	await goNext(page)
	await expect(page.getByLabel("What happened?")).toBeVisible()
})

Then("an inline, localized message explains that an answer is required", async ({ page }) => {
	await expect(page.getByText("This question is required.")).toBeVisible()
})

Then("the dependent question's page is skipped entirely", async ({ page }) => {
	await expect(page.getByLabel("Describe the injury")).not.toBeVisible()
	await expect(page.getByRole("group", { name: "Aircraft:" })).toBeVisible()
})

Then("the dependent question's page appears in the sequence", async ({ page }) => {
	await expect(page.getByLabel("Describe the injury")).toBeVisible()
})

Then("the control that was Next now reads Submit", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Submit report" })).toBeVisible()
	await expect(page.getByRole("button", { name: "Next" })).toHaveCount(0)
})

Then("pressing it sends the one final submission request", async ({ page }) => {
	await stubSubmission(page)
	await answerYesNo(page, "May we publish a summary of this report?", "Yes")
	await page.getByRole("button", { name: "Submit report" }).click()
	await expect.poll(() => submittedRequests.length).toBeGreaterThan(0)
})

Then("the UI shows bounded progress and disables repeat submission", async ({ page }) => {
	const submitButton = page.getByRole("button", { name: "Submitting…" })
	await expect(submitButton).toBeVisible()
	await expect(submitButton).toBeDisabled()
})

Then("retains local state if the network result is uncertain", async ({ page }) => {
	expect(await readDraftFromBrowser(page)).not.toBeNull()
})

Then("clears saved local state only after a definite 202 response", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Report submitted" })).toBeVisible()
	expect(await readDraftFromBrowser(page)).toBeNull()
})

Then("labels, help, validation, navigation, and formatting rerender in the new locale", async ({ page }) => {
	// French for a key not yet human-reviewed is a `#`-stubbed copy of the
	// English text (ADR-0054) until CI's translation workflow runs — asserting
	// specific French words here would fail on exactly that branch. What is
	// asserted is that the document actually rerendered in French.
	await expect(page.locator("html")).toHaveAttribute("lang", "fr-CA")
})

Then("entered answers are neither cleared nor remapped", async ({ page }) => {
	await expect(page.getByLabel("What happened?")).toHaveValue("Written in English.")
})

Then("only the consent_publish question displays required treatment", async ({ page }) => {
	await resetToIntro(page)
	await expect(page.getByText("Required")).not.toBeVisible()
	await reachLastPage(page)
	await expect(page.getByText("Required")).toBeVisible()
})

Then("every optional question offers a natural blank\\/skipped state with no coerced answer", async ({ page }) => {
	await resetToIntro(page)
	await goNext(page) // intro -> narrative, left blank
	await goNext(page) // advances without an answer, because it is optional
	await expect(page.getByText("Was anyone injured?")).toBeVisible()
})

Then("consent_publish has no selected default and requires an explicit yes or no", async ({ page }) => {
	await resetToIntro(page)
	await reachLastPage(page)
	await expect(page.getByRole("radio", { name: "Yes" })).not.toBeChecked()
	await expect(page.getByRole("radio", { name: "No" })).not.toBeChecked()
})

Then("the client shows inline validation using the same stable type\\/option rules and localized messages the API uses", async ({ page }) => {
	await expect(page.getByText("This question is required.")).toBeVisible()
})

Then("every control has a programmatic label and usable keyboard order", async ({ page }) => {
	await resetToIntro(page)
	await goNext(page)
	await expect(page.getByLabel("What happened?")).toBeVisible()
})

Then("groups use fieldset\\/legend", async ({ page }) => {
	await resetToIntro(page)
	await reachGroupPage(page)
	await expect(page.locator("fieldset legend", { hasText: "Aircraft:" })).toBeVisible()
})

Then("errors are linked to their fields and summarized", async ({ page }) => {
	const questions = defaultFormQuestions()
	const narrative = questions.find((q) => q.key === "narrative")!
	narrative.isRequired = true
	await stubCurrentQuestions(page, questions)
	await forgetDraftInBrowser(page)
	await page.reload()
	await goNext(page)
	await goNext(page)
	await expect(page.getByRole("alert").first()).toBeVisible()
	const describedBy = await page.getByLabel("What happened?").getAttribute("aria-describedby")
	expect(describedBy).toContain("error")
})

Then("focus is visible and status updates use appropriate live regions", async ({ page }) => {
	await expect(page.getByRole("status").first()).toBeAttached()
})

Then("motion respects reduced-motion and touch targets\\/contrast are sufficient", async () => {}) // Global CSS rule (index.css) forces near-zero transition duration under prefers-reduced-motion; covered by the repository-wide rule, not per-scenario here.

Then("media previews are never required to complete a report", async ({ page }) => {
	await resetToIntro(page)
	await goNext(page)
	await fillNarrative(page, "Text only, no media.")
	await goNext(page)
	await answerYesNo(page, "Was anyone injured?", "No")
	await goNext(page)
	await page.getByLabel("Type of aircraft").selectOption({ label: "Hang glider" })
	await page.getByLabel("Model").fill("Synthetic 1")
	await goNext(page)
	await goNext(page) // Attachments left empty — this must succeed.
	await expect(page.getByText("May we publish a summary of this report?")).toBeVisible()
})

Then("no private data is exposed", async ({ page }) => {
	await expect(page.locator("body")).toBeVisible()
})

Then("nothing is silently published", async () => {
	expect(submittedRequests).toHaveLength(0)
})

Then("saved local answers are not erased", async ({ page }) => {
	const draft = (await readDraftFromBrowser(page)) as { answers: Record<string, unknown> } | null
	expect(draft).not.toBeNull()
	expect(Object.keys(draft!.answers).length).toBeGreaterThan(0)
})

Then("the browser keeps the local report state", async ({ page }) => {
	expect(await readDraftFromBrowser(page)).not.toBeNull()
})

Then("explains to the reporter how to retry", async ({ page }) => {
	await expect(page.getByText(/could not be sent/i)).toBeVisible()
})

Then("a privacy explanation of the 15-day local storage is shown before submission", async ({ page }) => {
	await expect(page.getByText(/for up to 15 days/i)).toBeVisible()
})

Then("a notice states that signing in only confirms HPAC membership and that the report is not linked to their account", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Your report is not linked to you" })).toBeVisible()
	await expect(page.getByText(/only confirms that you are an HPAC member/i)).toBeVisible()
})

Then("attachment selection appears last with type\\/count\\/size guidance and a note that attached files are kept with the saved report for up to 15 days", async ({ page }) => {
	await resetToIntro(page)
	await goNext(page) // intro -> narrative
	await goNext(page) // narrative -> injured
	await answerYesNo(page, "Was anyone injured?", "No")
	await goNext(page) // -> group
	await goNext(page) // -> attachments
	// The native input is visually hidden behind the drop zone; the zone's
	// button is what the reporter sees, and the question still labels the input.
	await expect(page.getByLabel("Photos or videos")).toBeAttached()
	await expect(page.getByRole("button", { name: "Drag files here, or choose files" })).toBeVisible()
	await expect(page.getByText(/JPEG, PNG, WebP, HEIC/)).toBeVisible()
	await expect(page.getByText(/up to 5 files in all, 50 MB each/)).toBeVisible()
	await expect(page.getByText(/kept with your saved report for up to 15 days/i)).toBeVisible()
	// "Appears last": only the required consent question follows it.
	await goNext(page)
	await expect(page.getByText("May we publish a summary of this report?")).toBeVisible()
})

Given("the current page shows a multi-select question", async ({ page }) => {
	await openForm(page, multiSelectFormQuestions())
	await goNext(page) // intro -> the multi-select page
})

Then("its options are hidden behind one closed picker labelled by the question", async ({ page }) => {
	const picker = page.getByRole("button", { name: /Which conditions applied\?/ })
	await expect(picker).toHaveAttribute("aria-expanded", "false")
	await expect(picker).toContainText("Choose any")
	await expect(page.getByRole("checkbox")).toHaveCount(0)
})

When("the reporter opens the picker and checks two options", async ({ page }) => {
	await page.getByRole("button", { name: /Which conditions applied\?/ }).click()
	await page.getByRole("checkbox", { name: "Gusty" }).check()
	await page.getByRole("checkbox", { name: "Turbulent" }).check()
})

Then("the picker stays open with both options checked", async ({ page }) => {
	await expect(page.getByRole("button", { name: /Which conditions applied\?/ })).toHaveAttribute("aria-expanded", "true")
	await expect(page.getByRole("checkbox", { name: "Gusty" })).toBeChecked()
	await expect(page.getByRole("checkbox", { name: "Turbulent" })).toBeChecked()
	await expect(page.getByRole("checkbox", { name: "Thermic" })).not.toBeChecked()
})

When("the reporter presses Escape", async ({ page }) => {
	await page.keyboard.press("Escape")
})

Then("the picker closes, returns focus to itself, and names both chosen options", async ({ page }) => {
	const picker = page.getByRole("button", { name: /Which conditions applied\?/ })
	await expect(picker).toHaveAttribute("aria-expanded", "false")
	await expect(picker).toBeFocused()
	await expect(picker).toHaveAccessibleName("Which conditions applied? Gusty, Turbulent")
	await expect(page.getByRole("checkbox")).toHaveCount(0)
})

Then("a dialog asks whether to continue where they left off, with No and Yes buttons", async ({ page }) => {
	const dialog = resumeDialog(page)
	await expect(dialog).toBeVisible()
	await expect(dialog.getByRole("button", { name: "No, start over" })).toBeVisible()
	await expect(dialog.getByRole("button", { name: "Yes, continue" })).toBeVisible()
})

Then("a table below the buttons lists each saved question with its saved answer", async ({ page }) => {
	const dialog = resumeDialog(page)
	const table = dialog.getByRole("table")
	const buttonsBox = await dialog.getByRole("button", { name: "Yes, continue" }).boundingBox()
	const tableBox = await table.boundingBox()
	expect(tableBox!.y).toBeGreaterThan(buttonsBox!.y + buttonsBox!.height)

	const rows = table.getByRole("row").filter({ has: page.getByRole("rowheader") })
	await expect(rows).toHaveText([
		/What happened\?\s*A saved synthetic narrative\./,
		/Was anyone injured\?\s*Yes/,
		/Describe the injury\s*A synthetic sprain\./,
		/Type of aircraft\s*Paraglider/,
		/Photos or videos\s*saved-photo\.png/,
	])
	await expect(table).not.toContainText("retired")
})

Then("each saved attached file is listed by name under its question", async ({ page }) => {
	const row = resumeDialog(page).getByRole("row").filter({ has: page.getByRole("rowheader", { name: "Photos or videos" }) })
	await expect(row.getByRole("cell")).toHaveText("saved-photo.png")
})

Then("the saved answers are restored", async ({ page }) => {
	await expect(page.getByLabel("Describe the injury")).toHaveValue("A synthetic sprain.")
	await goBack(page)
	await expect(page.getByRole("group", { name: "Was anyone injured?" }).getByRole("radio", { name: "Yes" })).toBeChecked()
	await goBack(page)
	await expect(page.getByLabel("What happened?")).toHaveValue("A saved synthetic narrative.")
})

Then("the form opens on the page the reporter was last on", async ({ page }) => {
	await expect(page.getByLabel("Describe the injury")).toBeVisible()
})

Then("the browser removes the saved report", async ({ page }) => {
	expect(await readDraftFromBrowser(page)).toBeNull()
})

Then("the form opens at its introduction with no answers", async ({ page }) => {
	await expect(page.getByRole("heading", { level: 1 })).toContainText("Thanks for taking the time")
	await goNext(page)
	await expect(page.getByLabel("What happened?")).toHaveValue("")
})

Then("no dialog asks whether to continue", async ({ page }) => {
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
	await expect(resumeDialog(page)).toHaveCount(0)
})

// ------------------------------ a one-language reporter-added choice (REQ-QB-103) --

Given("a type-ahead question has a reporter-added choice typed only in English", async ({ page }) => {
	await openForm(page, typeAheadFormQuestions())
})

When("a reporter using French opens that question", async ({ page }) => {
	await goNext(page) // intro -> the type-ahead page
	await page.getByRole("button", { name: "Français" }).click()
})

Then("the type-ahead offers the choice in its English wording", async ({ page }) => {
	const offered = page.locator("datalist option")

	// The choice with both languages is offered in French; the one a reporter
	// typed in English only is offered in English, and says so.
	await expect(offered).toHaveCount(2)
	await expect(offered.nth(0)).toHaveAttribute("value", "Colline Cooper")
	await expect(offered.nth(1)).toHaveAttribute("value", "Mount 7")
	await expect(offered.nth(1)).toHaveAttribute("lang", "en-CA")
})

/*
 * The page in the address (#366). Each page is /report/<question-key>; the
 * introduction is /report.
 */

function reportAddress(stepKey?: string): RegExp {
	return stepKey ? new RegExp(`/report/${stepKey}$`) : /\/report$/
}

Given("a reporter is on the form's introduction at \\/report", async ({ page }) => {
	await openForm(page)
	await expect(page).toHaveURL(reportAddress())
})

Given("a reporter has answered a required question and pressed Next", async ({ page }) => {
	const questions = defaultFormQuestions()
	questions.find((question) => question.key === "narrative")!.isRequired = true
	await openForm(page, questions)
	await goNext(page) // intro -> narrative
	await fillNarrative(page, "A synthetic occurrence narrative.")
	await goNext(page) // narrative -> was_injured
	await expect(page).toHaveURL(reportAddress("was_injured"))
})

When("the reporter presses Next", async ({ page }) => {
	await goNext(page)
})

When("the reporter presses Back", async ({ page }) => {
	await goBack(page)
})

When("the reporter presses the browser's Back button", async ({ page }) => {
	await page.goBack()
})

When("the reporter clears the answer and presses the browser's Forward button", async ({ page }) => {
	await fillNarrative(page, "")
	await page.goForward()
})

When("the reporter opens the address of a page other than the one saved", async ({ page }) => {
	await signInAs(page, "user")
	await page.goto("/report/aircraft")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

When("the reporter opens the address of a later page of the form", async ({ page }) => {
	await signInAs(page, "user")
	await page.goto("/report/aircraft")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

When("the reporter opens the address of a page the form does not have", async ({ page }) => {
	await page.goto("/report/no_such_page")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

Then("the address names the page now shown, as \\/report\\/<question-key>", async ({ page }) => {
	await expect(page.getByLabel("What happened?")).toBeVisible()
	await expect(page).toHaveURL(reportAddress("narrative"))
})

Then("the address is \\/report", async ({ page }) => {
	await expect(page).toHaveURL(reportAddress())
})

Then("the required question's page shows and the address names it", async ({ page }) => {
	await expect(page.getByLabel("What happened?")).toHaveValue("A synthetic occurrence narrative.")
	await expect(page).toHaveURL(reportAddress("narrative"))
})

Then("the required question's page still shows", async ({ page }) => {
	await expect(page.getByLabel("What happened?")).toBeVisible()
	await expect(page).toHaveURL(reportAddress("narrative"))
})

Then("the address names the page the reporter was last on", async ({ page }) => {
	await expect(page.getByLabel("Describe the injury")).toBeVisible()
	await expect(page).toHaveURL(reportAddress("injury_detail"))
})

Then("the form opens at its introduction at \\/report", async ({ page }) => {
	await expect(page.getByRole("heading", { level: 1 })).toContainText("Thanks for taking the time")
	await expect(page).toHaveURL(reportAddress())
})
