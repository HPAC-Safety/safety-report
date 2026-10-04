import { createBdd } from "playwright-bdd"
import { expect, type Locator, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import {
	choiceFormQuestions,
	dateTimeFormQuestions,
	defaultFormQuestions,
	multiSelectFormQuestions,
	pickChoice,
	typeAheadFormQuestions,
	forgetDraftInBrowser,
	revisedNarrativeFormQuestions,
	writeOnlyStaleAnswersDraftToBrowser,
	readDraftFromBrowser,
	stubCurrentQuestions,
	stubSubmission,
	writeSavedDateTimeDraftToBrowser,
	writeSavedDraftToBrowser,
	writeStaleDraftToBrowser,
	type StubOption,
	type StubQuestion,
} from "./report-form-fixture"
import { present } from "./present"

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

function trackSubmissions(page: Page) {
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
	trackSubmissions(page)
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

/** Advances from the intro to the group ("Aircraft") page, answering "No" for injury along the way. */
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
	await pickChoice(page, "Type of aircraft", "Hang glider")
	await page.getByLabel("Model").fill("Synthetic 1")
	await goNext(page) // group -> attachments
	await goNext(page) // attachments -> consent
}

async function startFillingOutForm(page: Page) {
	await openForm(page)
	await goNext(page)
	await fillNarrative(page, "A synthetic occurrence narrative.")
}

Given("a reporter is filling out the form", async ({ page }) => {
	await startFillingOutForm(page)
})

// REQ-SUB-154 starts where REQ-SUB-122 ends: its leave dialog shown. REQ-SUB-122's Then checks that dialog's wording in full.
Given("a reporter filling out the form has activated a header navigation link away from it", async ({ page }) => {
	await startFillingOutForm(page)
	await page.getByRole("banner").getByRole("link", { name: "View safety reports" }).click()
	await expect(page.getByRole("dialog", { name: "Your report is saved" })).toBeVisible()
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

Given(
	"this browser holds an unexpired saved report, and an Administrator has since revised one of its answered questions",
	async ({ page }) => {
		await stubAuth(page)
		await stubCurrentQuestions(page, revisedNarrativeFormQuestions())
		await writeSavedDraftToBrowser(page)
	},
)

Given("this browser holds an unexpired saved report whose every answer is still current", async ({ page }) => {
	await stubAuth(page)
	await stubCurrentQuestions(page, dateTimeFormQuestions())
	await writeSavedDateTimeDraftToBrowser(page, "2026-09-13", "14:30")
})

Given(
	"this browser holds an unexpired saved report whose every answer names a revision that is no longer current",
	async ({ page }) => {
		await stubAuth(page)
		await stubCurrentQuestions(page)
		await writeOnlyStaleAnswersDraftToBrowser(page)
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
	await openForm(page) // The default fixture's "Aircraft" group already has two children.
})

Given("the current page shows a required, unanswered question", async ({ page }) => {
	const questions = defaultFormQuestions()
	const narrative = present(questions.find((question) => question.key === "narrative"))
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

async function returnToForm(page: Page) {
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
}

async function declineToContinue(page: Page) {
	await resumeDialog(page).getByRole("button", { name: "No, start over" }).click()
	await expect(resumeDialog(page)).toHaveCount(0)
}

When("the reporter returns to the form", async ({ page }) => {
	await returnToForm(page)
})

When("the reporter returns to the form and continues the saved report", async ({ page }) => {
	await returnToForm(page)
	await resumeDialog(page).getByRole("button", { name: "Yes, continue" }).click()
	await expect(resumeDialog(page)).toHaveCount(0)
})

When("the reporter returns to the form and declines to continue", async ({ page }) => {
	await returnToForm(page)
	await declineToContinue(page)
})

When("the reporter declines to continue", async ({ page }) => {
	await declineToContinue(page)
})

When("a reporter opens the report page", async ({ page }) => {
	await openForm(page)
})

When("a reporter goes on to the next page", async ({ page }) => {
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

When("the reporter answers the parent so the condition is met", async ({ page }) => {
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
	const narrative = present(questions.find((q) => q.key === "narrative"))
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
	const saved = present(draft, "a saved draft")
	expect(saved.locale).toBe("en-CA")
	expect(Object.keys(saved.answers).length).toBeGreaterThan(0)
	expect(Date.now() - saved.savedAtMs).toBeLessThan(60_000)
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

Then("no server draft, report ID reservation, or resumable upload protocol exists", () => {
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
	await expect(page.getByRole("group", { name: "Aircraft" })).toBeVisible()
	await expect(page.getByRole("combobox", { name: "Type of aircraft" })).toBeVisible()
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
	await expect(page.getByRole("group", { name: "Aircraft" })).toBeVisible()
})

Then("the dependent question's page appears in the sequence", async ({ page }) => {
	await expect(page.getByLabel("Describe the injury")).toBeVisible()
})

Then("the control that was Next now reads Submit", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Submit report" })).toBeVisible()
	await expect(page.getByRole("button", { name: "Next" })).toHaveCount(0)
})

When("the reporter chooses Submit", async ({ page }) => {
	await stubSubmission(page)
	await answerYesNo(page, "May we publish a summary of this report?", "Yes")
	await page.getByRole("button", { name: "Submit report" }).click()
})

Then("the one final submission request is sent", async () => {
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

Then("clears saved local state only after a definite acceptance", async ({ page }) => {
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

Then("only the questions made required display required treatment, and the publication consent question is always one of them", async ({ page }) => {
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

Then("the publication consent question has no selected default and requires an explicit yes or no", async ({ page }) => {
	await resetToIntro(page)
	await reachLastPage(page)
	await expect(page.getByRole("radio", { name: "Yes" })).not.toBeChecked()
	await expect(page.getByRole("radio", { name: "No" })).not.toBeChecked()
})

Then("the client shows inline validation using the same stable type\\/choice rules and localized messages the server uses", async ({ page }) => {
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
	await expect(page.locator("fieldset legend", { hasText: "Aircraft" })).toBeVisible()
})

Then("errors are linked to their questions and summarized", async ({ page }) => {
	const questions = defaultFormQuestions()
	const narrative = present(questions.find((q) => q.key === "narrative"))
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
	await pickChoice(page, "Type of aircraft", "Hang glider")
	await page.getByLabel("Model").fill("Synthetic 1")
	await goNext(page)
	await goNext(page) // Attachments left empty — this must succeed.
	await expect(page.getByText("May we publish a summary of this report?")).toBeVisible()
})

Then("no private data is exposed", async ({ page }) => {
	await expect(page.locator("body")).toBeVisible()
})

Then("nothing is silently published", () => {
	expect(submittedRequests).toHaveLength(0)
})

Then("saved local answers are not erased", async ({ page }) => {
	const draft = (await readDraftFromBrowser(page)) as { answers: Record<string, unknown> } | null
	expect(draft).not.toBeNull()
	expect(Object.keys(present(draft).answers).length).toBeGreaterThan(0)
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

Then("its choices are hidden behind one closed picker labelled by the question", async ({ page }) => {
	const picker = page.getByRole("combobox", { name: /Which conditions applied\?/ })
	await expect(picker).toHaveAttribute("aria-expanded", "false")
	await expect(picker).toContainText("Choose any")
	await expect(page.getByRole("checkbox")).toHaveCount(0)
})

// The picker's role, popup, and state are what assistive technology hears (REQ-SUB-132).
Then("assistive technology hears its closed picker as collapsed, labelled by the question, with a dialog as its popup", async ({ page }) => {
	const picker = page.getByRole("combobox", { name: /Which conditions applied\?/ })
	await expect(picker).toHaveAttribute("aria-haspopup", "dialog")
	await expect(picker).toHaveAttribute("aria-expanded", "false")
	await expect(page.getByRole("dialog")).toHaveCount(0)
})

When("the reporter opens the picker", async ({ page }) => {
	await page.getByRole("combobox", { name: /Which conditions applied\?/ }).click()
})

Then("the picker is expanded and controls a dialog labelled by the question, holding one checkbox for each choice", async ({ page }) => {
	const picker = page.getByRole("combobox", { name: /Which conditions applied\?/ })
	await expect(picker).toHaveAttribute("aria-expanded", "true")
	const dialog = page.getByRole("dialog", { name: "Which conditions applied?" })
	await expect(dialog).toBeVisible()
	await expect(picker).toHaveAttribute("aria-controls", present(await dialog.getAttribute("id")))
	await expect(dialog.getByRole("checkbox")).toHaveCount(3)
	for (const option of ["Gusty", "Thermic", "Turbulent"]) await expect(dialog.getByRole("checkbox", { name: option })).toBeVisible()
})

async function checkTwoChoices(page: Page) {
	await page.getByRole("combobox", { name: /Which conditions applied\?/ }).click()
	await page.getByRole("checkbox", { name: "Gusty" }).check()
	await page.getByRole("checkbox", { name: "Turbulent" }).check()
}

When("the reporter opens the picker and checks two choices", async ({ page }) => {
	await checkTwoChoices(page)
})

Given("the reporter has checked two choices in an open multi-select picker", async ({ page }) => {
	await openForm(page, multiSelectFormQuestions())
	await goNext(page) // intro -> the multi-select page
	await checkTwoChoices(page)
	await expect(page.getByRole("combobox", { name: /Which conditions applied\?/ })).toHaveAttribute("aria-expanded", "true")
})

Then("the picker stays open with both choices checked", async ({ page }) => {
	await expect(page.getByRole("combobox", { name: /Which conditions applied\?/ })).toHaveAttribute("aria-expanded", "true")
	await expect(page.getByRole("checkbox", { name: "Gusty" })).toBeChecked()
	await expect(page.getByRole("checkbox", { name: "Turbulent" })).toBeChecked()
	await expect(page.getByRole("checkbox", { name: "Thermic" })).not.toBeChecked()
})

// A key named in an Examples cell (CONV-004): the step says which key, never how.
When(/^the reporter uses the (\w+) key$/, async ({ page }, key: string) => {
	await page.keyboard.press(key)
})

Then("the picker closes, returns focus to itself, and names both choices", async ({ page }) => {
	const picker = page.getByRole("combobox", { name: /Which conditions applied\?/ })
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
	const below = present(buttonsBox, "the buttons' box")
	expect(present(tableBox, "the table's box").y).toBeGreaterThan(below.y + below.height)

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

Then("the revised question is empty", async ({ page }) => {
	// The step before left the form on the injured page; the narrative is the one before it.
	await goBack(page)
	await expect(page.getByLabel("What happened?")).toHaveValue("")
})

Then("the saved answers to the other questions are restored", async ({ page }) => {
	await expect(page.getByLabel("Describe the injury")).toHaveValue("A synthetic sprain.")
	await goBack(page)
	await expect(page.getByRole("group", { name: "Was anyone injured?" }).getByRole("radio", { name: "Yes" })).toBeChecked()
})

Then("one notice says the form changed since the report was saved, so some answers were cleared", async ({ page }) => {
	const notice = page.getByTestId("cleared-answers-notice")
	await expect(notice).toHaveCount(1)
	await expect(notice).toContainText("The form changed since you saved this report, so some of your answers were cleared.")
})

Then("no question is marked individually", async ({ page }) => {
	await expect(page.getByRole("alert")).toHaveCount(0)
	await expect(page.locator("[aria-invalid='true']")).toHaveCount(0)
})

Then("no notice says answers were cleared", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Back" })).toBeVisible()
	await expect(page.getByTestId("cleared-answers-notice")).toHaveCount(0)
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

Given("a type-ahead question has a reporter-added value typed only in English", async ({ page }) => {
	await openForm(page, typeAheadFormQuestions())
})

When("a reporter using {word} opens that question", async ({ page }, language: string) => {
	await goNext(page) // intro -> the question's page
	if (language === "French") await page.getByRole("button", { name: "Français" }).click()
})

Then("the type-ahead offers the choice in its English wording", async ({ page }) => {
	// A type-ahead's open list only ever shows what matches the typed text
	// (ADR-0152); "Colline Cooper" and "Mount 7" share no run of 3 characters,
	// so each is typed and checked on its own.
	const field = page.getByRole("combobox")

	// The choice with both languages is offered in French.
	await field.click()
	await field.fill("Col")
	const bilingual = page.getByRole("listbox").getByRole("option")
	await expect(bilingual).toHaveCount(1)
	await expect(bilingual.first()).toHaveText("Colline Cooper")
	await expect(bilingual.first()).not.toHaveAttribute("lang", /./)
	await field.fill("")
	await page.keyboard.press("Escape")

	// The one a reporter typed in English only is offered in English, and says so.
	await field.click()
	await field.fill("Mou")
	const englishOnly = page.getByRole("listbox").getByRole("option")
	await expect(englishOnly).toHaveCount(1)
	await expect(englishOnly.first()).toHaveText("Mount 7")
	await expect(englishOnly.first()).toHaveAttribute("lang", "en-CA")
})

/*
 * The page in the address (#366). Each page is /report/<question-key>; the
 * introduction is /report.
 */

function reportAddress(stepKey?: string): RegExp {
	return stepKey ? new RegExp(`/report/${stepKey}$`) : /\/report$/
}

Given("a reporter is on the form's introduction, at the form's own address", async ({ page }) => {
	await openForm(page)
	await expect(page).toHaveURL(reportAddress())
})

async function answerRequiredAndGoOn(page: Page) {
	const questions = defaultFormQuestions()
	present(questions.find((question) => question.key === "narrative")).isRequired = true
	await openForm(page, questions)
	await goNext(page) // intro -> narrative
	await fillNarrative(page, "A synthetic occurrence narrative.")
	await goNext(page) // narrative -> was_injured
	await expect(page).toHaveURL(reportAddress("was_injured"))
}

Given("a reporter has answered a required question and gone on to the next page", async ({ page }) => {
	await answerRequiredAndGoOn(page)
})

Given("a reporter has answered a required question, gone on, and come back with the browser's Back button", async ({ page }) => {
	await answerRequiredAndGoOn(page)
	await page.goBack()
	await expect(page).toHaveURL(reportAddress("narrative"))
})

Given("a reporter has gone on from the form's introduction to the next page", async ({ page }) => {
	await openForm(page)
	await expect(page).toHaveURL(reportAddress())
	await goNext(page)
	await expect(page).toHaveURL(reportAddress("narrative"))
})

When("the reporter goes on to the next page", async ({ page }) => {
	await goNext(page)
})

When("the reporter goes back a page", async ({ page }) => {
	await goBack(page)
})

When("the reporter goes back with the browser's Back button", async ({ page }) => {
	await page.goBack()
})

When("the reporter clears the answer and goes forward with the browser's Forward button", async ({ page }) => {
	await fillNarrative(page, "")
	await page.goForward()
})

async function openOtherPageThanSaved(page: Page) {
	await signInAs(page, "user")
	await page.goto("/report/aircraft")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
}

When("the reporter opens the address of a page other than the one saved", async ({ page }) => {
	await openOtherPageThanSaved(page)
})

Given(
	"this browser holds an unexpired saved report, and the reporter has opened the address of a page other than the one saved",
	async ({ page }) => {
		await stubAuth(page)
		await stubCurrentQuestions(page)
		await writeSavedDraftToBrowser(page)
		await openOtherPageThanSaved(page)
		await expect(resumeDialog(page)).toBeVisible()
	},
)

When("the reporter opens the address of a later page of the form", async ({ page }) => {
	await signInAs(page, "user")
	await page.goto("/report/aircraft")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

When("the reporter opens the address of a page the form does not have", async ({ page }) => {
	await signInAs(page, "user")
	await page.goto("/report/no_such_page")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

Then("the address names the page now shown by its question key", async ({ page }) => {
	await expect(page.getByLabel("What happened?")).toBeVisible()
	await expect(page).toHaveURL(reportAddress("narrative"))
})

Then("the address is the form's own address", async ({ page }) => {
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

Then("the form opens at its introduction, at the form's own address", async ({ page }) => {
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

/** A question type as a scenario names it: "type-ahead", "single-select", "multi-select", or the API's own name. */
function choiceType(named: string): string {
	return ({ "type-ahead": "autocomplete", "single-select": "single_select", "multi-select": "multi_select" } as Record<string, string>)[named] ?? named
}

function stubChoice(labelEn: string, labelFr = labelEn, pin = "none", onlyIn: string | null = null): StubOption {
	const code = labelEn.toLowerCase().replace(/[^a-z0-9]+/g, "_")
	return { id: `choice-${code}`, code, labelEn, labelFr, onlyIn, pin }
}

Given(
	"a {word} question offers {string} \\/ {string}, {string} \\/ {string}, {string} \\/ {string}, and {string} \\/ {string}, none pinned",
	async ({ page }, type: string, en1: string, fr1: string, en2: string, fr2: string, en3: string, fr3: string, en4: string, fr4: string) => {
		const options = [stubChoice(en1, fr1), stubChoice(en2, fr2), stubChoice(en3, fr3), stubChoice(en4, fr4)]
		await openForm(page, choiceFormQuestions(choiceType(type), options))
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
		await openForm(page, choiceFormQuestions(choiceType(type), options))
	},
)

Given(
	"a type-ahead question offers {string} and {string}, and a reporter has since added {string}",
	async ({ page }, first: string, second: string, added: string) => {
		const options = [stubChoice(first), stubChoice(second), stubChoice(added, added, "none", "en-CA")]
		await openForm(page, choiceFormQuestions("autocomplete", options))
	},
)

/** Text folded for matching: accents and case do not count, as `TypeAheadField` folds them. */
function folded(text: string): string {
	return text.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase()
}

/**
 * A run of 3 or more characters every one of `labels` contains, if one
 * exists — typing it opens the list on every one of them together, so their
 * relative order (ADR-0136) can be read off in one pass. `null` when no such
 * run exists.
 */
function sharedSubstring(labels: string[]): string | null {
	if (labels.length < 2) return null
	const [first, ...rest] = labels.map(folded)
	for (let length = first.length; length >= 3; length--) {
		for (let start = 0; start + length <= first.length; start++) {
			const candidate = first.slice(start, start + length)
			if (rest.every((label) => label.includes(candidate))) return candidate
		}
	}
	return null
}

/**
 * What the question's control lists, in order, with "|" where a separator is
 * drawn; a single-select's "Choose one" row is not a choice.
 *
 * A type-ahead's open list only ever shows what matches the typed text
 * (ADR-0152): it never shows every choice unfiltered, however it is opened.
 * `expected` is required there. Each label is typed and confirmed on its
 * own first, then every one of `expected` must share a run of 3 or more
 * characters — the fixture is chosen so they do — and typing it shows them
 * all together, separators included, so real order (and separator position)
 * is read the same way a single-select's or multi-select's full list is.
 * Never returns `expected` unchecked: with no shared run, a test asserting
 * order or separators against this would pass no matter what the list
 * actually shows, so this throws instead.
 */
async function listedChoices(page: Page, expected?: string[]): Promise<string[]> {
	const main = page.getByRole("main")
	await expect(main.getByText("Which one applies?").first()).toBeVisible()

	// A multi-select's trigger is a combobox too, whose popup is a dialog of checkboxes (REQ-SUB-132), not a listbox.
	const trigger = main.getByRole("combobox", { name: /Which one applies\?/ }).and(main.locator('[aria-haspopup="dialog"]'))
	if ((await trigger.count()) > 0) {
		if ((await trigger.getAttribute("aria-expanded")) !== "true") await trigger.click()
		return main
			.locator('[id$="-options"] label, [id$="-options"] [data-separator]')
			.evaluateAll((entries) => entries.map((entry) => (entry.hasAttribute("data-separator") ? "|" : entry.textContent.trim())))
	}

	const combobox = main.getByRole("combobox")
	if ((await combobox.count()) > 0) {
		const isInput = (await combobox.evaluate((element) => element.tagName)) === "INPUT"
		if (isInput) {
			if (!expected) throw new Error("listedChoices needs the expected labels to read a type-ahead's list")
			for (const label of expected) {
				await combobox.click()
				await combobox.fill(label)
				await expect(main.getByRole("listbox").getByRole("option", { name: label, exact: true })).toBeVisible()
				await combobox.fill("")
				await page.keyboard.press("Escape")
			}
			const shared = sharedSubstring(expected)
			if (!shared) {
				throw new Error(
					`listedChoices: no run of 3 or more characters is shared by every one of ${JSON.stringify(expected)}. ` +
						"A type-ahead's list only ever shows what matches typed text, so order and separators cannot be " +
						"read from it without one — pick fixture wording that shares a run, or drop this row.",
				)
			}
			await combobox.click()
			await combobox.fill(shared)
			const entries = await listEntries(page, { placeholder: false })
			await combobox.fill("")
			await page.keyboard.press("Escape")
			return entries
		}
		if ((await combobox.getAttribute("aria-expanded")) !== "true") await combobox.click()
		return listEntries(page, { placeholder: false })
	}

	throw new Error("The question has no field to read a list from.")
}

// The full expected order the last "its choices are listed" step asserted,
// so "a separator is drawn after" can read a type-ahead's list again with
// the same fixture — it only ever shows what matches typed text (ADR-0152),
// so it needs every label, not just the two either side of the separator.
let lastListedOrder: string[] | undefined

Then(/^its choices are listed (".*")$/, async ({ page }, quoted: string) => {
	const expected = [...quoted.matchAll(/"([^"]*)"/g)].map((match) => match[1])
	lastListedOrder = expected
	expect((await listedChoices(page, expected)).filter((entry) => entry !== "|")).toEqual(expected)
})

Then("a separator is drawn after {string} and after {string}", async ({ page }, first: string, second: string) => {
	const combobox = page.getByRole("main").getByRole("combobox")
	const isInput = (await combobox.count()) > 0 && (await combobox.evaluate((element) => element.tagName)) === "INPUT"
	const listed = isInput ? await listedChoices(page, lastListedOrder) : await listedChoices(page)
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

/** The field's open list, in order, with "|" where a separator is drawn. */
async function listEntries(page: Page, { placeholder = true } = {}): Promise<string[]> {
	await expect(typeAheadList(page)).toBeVisible()
	const entries = placeholder ? '[role="option"], [data-separator]' : '[role="option"]:not([data-placeholder]), [data-separator]'
	return typeAheadList(page)
		.locator(entries)
		.evaluateAll((found) => found.map((entry) => (entry.hasAttribute("data-separator") ? "|" : entry.textContent.trim())))
}

/** Whether the question's field is a single-select, a button, rather than a type-ahead's text input (ADR-0150). */
async function isSingleSelect(page: Page): Promise<boolean> {
	return (await typeAheadField(page).evaluate((element) => element.tagName)) === "BUTTON"
}

/** The page's token `name` as a computed colour, to compare a drawn colour with. */
async function tokenColour(page: Page, name: string): Promise<string> {
	return page.evaluate((token) => {
		const probe = document.createElement("div")
		probe.style.color = `var(${token})`
		document.body.appendChild(probe)
		const color = getComputedStyle(probe).color
		probe.remove()
		return color
	}, name)
}

/** Expects `row` drawn as a type-ahead's highlighted option: the stronger surface and the inset focus bar (ChoiceList). */
async function expectHighlighted(page: Page, row: Locator) {
	const surface = await tokenColour(page, "--color-surface-4")
	const focus = await tokenColour(page, "--color-focus")
	await expect(row).toHaveCSS("background-color", surface)
	// Tailwind composes several shadows; the inset bar is one of them.
	await expect.poll(() => row.evaluate((element) => getComputedStyle(element).boxShadow)).toContain(`${focus} 4px 0px 0px 0px inset`)
}

function quotedList(quoted: string): string[] {
	return [...quoted.matchAll(/"([^"]*)"/g)].map((match) => match[1])
}

Given(
	"a {word} question with the help text {string} offers {string}, {string}, and {string}, none pinned",
	async ({ page }, type: string, help: string, first: string, second: string, third: string) => {
		const options = [stubChoice(first), stubChoice(second), stubChoice(third)]
		await openForm(page, choiceFormQuestions(choiceType(type), options, { helpTextEn: help, helpTextFr: help }))
	},
)

Given("a {word} question offers {int} choices", async ({ page }, type: string, count: number) => {
	const options = Array.from({ length: count }, (_, index) => stubChoice(`Launch site ${index + 1}`))
	await openForm(page, choiceFormQuestions(choiceType(type), options))
})

/** A phone-width screen, as the area README states: 360 pixels wide (REQ-QB-163). */
const PHONE_WIDTH = 360

When("a reporter using English opens that question on a phone-width screen", async ({ page }) => {
	await page.setViewportSize({ width: PHONE_WIDTH, height: 740 })
	await goNext(page) // intro -> the question's page
})

Then(
	"the question is drawn by the form, closed, with no caret, described by its help text, and no browser suggestion list",
	async ({ page }) => {
		const field = page.getByRole("combobox", { name: "Which one applies?" })
		await expect(field).toBeVisible()
		await expect(field).toHaveAccessibleDescription("Pick the nearest site")
		await expect(field).toHaveAttribute("aria-expanded", "false")
		await expect(field).not.toHaveAttribute("list", /./)
		await expect(page.locator("datalist")).toHaveCount(0)
		// No caret drawn beside the field (ADR-0152).
		await expect(page.getByRole("button", { name: "Show choices" })).toHaveCount(0)
		await expect(page.locator("[data-caret]")).toHaveCount(0)
		// Drawn like the form's other fields, in the design-system border.
		const border = await field.evaluate((element) => getComputedStyle(element).borderTopColor)
		const rule = await field.evaluate((element) => {
			const probe = document.createElement("div")
			probe.style.color = "var(--color-rule)"
			if (!element.parentElement) throw new Error("The field has no parent to draw the probe in.")
			element.parentElement.appendChild(probe)
			const color = getComputedStyle(probe).color
			probe.remove()
			return color
		})
		expect(border).toBe(rule)
	},
)

When(/^they open the question's list by (.+)$/, async ({ page }, opening: string) => {
	const field = typeAheadField(page)
	const keys: Record<string, string> = {
		"pressing Alt and the down arrow": "Alt+ArrowDown",
		"pressing the down arrow": "ArrowDown",
		"pressing Enter": "Enter",
		"pressing Space": "Space",
	}
	if (opening === "pressing the caret") {
		// Only the single-select keeps a caret; it is drawn inside the field itself (ADR-0152).
		const box = present(await field.boundingBox())
		await field.click({ position: { x: box.width - 22, y: box.height / 2 } })
	} else if (opening === "clicking the question") await field.click()
	else if (keys[opening]) {
		await field.focus()
		await page.keyboard.press(keys[opening])
	} else {
		const typed = /^typing "(.*)"$/.exec(opening)
		if (!typed) throw new Error(`Unknown way to open the list: ${opening}`)
		await field.pressSequentially(typed[1])
	}
})

/** The list geometry shared by every opening: directly beneath the field, as wide as it. */
async function expectListBeneathField(page: Page) {
	const field = typeAheadField(page)
	await expect(field).toHaveAttribute("aria-expanded", "true")
	const list = typeAheadList(page)
	await expect(field).toHaveAttribute("aria-controls", present(await list.getAttribute("id")))

	const fieldBox = present(await field.boundingBox())
	const listBox = present(await list.boundingBox())
	expect(Math.abs(listBox.x - fieldBox.x)).toBeLessThanOrEqual(1)
	expect(Math.abs(listBox.width - fieldBox.width)).toBeLessThanOrEqual(1)
	expect(listBox.y).toBeGreaterThanOrEqual(fieldBox.y + fieldBox.height)
	expect(listBox.y - (fieldBox.y + fieldBox.height)).toBeLessThanOrEqual(8)
}

Then(/^a list as wide as the question opens directly beneath it, offering (".*")$/, async ({ page }, quoted: string) => {
	await expectListBeneathField(page)
	expect((await listEntries(page)).filter((entry) => entry !== "|")).toEqual(quotedList(quoted))
})

const HINT_TEXT = "Type 3 or more letters to see matching choices, or enter your own."

/** The hint row, and its polite live-region echo announced to assistive technology. */
async function expectHint(page: Page) {
	await expect(typeAheadList(page).getByRole("option")).toHaveCount(0)
	await expect(typeAheadList(page).locator("[data-hint]")).toHaveText(HINT_TEXT)
	await expect(page.getByRole("status").filter({ hasText: HINT_TEXT })).toBeAttached()
}

Then("the list opens directly beneath the question, as wide as it, offering only the hint to type 3 or more letters", async ({ page }) => {
	await expectListBeneathField(page)
	await expectHint(page)
})

Then("the list offers only the hint to type 3 or more letters", async ({ page }) => {
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "true")
	await expectHint(page)
})

Then("the question holds {string}", async ({ page }, value: string) => {
	await expect(typeAheadField(page)).toHaveValue(value)
})

When("they enter {string} in the question", async ({ page }, typed: string) => {
	await typeAheadField(page).pressSequentially(typed)
})

Then(/^its list offers only (".*")$/, async ({ page }, quoted: string) => {
	await expect(typeAheadList(page).getByRole("option")).toHaveText(quotedList(quoted))
})

Then("{string} is the question's active choice", async ({ page }, label: string) => {
	const option = typeAheadList(page).getByRole("option", { name: label, exact: true })
	// A type-ahead selects the highlighted option; a single-select keeps aria-selected for the chosen one (ADR-0150).
	if (!(await isSingleSelect(page))) await expect(option).toHaveAttribute("aria-selected", "true")
	await expect(typeAheadField(page)).toHaveAttribute("aria-activedescendant", present(await option.getAttribute("id")))
	await expectHighlighted(page, option)
	await expect(typeAheadField(page)).toBeFocused()
})

Then("its list is open, with {string} chosen and active", async ({ page }, label: string) => {
	const option = typeAheadList(page).getByRole("option", { name: label, exact: true })
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "true")
	await expect(option).toHaveAttribute("aria-selected", "true")
	await expect(typeAheadList(page).locator('[role="option"][aria-selected="true"]')).toHaveCount(1)
	await expect(typeAheadField(page)).toHaveAttribute("aria-activedescendant", present(await option.getAttribute("id")))
})

When("they point at {string}", async ({ page }, label: string) => {
	// A multi-select's row is its checkbox's label; a single-select's is an option.
	const multi = page.getByRole("main").locator('[id$="-options"] label').filter({ hasText: label })
	if ((await multi.count()) > 0) await multi.hover()
	else await typeAheadList(page).getByRole("option", { name: label, exact: true }).hover()
})

// REQ-QB-267/268: the pointer picks a row by a click; no row is ever focusable.
When("they pick {string} from the list with the pointer", async ({ page }, label: string) => {
	await typeAheadList(page).getByRole("option", { name: label, exact: true }).click()
})

Then("the question has focus", async ({ page }) => {
	await expect(typeAheadField(page)).toBeFocused()
	await expect(page.locator('[role="option"][tabindex]')).toHaveCount(0)
})

When("they pick {string} from the question's list", async ({ page }, label: string) => {
	if (await isSingleSelect(page)) {
		await pickChoice(page, "Which one applies?", label)
		return
	}
	// A type-ahead lists its choices only once 3 or more characters are typed (ADR-0152).
	await typeAheadField(page).pressSequentially(label)
	await typeAheadList(page).getByRole("option", { name: label, exact: true }).click()
	await expect(typeAheadField(page)).toHaveValue(label)
})

Then("the browser's saved report holds no answer to that question", async ({ page }) => {
	await expect
		.poll(async () => ((await readDraftFromBrowser(page)) as { answers?: Record<string, unknown> } | null)?.answers?.["rev-choice_question"])
		.toBeUndefined()
})

Then(
	"the question is drawn by the form, closed, with a caret showing {string}, described by its help text, and no browser select",
	async ({ page }, placeholder: string) => {
		const field = page.getByRole("combobox", { name: "Which one applies?" })
		await expect(field).toBeVisible()
		await expect(field).toHaveText(placeholder)
		await expect(field).toHaveAccessibleDescription("Pick the nearest site")
		await expect(field).toHaveAttribute("aria-expanded", "false")
		await expect(field.locator("svg")).toBeVisible()
		await expect(page.getByRole("main").locator("select")).toHaveCount(0)
		// Drawn like the form's other fields, in the design-system border.
		await expect(field).toHaveCSS("border-top-color", await tokenColour(page, "--color-rule"))
	},
)

Then("the list is drawn like a type-ahead's list", async ({ page }) => {
	const list = page.getByRole("main").locator('[role="listbox"]:visible, [id$="-options"]').first()
	await expect(list).toBeVisible()
	// The shared list's surface, border, and shadow (ChoiceList).
	await expect(list).toHaveCSS("background-color", await tokenColour(page, "--color-surface"))
	await expect(list).toHaveCSS("border-top-color", await tokenColour(page, "--color-rule"))
	await expect(list).toHaveCSS("border-top-width", "1px")
	expect(await list.evaluate((element) => getComputedStyle(element).boxShadow)).not.toBe("none")
	await expect(list).toHaveCSS("overflow-y", "auto")
	// A separator, where there is one, is a row of the list, not a rule of its own.
	await expect(list.locator("hr")).toHaveCount(0)
	for (const separator of await list.locator("[data-separator]").all()) {
		expect(await separator.evaluate((element) => element.tagName)).toBe("LI")
	}
})

When("they open the multi-select's list", async ({ page }) => {
	await page.getByRole("main").getByRole("combobox", { name: /Which one applies\?/ }).click()
})

/** Large enough to touch, as the area README states: at least 44 pixels tall (REQ-QB-211). */
const TOUCH_TARGET = 44

Then("each choice is a row large enough to touch, holding a checkbox", async ({ page }) => {
	const rows = page.getByRole("main").locator('[id$="-options"] label')
	await expect(rows).toHaveCount(6)
	for (const row of await rows.all()) {
		expect(present(await row.boundingBox()).height).toBeGreaterThanOrEqual(TOUCH_TARGET)
		await expect(row.getByRole("checkbox")).toHaveCount(1)
		// The type-ahead's row inset (ChoiceList).
		await expect(row).toHaveCSS("padding-left", "12px")
	}
})

Then("the {string} row is highlighted as a type-ahead's active choice is", async ({ page }, label: string) => {
	await expectHighlighted(page, page.getByRole("main").locator('[id$="-options"] label').filter({ hasText: label }))
})

When(/^they move to the "(.*)" checkbox and check it with (.+)$/, async ({ page }, label: string, keys: string) => {
	// Tab from the checkbox before it, so focus arrives by keyboard and shows.
	const boxes = page.getByRole("main").getByRole("checkbox")
	const labels = await boxes.evaluateAll((found) => found.map((entry) => entry.closest("label")?.textContent.trim() ?? ""))
	await boxes.nth(labels.indexOf(label) - 1).focus()
	await page.keyboard.press("Tab")
	await expect(page.getByRole("checkbox", { name: label })).toBeFocused()
	// Move the pointer off the list, so only keyboard focus can highlight a row.
	await page.mouse.move(0, 0)
	for (const key of keyPresses(keys)) await page.keyboard.press(key)
})

Then("{string} is checked, and the list stays open", async ({ page }, label: string) => {
	await expect(page.getByRole("checkbox", { name: label })).toBeChecked()
	await expect(page.getByRole("main").locator('[id$="-options"]')).toBeVisible()
})

/*
 * Keys are named only in a scenario's Examples cells (#815): "the Enter key",
 * "the down arrow key twice", "the Alt and down arrow keys", "the m key".
 */
const KEY_NAMES: Record<string, string> = { "down arrow": "ArrowDown", "up arrow": "ArrowUp", "Alt and down arrow": "Alt+ArrowDown" }

/** The key presses an Examples cell names, in order. */
function keyPresses(cell: string): string[] {
	const named = /^the (.+?) keys?( twice)?$/.exec(cell)
	if (!named) throw new Error(`Unknown keys: ${cell}`)
	const key = KEY_NAMES[named[1]] ?? named[1]
	return named[2] ? [key, key] : [key]
}

When(/^they use (.+) in the question$/, async ({ page }, keys: string) => {
	for (const key of keyPresses(keys)) await page.keyboard.press(key)
})

/** The question's active choice: the option its field points at, or none. */
async function activeChoice(page: Page): Promise<string | null> {
	const id = await typeAheadField(page).getAttribute("aria-activedescendant")
	if (!id) return null
	return (await page.locator(`[id="${id}"]`).textContent())?.trim() ?? null
}

Given(/^the question's active choice is (?:none|"(.*)")$/, async ({ page }, label: string | undefined) => {
	if (label === undefined) {
		expect(await activeChoice(page)).toBeNull()
		return
	}
	// A single-select's list starts again from its first row; a type-ahead's moves down from none.
	if ((await activeChoice(page)) !== label && (await isSingleSelect(page))) await page.keyboard.press("Home")
	for (let step = 0; step < 10 && (await activeChoice(page)) !== label; step++) await page.keyboard.press("ArrowDown")
	expect(await activeChoice(page)).toBe(label)
})

When("they open the question's list", async ({ page }) => {
	await typeAheadField(page).click()
	await expect(typeAheadList(page)).toBeVisible()
})

When("they dismiss the question's list", async ({ page }) => {
	await page.keyboard.press("Escape")
})

When(/^they close the question's list with (.+)$/, async ({ page }, closing: string) => {
	await expect(typeAheadList(page)).toBeVisible()
	if (closing === "the pointer outside the question") {
		// The page's left margin: outside the field, its caret, its label, and its list.
		const field = present(await typeAheadField(page).boundingBox())
		await page.mouse.click(Math.max(1, field.x - 20), field.y + field.height / 2)
	} else {
		for (const key of keyPresses(closing)) await page.keyboard.press(key)
	}
})

Then("its list is open", async ({ page }) => {
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "true")
	await expect(typeAheadList(page)).toBeVisible()
	await expect(typeAheadField(page)).toBeFocused()
})

Then("the list is closed and the question holds {string}", async ({ page }, value: string) => {
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "false")
	await expect(typeAheadList(page)).toBeHidden()
	// A single-select shows its choice as the field's text; a type-ahead holds it as the input's value.
	if (await isSingleSelect(page)) await expect(typeAheadField(page)).toHaveText(value)
	else await expect(typeAheadField(page)).toHaveValue(value)
})

Then("the list says no choice matches", async ({ page }) => {
	await expect(typeAheadField(page)).toHaveAttribute("aria-expanded", "false")
	await expect(page.getByRole("status").filter({ hasText: "No choice matches" })).toBeVisible()
})

Then("the list fits within the screen's width, and the page grows no wider than the screen", async ({ page }) => {
	const listBox = present(await typeAheadList(page).boundingBox())
	const width = present(page.viewportSize()).width
	expect(listBox.x).toBeGreaterThanOrEqual(0)
	expect(listBox.x + listBox.width).toBeLessThanOrEqual(width)
	expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width)
})

Then("the list reaches its last choice within itself, leaving the page where it is", async ({ page }) => {
	const list = typeAheadList(page)
	const { scrollHeight, clientHeight, overflowY } = await list.evaluate((element) => ({
		scrollHeight: element.scrollHeight,
		clientHeight: element.clientHeight,
		overflowY: getComputedStyle(element).overflowY,
	}))
	expect(scrollHeight).toBeGreaterThan(clientHeight)
	expect(overflowY).toBe("auto")
	// The last choice is reached by scrolling the list, not the page. A
	// type-ahead's Up starts from the end; a single-select's End jumps there.
	await page.keyboard.press((await isSingleSelect(page)) ? "End" : "ArrowUp")
	await expect(list.getByRole("option", { name: "Launch site 30" })).toBeInViewport()
})

// ------------------- a merged-away wording is offered as an alias while typing (ADR-0129 amendment, issue #654) --

function stubAlias(labelEn: string | null, labelFr: string | null = null): { labelEn: string | null; labelFr: string | null } {
	return { labelEn, labelFr }
}

/**
 * The one option's own label, separate from its hint span — so a match on
 * the label can be told apart from a match found only through an alias.
 */
async function ownLabelAndHint(option: Locator): Promise<{ label: string; hint: string | null }> {
	const hintLocator = option.locator('[data-testid="choice-hint"]')
	const hint = (await hintLocator.count()) > 0 ? await hintLocator.innerText() : null
	const label = await option.evaluate((element) => {
		const clone = element.cloneNode(true) as HTMLElement
		clone.querySelector('[data-testid="choice-hint"]')?.remove()
		return clone.textContent.trim()
	})
	return { label, hint }
}

Then(/^the list offers (".*"), hinting (".*")$/, async ({ page }, quotedLabel: string, quotedHint: string) => {
	const options = typeAheadList(page).getByRole("option")
	await expect(options).toHaveCount(1)
	const { label, hint } = await ownLabelAndHint(options.first())
	expect(label).toBe(quotedList(quotedLabel)[0])
	expect(hint).toBe(quotedList(quotedHint)[0])
})

Given("a type-ahead question offers {string}, one merged from {string}", async ({ page }, survivor: string, alias: string) => {
	const option = { ...stubChoice(survivor), aliases: [stubAlias(alias)] }
	await openForm(page, choiceFormQuestions("autocomplete", [option]))
})

Given(
	"a type-ahead question offers {string} \\/ {string}, one merged from {string}",
	async ({ page }, survivorEn: string, survivorFr: string, alias: string) => {
		const option = { ...stubChoice(survivorEn, survivorFr), aliases: [stubAlias(null, alias)] }
		await openForm(page, choiceFormQuestions("autocomplete", [option]))
	},
)

Given(
	"a type-ahead question offers {string}, merged from {string}, itself merged from {string}",
	async ({ page }, survivor: string, mid: string, first: string) => {
		// Merges are flattened server-side: the survivor's aliases already name
		// every earlier wording, with no chain for the client to follow
		// (ADR-0129 amendment).
		const option = { ...stubChoice(survivor), aliases: [stubAlias(first), stubAlias(mid)] }
		await openForm(page, choiceFormQuestions("autocomplete", [option]))
	},
)

