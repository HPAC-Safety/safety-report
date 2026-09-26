import { readFileSync } from "node:fs"

import { createBdd } from "playwright-bdd"
import { devices, expect, type BrowserContext, type Page, type Request } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions, stubSubmission, type StubQuestion } from "./report-form-fixture"

const { Given, When, Then, After } = createBdd()

/*
 * REQ-SUB-098..107: a date question (issue #517, ADR-0138). On a desktop, a
 * text field with the form's own one-month calendar; on a touch device, the
 * device's native date picker. Either way a future date is held to the
 * question's Allow future dates setting. The API is stubbed at the network
 * boundary; its own refusal of a future date is REQ-SUB-108, through the booted
 * host. Every question is synthetic.
 *
 * "Today" is the reporter's local date, the browser's; this process shares its
 * clock and time zone, so the expected dates are computed here.
 */

const DATE_FIELD = "#question-rev-occurred_on"

const LOCALES: Record<string, string> = { English: "en-CA", French: "fr-CA" }

// The catalogues the interface reads, for asserting a label without writing
// any French here: CI fills fr-CA.json, so its current value is what shows.
function catalogue(language: string): Record<string, unknown> {
	return JSON.parse(readFileSync(new URL(`../../../locales/${LOCALES[language]}.json`, import.meta.url), "utf8")) as Record<string, unknown>
}

function label(language: string, key: string): string {
	// en-CA.json is flat; fr-CA.json nests on the dots. Either shape resolves.
	const entries = catalogue(language)
	if (typeof entries[key] === "string") return entries[key] as string
	return key.split(".").reduce<unknown>((node, part) => (node as Record<string, unknown>)?.[part], entries) as string
}

function dateForm(allowFutureDates: boolean): StubQuestion[] {
	const base = {
		isRequired: false,
		isPrivate: false,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: false,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		children: [],
	}

	return [
		{
			...base,
			id: "occurred_on",
			revisionId: "rev-occurred_on",
			key: "occurred_on",
			role: "none",
			type: "date",
			labelEn: "On what date did it happen?",
			labelFr: "À quelle date est-ce arrivé?",
			displayOrder: 0,
			allowFutureDates,
		},
		{
			...base,
			id: "consent",
			revisionId: "rev-consent",
			key: "consent_publish",
			role: "consent_publish",
			type: "yes_no",
			labelEn: "May we publish a summary of this report?",
			labelFr: "Pouvons-nous publier un résumé de ce signalement?",
			displayOrder: 1,
			isRequired: true,
		},
	]
}

// A touch device is its own browser context; its page stands in for `page`
// for the rest of that scenario.
const touchPages = new Map<string, { context: BrowserContext; page: Page }>()
const sent = new WeakMap<Page, Request>()

function active(page: Page, testId: string): Page {
	return touchPages.get(testId)?.page ?? page
}

After(async ({ $testInfo }) => {
	const touch = touchPages.get($testInfo.testId)
	touchPages.delete($testInfo.testId)
	await touch?.context.close()
})

async function openDateForm(page: Page, allowFutureDates: boolean, language = "English") {
	await stubAuth(page)
	await stubCurrentQuestions(page, dateForm(allowFutureDates))
	await stubSubmission(page)
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.locator(DATE_FIELD)).toBeVisible()
	if (language === "French") {
		await page.getByRole("button", { name: "Français" }).click()
		await expect(page.locator("html")).toHaveAttribute("lang", "fr-CA")
	}
}

function pad(value: number): string {
	return String(value).padStart(2, "0")
}

function iso(date: Date): string {
	return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function today(): Date {
	const now = new Date()
	return new Date(now.getFullYear(), now.getMonth(), now.getDate())
}

function daysBefore(days: number): Date {
	const day = today()
	day.setDate(day.getDate() - days)
	return day
}

// The field's own calendar, in whichever language; its English name is
// asserted where a scenario says what it is called.
function calendar(page: Page) {
	return page.locator(`${DATE_FIELD}-calendar[role="dialog"]`)
}

function dayButton(page: Page, date: Date) {
	return calendar(page).locator(`button[data-day="${iso(date)}"]`)
}

function monthName(date: Date, locale: string): string {
	return new Intl.DateTimeFormat(locale, { month: "long" }).format(date)
}

async function expectFocusOn(page: Page, date: Date) {
	await expect(dayButton(page, date)).toBeFocused()
}

async function tabInto(page: Page) {
	const field = page.locator(DATE_FIELD)
	for (let presses = 0; presses < 40; presses++) {
		if (await field.evaluate((element) => element === document.activeElement)) return
		await page.keyboard.press("Tab")
	}
	await expect(field).toBeFocused()
}

// ------------------------------------------------------------------- Given --

Given(
	/^the current page shows a date question that (allows|does not allow) future dates, on a (desktop|touch device)$/,
	async ({ page, browser, $testInfo }, allows: string, device: string) => {
		if (device === "desktop") {
			await openDateForm(page, allows === "allows")
			return
		}

		// A phone: touch input and a coarse pointer, so `(pointer: coarse)` matches.
		const context = await browser.newContext({ ...devices["Pixel 7"], baseURL: $testInfo.project.use.baseURL })
		const touch = await context.newPage()
		touchPages.set($testInfo.testId, { context, page: touch })
		expect(await touch.evaluate(() => window.matchMedia("(pointer: coarse)").matches)).toBe(true)
		await openDateForm(touch, allows === "allows")
	},
)

Given(/^the current page shows a date question in (English|French), on a desktop$/, async ({ page }, language: string) => {
	await openDateForm(page, false, language)
})

// -------------------------------------------------------------------- When --

When(/^the reporter (clicks|tabs into) the date field$/, async ({ page }, how: string) => {
	if (how === "clicks") await page.locator(DATE_FIELD).click()
	else await tabInto(page)
})

When("the reporter clicks the date field and chooses the 1st of today's month", async ({ page }) => {
	await page.locator(DATE_FIELD).click()
	const first = today()
	first.setDate(1)
	await dayButton(page, first).click()
})

When("the reporter types {string} into the date field and presses Next", async ({ page }, typed: string) => {
	const field = page.locator(DATE_FIELD)
	await field.click()
	await field.pressSequentially(typed)
	await page.getByRole("button", { name: "Next", exact: true }).click()
})

When("the reporter submits the report from the next page", async ({ page }) => {
	const consent = page.getByRole("group", { name: "May we publish a summary of this report?" })
	await consent.getByRole("radio", { name: "Yes" }).click()
	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: "Submit report" }).click()
	sent.set(page, await request)
})

When("the reporter tabs into the date field and presses ArrowDown", async ({ page }) => {
	await tabInto(page)
	await expect(calendar(page)).toBeVisible()
	await page.keyboard.press("ArrowDown")
})

When(/^the reporter presses (ArrowLeft|ArrowRight|ArrowUp|ArrowDown|PageUp|PageDown|Enter)$/, async ({ page }, key: string) => {
	await page.keyboard.press(key)
})

When("the reporter presses ArrowDown and then Escape", async ({ page }) => {
	await page.keyboard.press("ArrowDown")
	await expect(calendar(page)).toBeVisible()
	await expectFocusOn(page, today())
	await page.keyboard.press("Escape")
})

When(
	/^the reporter chooses (\w+) in the calendar's month picker and (\d{4}) in its year picker$/,
	async ({ page }, month: string, year: string | number) => {
		await calendar(page).getByRole("combobox", { name: "Month" }).selectOption({ label: month })
		await calendar(page).getByRole("combobox", { name: "Year" }).selectOption(String(year))
	},
)

When(/^the reporter chooses the (\d+)(?:st|nd|rd|th)$/, async ({ page }, day: string | number) => {
	await calendar(page).locator(`button[data-day$="-${String(day).padStart(2, "0")}"]`).click()
})

When(
	"the device's picker sets the date field to {string} and the reporter presses Next",
	async ({ page, $testInfo }, value: string) => {
		const touch = active(page, $testInfo.testId)
		await touch.locator(DATE_FIELD).fill(value)
		await touch.getByRole("button", { name: "Next", exact: true }).click()
	},
)

// -------------------------------------------------------------------- Then --

Then("a calendar labelled {string} opens under the field, showing today's month", async ({ page }, name: string) => {
	const dialog = page.getByRole("dialog", { name })
	await expect(dialog).toBeVisible()
	await expect(page.locator(DATE_FIELD)).toHaveAttribute("aria-expanded", "true")
	await expect(page.locator(DATE_FIELD)).toHaveAttribute("aria-controls", (await dialog.getAttribute("id"))!)

	const field = (await page.locator(DATE_FIELD).boundingBox())!
	const box = (await dialog.boundingBox())!
	expect(box.y).toBeGreaterThanOrEqual(field.y + field.height)
	expect(Math.abs(box.x - field.x)).toBeLessThan(2)

	await expect(dialog.getByRole("combobox", { name: "Month" }).locator("option:checked")).toHaveText(monthName(today(), "en-CA"))
	await expect(dialog.getByRole("combobox", { name: "Year" }).locator("option:checked")).toHaveText(String(today().getFullYear()))
})

Then("today is marked in it", async ({ page }) => {
	await expect(calendar(page).locator('button[aria-current="date"]')).toHaveCount(1)
	await expect(dayButton(page, today())).toHaveAttribute("aria-current", "date")
})

Then("it has {string} and {string} buttons", async ({ page }, previous: string, next: string) => {
	await expect(calendar(page).getByRole("button", { name: previous, exact: true })).toBeVisible()
	await expect(calendar(page).getByRole("button", { name: next, exact: true })).toBeVisible()
})

Then("the date field reads the 1st of today's month as yyyy-mm-dd", async ({ page }) => {
	const first = today()
	first.setDate(1)
	await expect(page.locator(DATE_FIELD)).toHaveValue(iso(first))
})

Then("the calendar closes", async ({ page }) => {
	await expect(calendar(page)).toBeHidden()
	await expect(page.locator(DATE_FIELD)).toHaveAttribute("aria-expanded", "false")
})

Then("the chosen day is announced in words", async ({ page }) => {
	const first = today()
	first.setDate(1)
	const words = new Intl.DateTimeFormat("en-CA", { dateStyle: "full" }).format(first)
	await expect(page.locator(DATE_FIELD).locator("xpath=following-sibling::*[@role='status']")).toHaveText(`Selected: ${words}`)
})

Then("the calendar shows today's month", async ({ page }) => {
	await expect(calendar(page).getByRole("combobox", { name: "Month" }).locator("option:checked")).toHaveText(monthName(today(), "en-CA"))
	await expect(dayButton(page, today())).toBeVisible()
})

Then(/^every day after today is (disabled|offered)$/, async ({ page }, state: string) => {
	const days = calendar(page).locator("button[data-day]")
	const after = iso(today())
	for (const day of await days.all()) {
		if ((await day.getAttribute("data-day"))! <= after) {
			await expect(day).toBeEnabled()
			continue
		}
		if (state === "disabled") await expect(day).toBeDisabled()
		else await expect(day).toBeEnabled()
	}

	// And beyond this month: the next one is out of reach, or open.
	const next = calendar(page).getByRole("button", { name: "Next month", exact: true })
	if (state === "disabled") {
		await expect(next).toBeDisabled()
		return
	}
	await next.click()
	await expect(calendar(page).locator("button[data-day]").first()).toBeEnabled()
})

Then("the reporter stays on the date page", async ({ page, $testInfo }) => {
	const current = active(page, $testInfo.testId)
	await expect(current.locator(DATE_FIELD)).toBeVisible()
	await expect(current.getByRole("group", { name: "May we publish a summary of this report?" })).toHaveCount(0)
})

Then("an inline message says {string}", async ({ page, $testInfo }, message: string) => {
	const current = active(page, $testInfo.testId)
	await expect(current.getByRole("alert").filter({ hasText: message })).toBeVisible()
	await expect(current.locator(DATE_FIELD)).toHaveAttribute("aria-describedby", /rev-occurred_on-error/)
})

Then("the date answer is sent as {string}", async ({ page }, value: string) => {
	const body = sent.get(page)!.postDataJSON() as { answers: { questionRevisionId: string; value: unknown }[] }
	expect(body.answers.find((answer) => answer.questionRevisionId === "rev-occurred_on")?.value).toBe(value)
})

Then(/^the calendar names today's month in (English|French)$/, async ({ page }, language: string) => {
	await expect(calendar(page).getByRole("combobox", { name: label(language, "report.date.month") }).locator("option:checked")).toHaveText(
		monthName(today(), LOCALES[language]),
	)
})

Then(/^its weekday headings start on (Sunday|Monday)$/, async ({ page }, first: string) => {
	const headings = calendar(page).locator("thead th")
	await expect(headings).toHaveCount(7)
	// 4 January 1970 was a Sunday, and the 5th a Monday.
	const locale = (await page.locator("html").getAttribute("lang"))!
	const expected = new Intl.DateTimeFormat(locale, { weekday: "long" }).format(new Date(1970, 0, first === "Sunday" ? 4 : 5))
	await expect(headings.first()).toHaveAttribute("abbr", expected)
})

Then(/^its buttons and pickers are labelled from the (English|French) catalogue$/, async ({ page }, language: string) => {
	const dialog = page.getByRole("dialog", { name: label(language, "report.date.calendar") })
	await expect(dialog).toBeVisible()
	await expect(dialog.getByRole("button", { name: label(language, "report.date.previousMonth"), exact: true })).toBeVisible()
	await expect(dialog.getByRole("button", { name: label(language, "report.date.nextMonth"), exact: true })).toBeVisible()
	await expect(dialog.getByRole("combobox", { name: label(language, "report.date.month"), exact: true })).toBeVisible()
	await expect(dialog.getByRole("combobox", { name: label(language, "report.date.year"), exact: true })).toBeVisible()
})

Then("today has focus in the calendar", async ({ page }) => {
	await expectFocusOn(page, today())
})

Then(/^the day (\d+) days? before today has focus$/, async ({ page }, days: string) => {
	await expectFocusOn(page, daysBefore(Number(days)))
})

Then("the same day of the previous month has focus", async ({ page }) => {
	const now = today()
	const lastOfPrevious = new Date(now.getFullYear(), now.getMonth(), 0).getDate()
	await expectFocusOn(page, new Date(now.getFullYear(), now.getMonth() - 1, Math.min(now.getDate(), lastOfPrevious)))
})

Then("the date field reads today as yyyy-mm-dd", async ({ page }) => {
	await expect(page.locator(DATE_FIELD)).toHaveValue(iso(today()))
})

Then("focus is on the date field", async ({ page }) => {
	await expect(page.locator(DATE_FIELD)).toBeFocused()
})

Then(/^the calendar shows (\w+) (\d{4})$/, async ({ page }, month: string, shownYear: string | number) => {
	const year = String(shownYear)
	await expect(calendar(page).getByRole("combobox", { name: "Month" }).locator("option:checked")).toHaveText(month)
	await expect(calendar(page).getByRole("combobox", { name: "Year" }).locator("option:checked")).toHaveText(year)
	await expect(calendar(page).getByRole("grid", { name: `${month} ${year}` })).toBeVisible()
})

Then("the date field reads {string}", async ({ page }, value: string) => {
	await expect(page.locator(DATE_FIELD)).toHaveValue(value)
})

Then(/^the date field is a native date input (whose latest date is today|with no latest date)$/, async ({ page, $testInfo }, limit: string) => {
	const field = active(page, $testInfo.testId).locator(DATE_FIELD)
	await expect(field).toHaveAttribute("type", "date")
	if (limit === "with no latest date") await expect(field).not.toHaveAttribute("max")
	else await expect(field).toHaveAttribute("max", iso(today()))
})

Then("tapping it opens no calendar of the form's own", async ({ page, $testInfo }) => {
	const touch = active(page, $testInfo.testId)
	await touch.locator(DATE_FIELD).tap()
	await expect(touch.getByRole("dialog", { name: "Choose a date" })).toHaveCount(0)
})
