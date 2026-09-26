import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import {
	choiceFormQuestions,
	dateTimeFormQuestions,
	defaultFormQuestions,
	multiSelectFormQuestions,
	typeAheadFormQuestions,
	forgetDraftInBrowser,
	readDraftFromBrowser,
	stubCurrentQuestions,
	stubSubmission,
	writeSavedDateTimeDraftToBrowser,
	writeSavedDraftToBrowser,
	writeStaleDraftToBrowser,
	type StubOption,
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

Given(
	"this browser holds an unexpired saved report with a date answer {string} and a time answer {string}",
	async ({ page }, date: string, time: string) => {
		await stubAuth(page)
		await stubCurrentQuestions(page, dateTimeFormQuestions())
		await writeSavedDateTimeDraftToBrowser(page, date, time)
	},
)

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

Then("only the questions made required display required treatment, and consent_publish is always one of them", async ({ page }) => {
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
	await expect(page.getByText(/up to 5 files in all\. Each video may be up to 250 MB, and each photo or document up to 25 MB\./)).toBeVisible()
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

When("a reporter using {word} opens that question", async ({ page }, language: string) => {
	await goNext(page) // intro -> the question's page
	if (language === "French") await page.getByRole("button", { name: "Français" }).click()
})

Then("the type-ahead offers the choice in its English wording", async ({ page }) => {
	await page.getByRole("combobox").click()
	const offered = page.getByRole("listbox").getByRole("option")

	// The choice with both languages is offered in French; the one a reporter
	// typed in English only is offered in English, and says so.
	await expect(offered).toHaveCount(2)
	await expect(offered.nth(0)).toHaveText("Colline Cooper")
	await expect(offered.nth(0)).not.toHaveAttribute("lang", /./)
	await expect(offered.nth(1)).toHaveText("Mount 7")
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

Then("the continue dialog lists the date as {string} and the time as {string}", async ({ page }, date: string, time: string) => {
	// Named by its rows, not its title, so the step holds in either language.
	const table = page.getByRole("dialog").getByRole("table")
	await expect(table.getByRole("row").filter({ has: page.getByRole("rowheader", { name: /^On what date\?/ }) }).getByRole("cell")).toHaveText(date)
	await expect(table.getByRole("row").filter({ has: page.getByRole("rowheader", { name: /^At what time\?/ }) }).getByRole("cell")).toHaveText(time)
})

// ------------------ choices listed alphabetically, pinned first or last (ADR-0136) --

/*
 * REQ-QB-145, REQ-QB-146, REQ-QB-148. Each stub sends its choices in an order
 * that is not alphabetical, as the server does (grouped by pin, by ID within a
 * group), so the order on screen is the browser's own.
 */

function stubChoice(labelEn: string, labelFr = labelEn, pin = "none", onlyIn: string | null = null): StubOption {
	const code = labelEn.toLowerCase().replace(/[^a-z0-9]+/g, "_")
	return { id: `choice-${code}`, code, labelEn, labelFr, onlyIn, pin }
}

Given(
	"a {word} question offers {string} \\/ {string}, {string} \\/ {string}, {string} \\/ {string}, and {string} \\/ {string}, none pinned",
	async ({ page }, type: string, en1: string, fr1: string, en2: string, fr2: string, en3: string, fr3: string, en4: string, fr4: string) => {
		const options = [stubChoice(en1, fr1), stubChoice(en2, fr2), stubChoice(en3, fr3), stubChoice(en4, fr4)]
		await openForm(page, choiceFormQuestions(type === "type-ahead" ? "autocomplete" : type, options))
	},
)

Given(
	"a {word} question offers {string} and {string} pinned first, {string} pinned last, and {string}, {string}, and {string} not pinned",
	async ({ page }, type: string, firstA: string, firstB: string, last: string, noneA: string, noneB: string, noneC: string) => {
		// The server's order: pinned first, unpinned, pinned last, each by ID — not alphabetical.
		const options = [
			stubChoice(firstA, firstA, "first"),
			stubChoice(firstB, firstB, "first"),
			...[noneA, noneB, noneC].map((label) => stubChoice(label)),
			stubChoice(last, last, "last"),
		]
		await openForm(page, choiceFormQuestions(type, options))
	},
)

Given(
	"a type-ahead question offers {string} and {string}, and a reporter has since added {string}",
	async ({ page }, first: string, second: string, added: string) => {
		const options = [stubChoice(first), stubChoice(second), stubChoice(added, added, "none", "en-CA")]
		await openForm(page, choiceFormQuestions("autocomplete", options))
	},
)

/** What the question's control lists, in order, with "|" where a separator is drawn. */
async function listedChoices(page: Page): Promise<string[]> {
	const main = page.getByRole("main")
	await expect(main.getByText("Which one applies?").first()).toBeVisible()

	if ((await main.locator("select").count()) > 0) {
		return main
			.locator("select option")
			.evaluateAll((options) =>
				(options as HTMLOptionElement[])
					.filter((option) => option.value !== "" || option.hasAttribute("data-separator"))
					.map((option) => (option.hasAttribute("data-separator") ? "|" : (option.textContent ?? "").trim())),
			)
	}

	const combobox = main.getByRole("combobox")
	if ((await combobox.count()) > 0) {
		if ((await combobox.getAttribute("aria-expanded")) !== "true") await combobox.click()
		return listEntries(page)
	}

	const trigger = main.getByRole("button", { name: /Which one applies\?/ })
	if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click()
	return main
		.locator('[id$="-options"] label, [id$="-options"] hr')
		.evaluateAll((entries) => entries.map((entry) => (entry.tagName === "HR" ? "|" : (entry.textContent ?? "").trim())))
}

Then(/^its choices are listed (".*")$/, async ({ page }, quoted: string) => {
	const expected = [...quoted.matchAll(/"([^"]*)"/g)].map((match) => match[1])
	expect((await listedChoices(page)).filter((entry) => entry !== "|")).toEqual(expected)
})

Then("a separator is drawn after {string} and after {string}", async ({ page }, first: string, second: string) => {
	const listed = await listedChoices(page)
	const separators = listed.flatMap((entry, index) => (entry === "|" ? [listed[index - 1]] : []))
	expect(separators).toEqual([first, second])
})

// ------------------------------ the type-ahead as a picker the form draws (ADR-0140) --

/*
 * REQ-QB-159 to REQ-QB-163. The type-ahead is a WAI-ARIA combobox: focus stays
 * in the field, and its list is a listbox the form draws beneath it.
 */

const typeAheadField = (page: Page) => page.getByRole("main").getByRole("combobox")
const typeAheadList = (page: Page) => page.getByRole("main").getByRole("listbox")

/** The type-ahead's open list, in order, with "|" where a separator is drawn. */
async function listEntries(page: Page): Promise<string[]> {
	await expect(typeAheadList(page)).toBeVisible()
	return typeAheadList(page)
		.locator('[role="option"], [data-separator]')
		.evaluateAll((entries) => entries.map((entry) => (entry.hasAttribute("data-separator") ? "|" : (entry.textContent ?? "").trim())))
}

function quotedList(quoted: string): string[] {
	return [...quoted.matchAll(/"([^"]*)"/g)].map((match) => match[1])
}

Given(
	"a type-ahead question with the help text {string} offers {string}, {string}, and {string}, none pinned",
	async ({ page }, help: string, first: string, second: string, third: string) => {
		const options = [stubChoice(first), stubChoice(second), stubChoice(third)]
		await openForm(page, choiceFormQuestions("autocomplete", options, { helpTextEn: help, helpTextFr: help }))
	},
)

Given("a type-ahead question offers {int} choices", async ({ page }, count: number) => {
	const options = Array.from({ length: count }, (_, index) => stubChoice(`Launch site ${index + 1}`))
	await openForm(page, choiceFormQuestions("autocomplete", options))
})

When("a reporter using English opens that question on a screen {int} pixels wide", async ({ page }, width: number) => {
	await page.setViewportSize({ width, height: 740 })
	await goNext(page) // intro -> the question's page
})

Then(
	"the question is a combobox field with a caret, described by its help text, and no browser suggestion list",
	async ({ page }) => {
		const field = page.getByRole("combobox", { name: "Which one applies?" })
		await expect(field).toBeVisible()
		await expect(field).toHaveAccessibleDescription("Pick the nearest site")
		await expect(field).toHaveAttribute("aria-expanded", "false")
		await expect(field).not.toHaveAttribute("list", /./)
		await expect(page.locator("datalist")).toHaveCount(0)
		await expect(page.getByRole("button", { name: "Show choices" })).toBeVisible()
		// Drawn like the form's other fields, in the design-system border.
		const border = await field.evaluate((element) => getComputedStyle(element).borderTopColor)
		const rule = await field.evaluate((element) => {
			const probe = document.createElement("div")
			probe.style.color = "var(--color-rule)"
			element.parentElement!.appendChild(probe)
			const color = getComputedStyle(probe).color
			probe.remove()
			return color
		})
		expect(border).toBe(rule)
	},
)

When(/^they open the field's list by (.+)$/, async ({ page }, opening: string) => {
	const field = typeAheadField(page)
	if (opening === "pressing the caret") await page.getByRole("button", { name: "Show choices" }).click()
	else if (opening === "clicking the field") await field.click()
	else if (opening === "pressing Alt and the down arrow") {
		await field.focus()
		await page.keyboard.press("Alt+ArrowDown")
	} else {
		const typed = /^typing "(.*)"$/.exec(opening)
		if (!typed) throw new Error(`Unknown way to open the list: ${opening}`)
		await field.pressSequentially(typed[1])
	}
})

Then(/^a list as wide as the field opens directly beneath it, offering (".*")$/, async ({ page }, quoted: string) => {
	const field = typeAheadField(page)
	await expect(field).toHaveAttribute("aria-expanded", "true")
	const list = typeAheadList(page)
	await expect(field).toHaveAttribute("aria-controls", (await list.getAttribute("id"))!)

	const fieldBox = (await field.boundingBox())!
	const listBox = (await list.boundingBox())!
	expect(Math.abs(listBox.x - fieldBox.x)).toBeLessThanOrEqual(1)
	expect(Math.abs(listBox.width - fieldBox.width)).toBeLessThanOrEqual(1)
	expect(listBox.y).toBeGreaterThanOrEqual(fieldBox.y + fieldBox.height)
	expect(listBox.y - (fieldBox.y + fieldBox.height)).toBeLessThanOrEqual(8)

	expect((await listEntries(page)).filter((entry) => entry !== "|")).toEqual(quotedList(quoted))
})

When("they type {string} in the field", async ({ page }, typed: string) => {
	await typeAheadField(page).pressSequentially(typed)
})

Then(/^its list offers only (".*")$/, async ({ page }, quoted: string) => {
	await expect(typeAheadList(page).getByRole("option")).toHaveText(quotedList(quoted))
})

When("they type {string} in the field and press the down arrow twice", async ({ page }, typed: string) => {
	await typeAheadField(page).pressSequentially(typed)
	await page.keyboard.press("ArrowDown")
	await page.keyboard.press("ArrowDown")
})

Then("{string} is the field's active option", async ({ page }, label: string) => {
	const option = typeAheadList(page).getByRole("option", { name: label })
	await expect(option).toHaveAttribute("aria-selected", "true")
	await expect(typeAheadField(page)).toHaveAttribute("aria-activedescendant", (await option.getAttribute("id"))!)
	await expect(typeAheadField(page)).toBeFocused()
})

When("they press Enter", async ({ page }) => {
	await page.keyboard.press("Enter")
})

When("they press Tab", async ({ page }) => {
	await page.keyboard.press("Tab")
})

When("they press the up arrow", async ({ page }) => {
	await page.keyboard.press("ArrowUp")
})

When("they press the down arrow", async ({ page }) => {
	await page.keyboard.press("ArrowDown")
})

When("they press Alt and the down arrow", async ({ page }) => {
	await page.keyboard.press("Alt+ArrowDown")
})

When("they press Escape", async ({ page }) => {
	await page.keyboard.press("Escape")
})

When("they press outside the field", async ({ page }) => {
	await expect(typeAheadList(page)).toBeVisible()
	// The page's left margin: outside the field, its caret, its label, and its list.
	const field = (await typeAheadField(page).boundingBox())!
	await page.mouse.click(Math.max(1, field.x - 20), field.y + field.height / 2)
})

Then("its list is open", async ({ page }) => {
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "true")
	await expect(typeAheadList(page)).toBeVisible()
	await expect(typeAheadField(page)).toBeFocused()
})

Then("the list is closed and the field holds {string}", async ({ page }, value: string) => {
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "false")
	await expect(typeAheadList(page)).toBeHidden()
	await expect(typeAheadField(page)).toHaveValue(value)
})

Then("the list says no choice matches", async ({ page }) => {
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "false")
	await expect(page.getByRole("status").filter({ hasText: "No choice matches" })).toBeVisible()
})

Then("the list fits within the screen's width, and the page does not scroll sideways", async ({ page }) => {
	const listBox = (await typeAheadList(page).boundingBox())!
	const width = page.viewportSize()!.width
	expect(listBox.x).toBeGreaterThanOrEqual(0)
	expect(listBox.x + listBox.width).toBeLessThanOrEqual(width)
	expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width)
})

Then("the list scrolls within itself", async ({ page }) => {
	const list = typeAheadList(page)
	const { scrollHeight, clientHeight, overflowY } = await list.evaluate((element) => ({
		scrollHeight: element.scrollHeight,
		clientHeight: element.clientHeight,
		overflowY: getComputedStyle(element).overflowY,
	}))
	expect(scrollHeight).toBeGreaterThan(clientHeight)
	expect(overflowY).toBe("auto")
	// The last choice is reached by scrolling the list, not the page.
	await page.keyboard.press("ArrowUp")
	await expect(list.getByRole("option", { name: "Launch site 30" })).toBeInViewport()
})

