import { readFileSync } from "node:fs"
import { createBdd } from "playwright-bdd"
import { expect, type Page, type Request } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { pickChoice, stubCurrentQuestions, stubSubmission, type StubQuestion } from "./report-form-fixture"
import { present } from "./present"

const { Given, When, Then } = createBdd()

/*
 * REQ-QB-195 to REQ-QB-224: a picker or type-ahead's choices depending on
 * another's answer, each choice under one or more parent choices, in the
 * editor, on the report form, and on the type-ahead review page (ADR-0146,
 * ADR-0151). The API is stubbed at the network boundary; what the
 * server does with a dependency, a link, or a submission is REQ-QB-179 to
 * REQ-QB-194, REQ-QB-203, and REQ-SUB-113. Every question and choice here is
 * synthetic.
 */

// ---- The editor ----

interface AdminChoice {
	id: string
	code: string
	labelEn: string
	labelFr: string
	addedByReporter: boolean
	needsTranslation: boolean
	reporterLocale: string | null
	pin: string
	parentChoiceIds: string[]
}

function adminChoice(id: string, label: string, parents: string | string[] | null = null, pin = "none"): AdminChoice {
	const parentChoiceIds = parents === null ? [] : typeof parents === "string" ? [parents] : parents
	return { id, code: id, labelEn: label, labelFr: label, addedByReporter: false, needsTranslation: false, reporterLocale: null, pin, parentChoiceIds }
}

function adminQuestion(id: string, labelEn: string, type: string, displayOrder: number, options: AdminChoice[], choicesDependOnQuestionId: string | null = null) {
	return {
		id,
		key: id,
		revisionId: `rev-${id}`,
		revisionNumber: 1,
		type,
		isSystem: false,
		isRequired: false,
		isPrivate: false,
		isTranslatable: false,
		allowFutureDates: false,
		isActive: true,
		displayOrder,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		groupedUnderQuestionId: null,
		choicesDependOnQuestionId,
		labelEn,
		labelFr: labelEn,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options,
		reporterChoicesAwaitingReview: 0,
		hasBeenAnswered: false,
	}
}

type SavedOption = { code: string | null; labelEn: string; parentChoiceIds?: string[] | null }
type Saved = { choicesDependOnQuestionId: string | null; options: SavedOption[] }

const saves = new WeakMap<Page, Saved[]>()

/** Answers the admin question list with `questions`, and records each save of one of them. */
async function stubQuestionBank(page: Page, questions: ReturnType<typeof adminQuestion>[]) {
	const saved: Saved[] = []
	saves.set(page, saved)

	await page.route("**/api/admin/questions", (route) => route.fulfill({ json: questions }))
	await page.route("**/api/admin/questions/*", async (route) => {
		const id = route.request().url().split("/").pop()
		const index = questions.findIndex((candidate) => candidate.id === id)
		const body = route.request().postDataJSON() as Saved
		saved.push(body)
		const current = questions[index]
		questions[index] = {
			...current,
			choicesDependOnQuestionId: body.choicesDependOnQuestionId,
			options: body.options.map((option) => {
				const existing = current.options.find((candidate) => candidate.code === option.code)
				return { ...(existing ?? adminChoice(option.labelEn.toLowerCase(), option.labelEn)), parentChoiceIds: option.parentChoiceIds ?? existing?.parentChoiceIds ?? [] }
			}),
		}
		await route.fulfill({ json: questions[index] })
	})
	await page.reload()
}

function lastSave(page: Page): Saved {
	const all = saves.get(page) ?? []
	expect(all.length).toBeGreaterThan(0)
	return all[all.length - 1]
}

async function editQuestion(page: Page, label: string) {
	await page.getByRole("listitem").filter({ hasText: label }).getByRole("button", { name: "Edit" }).click()
}

const MAKE = [adminChoice("niviuk", "Niviuk"), adminChoice("ozone", "Ozone")]

/** The "Offered under" multi-select on one choice row (ADR-0151). */
function parentPicker(page: Page, row: number) {
	return page.getByTestId("question-choice-parent").nth(row)
}

/** Ticks exactly `labels` in one row's "Offered under" list, then closes it. */
async function tickParents(page: Page, row: number, labels: string[]) {
	const picker = parentPicker(page, row)
	await picker.getByRole("combobox").click()
	for (const checkbox of await picker.getByRole("checkbox").all()) {
		const label = (await checkbox.locator("xpath=..").textContent())?.trim() ?? ""
		if (labels.includes(label)) await checkbox.check()
		else await checkbox.uncheck()
	}
	await page.keyboard.press("Escape")
}

When(
	"they edit a type-ahead question placed after a single-select {string}, a type-ahead {string}, a multi-select {string}, and a type-ahead {string} whose choices depend on {string}",
	async ({ page }, make: string, site: string, conditions: string, model: string, _parent: string) => {
		await stubQuestionBank(page, [
			adminQuestion("make", make, "single_select", 0, MAKE),
			adminQuestion("site", site, "autocomplete", 1, [adminChoice("woodside", "Woodside")]),
			adminQuestion("conditions", conditions, "multi_select", 2, [adminChoice("gusty", "Gusty")]),
			adminQuestion("model", model, "autocomplete", 3, [adminChoice("mentor_7", "Mentor 7", "niviuk")], "make"),
			adminQuestion("harness", "Harness", "autocomplete", 4, [adminChoice("lightness", "Lightness"), adminChoice("impress", "Impress")]),
		])
		await editQuestion(page, "Harness")
	},
)

Then("its {string} control offers {string} and {string} only", async ({ page }, _control: string, first: string, second: string) => {
	const control = page.getByLabel("Choices depend on the answer to")
	await expect(control.locator("option")).toHaveText(["No other question", first, second])
})

When("they pick {string}, link every choice, and save", async ({ page }, parent: string) => {
	await page.getByLabel("Choices depend on the answer to").selectOption({ label: parent })
	const rows = await page.getByTestId("question-choice-parent").count()
	for (let row = 0; row < rows; row++) {
		await tickParents(page, row, ["Niviuk"])
	}
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the save names {string} as the question its choices depend on", ({ page }, _parent: string) => {
	const save = lastSave(page)
	expect(save.choicesDependOnQuestionId).toBe("make")
	expect(save.options.map((option) => option.parentChoiceIds)).toEqual([["niviuk"], ["niviuk"]])
})

When("they clear {string} and save", async ({ page }, _control: string) => {
	await editQuestion(page, "Harness")
	await page.getByLabel("Choices depend on the answer to").selectOption({ label: "No other question" })
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the save names no parent question and keeps every choice's link", ({ page }) => {
	const save = lastSave(page)
	expect(save.choicesDependOnQuestionId).toBeNull()
	expect(save.options.map((option) => option.parentChoiceIds)).toEqual([["niviuk"], ["niviuk"]])
})

When(
	"they make a type-ahead question's choices depend on a single-select question offering {string} pinned last, and {string} and {string} not pinned",
	async ({ page }, last: string, first: string, second: string) => {
		await stubQuestionBank(page, [
			adminQuestion("make", "Make", "single_select", 0, [
				adminChoice("other", last, null, "last"),
				adminChoice("ozone", first),
				adminChoice("niviuk", second),
			]),
			adminQuestion("model", "Model", "autocomplete", 1, [adminChoice("mentor_7", "Mentor 7"), adminChoice("rush_6", "Rush 6")]),
		])
		await editQuestion(page, "Model")
		await page.getByLabel("Choices depend on the answer to").selectOption({ label: "Make" })
		await page.getByRole("button", { name: "Add a choice" }).click()
		const rows = page.getByTestId("question-choice")
		await rows.last().getByRole("textbox", { name: "Choice (English)" }).fill("Zeno 2")
		await rows.last().getByRole("textbox", { name: "Choice (French)" }).fill("Zeno 2")
	},
)

Then(
	"every choice row, a new one included, has an {string} multi-select listing {string}, {string}, {string}",
	async ({ page }, control: string, first: string, second: string, third: string) => {
		const pickers = page.getByTestId("question-choice-parent")
		await expect(pickers).toHaveCount(3)
		for (let row = 0; row < 3; row++) {
			const trigger = parentPicker(page, row).getByRole("combobox")
			await expect(trigger).toHaveAccessibleName(new RegExp(`^${control}`))
			await trigger.click()
			await expect(parentPicker(page, row).getByRole("checkbox")).toHaveCount(3)
			await expect(parentPicker(page, row).locator("li")).toHaveText([first, second, third])
			await page.keyboard.press("Escape")
		}
	},
)

const languages = new WeakMap<Page, string>()

Given("an Administrator using {word} opens the manage-questions page", async ({ page }, language: string) => {
	languages.set(page, language)
	await stubAuth(page)
	await signInAs(page, "administrator")
	await page.goto("/admin/questions")
})

/** The French catalogue's wording for `key`, when CI has translated it; the "#"-prefixed stub otherwise. */
function french(key: string): string | undefined {
	const catalogue = JSON.parse(readFileSync(new URL("../../../locales/fr-CA.json", import.meta.url), "utf8")) as Record<string, unknown>
	let node: unknown = catalogue
	for (const part of key.split(".")) node = (node as Record<string, unknown> | undefined)?.[part]
	return typeof node === "string" ? node : undefined
}

Then("Save is refused while a choice is offered under nothing, naming that choice in {word}, with its multi-select marked invalid", async ({ page }, language: string) => {
	await tickParents(page, 0, ["Niviuk"])
	await tickParents(page, 1, ["Ozone"])
	await expect(page.getByRole("button", { name: "Save" })).toBeDisabled()
	// The row offered under nothing is marked invalid, and described by the refusal.
	await expect(parentPicker(page, 2).getByRole("combobox")).toHaveAttribute("aria-invalid", "true")
	await expect(parentPicker(page, 0).getByRole("combobox")).not.toHaveAttribute("aria-invalid", "true")
	const refusal = page.getByTestId("question-choices-unlinked")
	await expect(refusal).toContainText("Zeno 2")
	await expect(refusal).not.toContainText("Mentor 7")

	if (language === "French") {
		// The same refusal, in French, keeping every tick; then back to English.
		await page.getByRole("button", { name: "Français" }).click()
		const expected = french("questions.choice.unlinked")
		await expect(refusal).not.toContainText("Tick at least one answer")
		if (expected && !expected.startsWith("#")) await expect(refusal).toContainText(expected.split("{choices}")[0].trim())
		await expect(refusal).toContainText("Zeno 2")
		await page.getByRole("button", { name: /^Passer à/ }).click()
	} else {
		await expect(refusal).toContainText("Tick at least one answer each choice is offered under before saving: Zeno 2.")
	}
})

When("they tick {string} and {string} for one choice and {string} for every other, and save", async ({ page }, first: string, second: string, other: string) => {
	await tickParents(page, 0, [first, second])
	await tickParents(page, 1, [other])
	await tickParents(page, 2, [other])
	await expect(page.getByTestId("question-choices-unlinked")).toHaveCount(0)
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the save sends each choice with every parent choice ticked for it", ({ page }) => {
	const save = lastSave(page)
	expect(save.choicesDependOnQuestionId).toBe("make")
	expect(save.options.map((option) => [option.labelEn, [...(option.parentChoiceIds ?? [])].sort()])).toEqual([
		["Mentor 7", ["niviuk", "ozone"]],
		["Rush 6", ["ozone"]],
		["Zeno 2", ["ozone"]],
	])
})

// ---- The report form ----

function formQuestion(overrides: Partial<StubQuestion> & { id: string; labelEn: string; type: string; displayOrder: number }): StubQuestion {
	return {
		key: overrides.id,
		role: "none",
		revisionId: `rev-${overrides.id}`,
		isRequired: false,
		isPrivate: false,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: overrides.type === "autocomplete",
		labelFr: overrides.labelEn,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		children: [],
		...overrides,
	}
}

function formChoice(id: string, label: string, parents: string | string[] | null = null) {
	const parentChoiceIds = parents === null ? [] : typeof parents === "string" ? [parents] : parents
	return { id, code: id, labelEn: label, labelFr: label, onlyIn: null, pin: "none", parentChoiceIds }
}

const MODELS = [
	formChoice("mentor_7", "Mentor 7", "niviuk"),
	formChoice("ikuma", "Ikuma", "niviuk"),
	formChoice("rush_6", "Rush 6", "ozone"),
]

/** One page asking the make and the model together, then publication consent. */
function wingForm(makeType: string, modelType: string, { modelRequired = false, makes = ["Niviuk", "Ozone"] } = {}): StubQuestion[] {
	return [
		formQuestion({
			id: "wing",
			labelEn: "Your wing",
			type: "group",
			displayOrder: 0,
			children: [
				formQuestion({ id: "make", labelEn: "Make", type: makeType, displayOrder: 1, options: makes.map((make) => formChoice(make.toLowerCase(), make)) }),
				{
					...formQuestion({ id: "model", labelEn: "Model", type: modelType, displayOrder: 2, options: MODELS, isRequired: modelRequired }),
					choicesDependOnQuestionId: "make",
				} as StubQuestion,
			],
		}),
		formQuestion({ id: "consent", labelEn: "May we publish a summary of this report?", type: "yes_no", displayOrder: 3, role: "consent_publish", isRequired: true }),
	]
}

const forms = new WeakMap<Page, StubQuestion[]>()

function modelField(page: Page) {
	const type = forms.get(page)?.[0]?.children[1]?.type
	// By its id: a required question's label also carries the Required badge.
	return type === "autocomplete" ? page.getByRole("combobox", { name: "Model" }) : page.locator("#question-rev-model")
}

/**
 * The choices currently offered under the parent's answer. A type-ahead
 * shows no choices below 3 typed characters (ADR-0152), so each candidate's
 * own label is typed in turn and checked for a match, rather than opening the
 * list once to read every row.
 *
 * This proves which choices are offered, not the order they would list in:
 * REQ-QB-197/223's own fixed wording ("Ikuma", "Mentor 7", "Other") does not
 * claim an order, and shares no run of 3 or more characters that would let a
 * type-ahead show them together to prove one. Order still sorts through the
 * same `optionGroups`/`sortChoices` path (ADR-0136) as every other choice
 * question; `report-form.steps.ts`'s `listedChoices` proves it where a
 * scenario claims it and its fixture shares a run — REQ-QB-145, -146, -148.
 */
async function modelOffers(page: Page, expected: string[]): Promise<void> {
	const type = forms.get(page)?.[0]?.children[1]?.type
	if (type === "autocomplete") {
		const field = modelField(page)
		// Probing types into the field, which may already hold a real answer;
		// put it back exactly as found once every label has been checked.
		const original = await field.inputValue()
		for (const label of expected) {
			await field.click()
			await field.fill(label)
			await expect(page.getByRole("listbox", { name: "Model" }).getByRole("option", { name: label, exact: true })).toBeVisible()
			await field.fill("")
			await page.keyboard.press("Escape")
		}
		// Every label this form's "Model" could name, not just the current
		// form's own options: the fixture varies from scenario to scenario.
		const allLabels = (forms.get(page)?.[0]?.children[1]?.options ?? MODELS).map((option) => option.labelEn)
		for (const label of allLabels.filter((label) => !expected.includes(label))) {
			// Typing another choice's exact wording matches it by wording
			// (REQ-QB-171), but #562/REQ-QB-227 keeps it as the words typed
			// instead of clearing it, since the parent's answer does not offer
			// it — so the exact label is safe to type here.
			await field.click()
			await field.fill(label)
			await expect(page.getByRole("listbox", { name: "Model" })).toBeHidden()
			await field.fill("")
			await page.keyboard.press("Escape")
		}
		if (original) {
			await field.click()
			await field.fill(original)
			const option = page.getByRole("listbox", { name: "Model" }).getByRole("option", { name: original, exact: true })
			if (await option.count()) await option.click()
			else await page.keyboard.press("Escape")
		}
		return
	}
	// A single-select's list, less its "Choose one" row (ADR-0150).
	await modelField(page).click()
	const offered = await page.getByRole("listbox", { name: "Model" }).locator('[role="option"]:not([data-placeholder])').allTextContents()
	await page.keyboard.press("Escape")
	expect(offered).toEqual(expected)
}

async function openForm(page: Page, form: StubQuestion[], language = "English") {
	forms.set(page, form)
	if (language === "French") await page.context().addInitScript(() => localStorage.setItem("hpac.locale", "fr-CA"))
	await stubAuth(page)
	await stubCurrentQuestions(page, form)
	await stubSubmission(page)
	await signInAs(page, "user")
	await page.goto("/report")
}

async function answerMake(page: Page, make: string) {
	const makeType = forms.get(page)?.[0]?.children[0]?.type
	if (makeType === "autocomplete") {
		await page.getByRole("combobox", { name: "Make" }).fill(make)
	} else {
		await pickChoice(page, "Make", make)
	}
}

Given(
	"a {word} {string} question's choices depend on a single-select {string} question offering {string} and {string}",
	({ page }, childType: string, _child: string, _make: string, _first: string, _second: string) => {
		forms.set(page, wingForm("single_select", childType))
	},
)

Given(
	"{string} offers {string} and {string} linked to {string}, and {string} linked to {string}",
	({ page }, _child: string, first: string, second: string, parent: string, third: string, otherParent: string) => {
		const make = present(forms.get(page))[0].children[0]
		const idOf = (label: string) => present(make.options.find((option) => option.labelEn === label)).id
		const model = present(forms.get(page))[0].children[1]
		model.options = [
			formChoice(`${idOf(parent)}-${first}`, first, idOf(parent)),
			formChoice(`${idOf(parent)}-${second}`, second, idOf(parent)),
			formChoice(`${idOf(otherParent)}-${third}`, third, idOf(otherParent)),
		]
	},
)

When("a reporter using {word} opens the page asking both", async ({ page }, language: string) => {
	await openForm(page, present(forms.get(page)), language)
})

Then("{string} is disabled, and says to answer {string} first", async ({ page }, _child: string, parent: string) => {
	await expect(modelField(page)).toBeDisabled()
	await expect(page.getByTestId("question-note")).toContainText(parent)
})

When("they answer {string} with {string}", async ({ page }, _parent: string, make: string) => {
	await answerMake(page, make)
})

Then("{string} is enabled and offers only {string} and {string}", async ({ page }, _child: string, first: string, second: string) => {
	await expect(modelField(page)).toBeEnabled()
	await modelOffers(page, [first, second])
})

Given("the type-ahead {string} question's choices depend on the single-select {string} question", async ({ page }, _child: string, _parent: string) => {
	await openForm(page, wingForm("single_select", "autocomplete"))
})

Given("a reporter answered {string} with {string} and picked {string} for {string}", async ({ page }, _parent: string, make: string, model: string, _child: string) => {
	await answerMake(page, make)
	const field = modelField(page)
	await field.click()
	await field.fill(model)
	await page.getByRole("listbox", { name: "Model" }).getByRole("option", { name: model, exact: true }).click()
	await expect(modelField(page)).toHaveValue(model)
})

When("they change {string} to {string}", async ({ page }, _parent: string, make: string) => {
	await answerMake(page, make)
})

// ---- A dependent type-ahead's own hint and threshold (REQ-QB-231, ADR-0152) ----

When("they open {string}'s list by clicking the field", async ({ page }, _child: string) => {
	await modelField(page).click()
})

When("they type {string} in {string}", async ({ page }, typed: string, _child: string) => {
	// Cleared first: Playwright's fill() is a no-op when the field already
	// holds the requested text, which a scenario retyping the same words
	// after the parent's answer changes relies on firing again.
	await modelField(page).fill("")
	await modelField(page).fill(typed)
})

Then("{string}'s list offers only the hint to type 3 or more letters", async ({ page }, _child: string) => {
	const list = page.getByRole("listbox", { name: "Model" })
	await expect(list.getByRole("option")).toHaveCount(0)
	await expect(list.locator("[data-hint]")).toHaveText("Type 3 or more letters to see matching choices, or enter your own.")
})

Then("{string}'s list offers only {string}", async ({ page }, _child: string, label: string) => {
	await expect(page.getByRole("listbox", { name: "Model" }).getByRole("option")).toHaveText([label])
})

Then("{string}'s list offers no choice", async ({ page }, _child: string) => {
	await expect(modelField(page)).toHaveAttribute("aria-expanded", "false")
	await expect(page.getByRole("listbox", { name: "Model" })).toBeHidden()
})

Then("{string} is empty and offers only {string}", async ({ page }, _child: string, only: string) => {
	await expect(modelField(page)).toHaveValue("")
	await modelOffers(page, [only])
})

When("they type {string}, a value {string} does not offer, and change {string} to {string}", async ({ page }, typed: string, _child: string, _parent: string, make: string) => {
	await modelField(page).fill(typed)
	await page.keyboard.press("Tab")
	await answerMake(page, make)
})

Then("{string} still holds {string}", async ({ page }, _child: string, typed: string) => {
	await expect(modelField(page)).toHaveValue(typed)
})

// ---- A merged-away wording is offered only under its own parent choices (ADR-0129 amendment, issue #654) ----

Given(
	"the type-ahead {string} question's choices depend on the single-select {string} question, and its {string} under {string} was merged from {string}",
	async ({ page }, _child: string, _parent: string, survivor: string, _parentChoice: string, alias: string) => {
		const questions = wingForm("single_select", "autocomplete")
		const model = questions[0].children[1]
		model.options = model.options.map((option) =>
			option.labelEn === survivor ? { ...option, aliases: [{ labelEn: alias, labelFr: null }] } : option,
		)
		await openForm(page, questions)
	},
)

Then("{string}'s list offers {string}, hinting {string}", async ({ page }, _child: string, label: string, hint: string) => {
	const list = page.getByRole("listbox", { name: "Model" })
	const options = list.getByRole("option")
	await expect(options).toHaveCount(1)
	const hintLocator = options.first().locator('[data-testid="choice-hint"]')
	await expect(hintLocator).toHaveText(hint)
	const ownLabel = await options.first().evaluate((element) => {
		const clone = element.cloneNode(true) as HTMLElement
		clone.querySelector('[data-testid="choice-hint"]')?.remove()
		return clone.textContent.trim()
	})
	expect(ownLabel).toBe(label)
})

Given("the type-ahead {string} question's choices depend on the type-ahead {string} question", async ({ page }, _child: string, _parent: string) => {
	await openForm(page, wingForm("autocomplete", "autocomplete"))
})

When("a reporter types {string}, a value {string} does not offer, for {string}", async ({ page }, typed: string, _parent: string, _again: string) => {
	await answerMake(page, typed)
	await page.keyboard.press("Tab")
})

Then("{string} is enabled and its list offers no choice", async ({ page }, _child: string) => {
	await expect(modelField(page)).toBeEnabled()
	await modelOffers(page, [])
	await expect(page.getByTestId("question-note")).toContainText("Type yours")
})

const sent = new WeakMap<Page, Request>()

When("they type {string} for {string} and send the report", async ({ page }, typed: string, _child: string) => {
	await modelField(page).fill(typed)
	await page.keyboard.press("Tab")
	await page.getByRole("button", { name: "Next" }).click()
	await page.getByRole("radio", { name: "Yes" }).click()

	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: "Submit report" }).click()
	sent.set(page, await request)
})

Then("{string} is sent as {string} and {string} as {string}, both as the words typed", ({ page }, _parent: string, make: string, _child: string, model: string) => {
	const body = present(sent.get(page)).postDataJSON() as { answers: { questionRevisionId: string; value: string | null; choices: string[] | null }[] }
	expect(body.answers.find((answer) => answer.questionRevisionId === "rev-make")).toMatchObject({ value: make, choices: null })
	expect(body.answers.find((answer) => answer.questionRevisionId === "rev-model")).toMatchObject({ value: model, choices: null })
})

/** Saves a report in the browser answering the make and the model, before the page loads. */
async function savedDraft(page: Page, make: string, model: { id: string; label: string }) {
	await page.addInitScript(
		({ make, model }) => {
			localStorage.setItem(
				"hpac.report.draft",
				JSON.stringify({
					locale: "en-CA",
					answers: { "rev-make": { kind: "value", value: make }, "rev-model": { kind: "value", value: model.label, choice: model.id } },
					stepKey: "wing",
					startedAtMs: Date.now() - 60 * 60 * 1000,
					savedAtMs: Date.now() - 60 * 60 * 1000,
				}),
			)
		},
		{ make, model },
	)
}

Given("a reporter answered {string} with {string} and {string} with {string}, and the browser saved the report", async ({ page }, _parent: string, _make: string, _child: string, _model: string) => {
	forms.set(page, wingForm("single_select", "autocomplete"))
	await savedDraft(page, "niviuk", { id: "mentor_7", label: "Mentor 7" })
})

When("they come back and continue the saved report", async ({ page }) => {
	await openForm(page, present(forms.get(page)))
	await page.getByRole("dialog", { name: "Continue where you left off?" }).getByRole("button", { name: "Yes, continue" }).click()
})

Then("{string} holds {string}, and {string} holds {string} and offers only the {string} models", async ({ page }, _parent: string, make: string, _child: string, model: string, _models: string) => {
	await expect(page.getByRole("combobox", { name: "Make" })).toHaveText(make)
	await expect(modelField(page)).toHaveValue(model)
	await modelOffers(page, ["Ikuma", "Mentor 7"])
})

Then("a saved {string} answer no longer linked to the saved {string} answer is restored empty", async ({ browser }, _child: string, _parent: string) => {
	const page = await browser.newPage()
	forms.set(page, wingForm("single_select", "autocomplete"))
	await savedDraft(page, "ozone", { id: "mentor_7", label: "Mentor 7" })
	await openForm(page, present(forms.get(page)))
	await page.getByRole("dialog", { name: "Continue where you left off?" }).getByRole("button", { name: "Yes, continue" }).click()
	await expect(page.getByRole("combobox", { name: "Make" })).toHaveText("Ozone")
	await expect(modelField(page)).toHaveValue("")
	await page.close()
})

Given("a required {string} question's choices depend on an optional {string} question", async ({ page }, _child: string, _parent: string) => {
	await openForm(page, wingForm("single_select", "autocomplete", { modelRequired: true }))
})

When("a reporter leaves {string} unanswered and presses Next", async ({ page }, _parent: string) => {
	await page.getByRole("button", { name: "Next" }).click()
})

Then("the form moves on, because {string} cannot be answered until {string} is", async ({ page }, _child: string, _parent: string) => {
	await expect(page.getByRole("radio", { name: "Yes" })).toBeVisible()
	await expect(page.getByRole("combobox", { name: "Model" })).toHaveCount(0)
})

// ---- One choice under several parent answers (REQ-QB-223, REQ-QB-227) ----

Given(
	"{string} offers {string} under {string} and {string}, {string} under {string}, and {string} under {string}",
	async ({ page }, _child: string, shared: string, first: string, second: string, one: string, oneParent: string, other: string, otherParent: string) => {
		const form = present(forms.get(page))
		const make = form[0].children[0]
		const idOf = (label: string) => present(make.options.find((option) => option.labelEn === label)).id
		form[0].children[1].options = [
			formChoice("shared", shared, [idOf(first), idOf(second)]),
			formChoice("one", one, idOf(oneParent)),
			formChoice("other", other, idOf(otherParent)),
		]
		await stubCurrentQuestions(page, form)
		await page.reload()
	},
)

Then("{string} offers {string} and {string}", async ({ page }, _child: string, first: string, second: string) => {
	await expect(modelField(page)).toBeEnabled()
	await modelOffers(page, [first, second])
})

When("they pick {string} and change {string} to {string}", async ({ page }, model: string, _parent: string, make: string) => {
	const field = modelField(page)
	await field.click()
	await field.fill(model)
	await page.getByRole("listbox", { name: "Model" }).getByRole("option", { name: model, exact: true }).click()
	await expect(modelField(page)).toHaveValue(model)
	await answerMake(page, make)
})

Then("{string} still holds {string} and offers {string} and {string}", async ({ page }, _child: string, model: string, first: string, second: string) => {
	await expect(modelField(page)).toHaveValue(model)
	await modelOffers(page, [first, second])
})

When("the browser saved the report and they come back and continue it", async ({ page }) => {
	await page.reload()
	await page.getByRole("dialog", { name: "Continue where you left off?" }).getByRole("button", { name: "Yes, continue" }).click()
})

Then("{string} holds {string} and {string} holds {string}", async ({ page }, _parent: string, make: string, _child: string, model: string) => {
	await expect(page.getByRole("combobox", { name: "Make" })).toHaveText(make)
	await expect(modelField(page)).toHaveValue(model)
})

When("a reporter answers {string} with {string} and types {string}, which is offered only under {string}", async ({ page }, _parent: string, make: string, typed: string, _other: string) => {
	await answerMake(page, make)
	await modelField(page).fill(typed)
	await page.keyboard.press("Tab")
})

When("they press Next, consent, and send the report", async ({ page }) => {
	await page.getByRole("button", { name: "Next" }).click()
	await page.getByRole("radio", { name: "Yes" }).click()
	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: "Submit report" }).click()
	sent.set(page, await request)
})

Then("{string} is sent as the words {string}", ({ page }, _child: string, typed: string) => {
	const body = present(sent.get(page)).postDataJSON() as { answers: { questionRevisionId: string; value: string | null; choices: string[] | null }[] }
	expect(body.answers.find((answer) => answer.questionRevisionId === "rev-model")).toMatchObject({ value: typed, choices: null })
})

// ---- The type-ahead review page ----

const relinks = new WeakMap<Page, { id: string; body: unknown }[]>()

Given(
	"a Safety Officer reviews the reporter-added {string} value {string}, offered under {string}",
	async ({ page }, child: string, value: string, parentChoice: string) => {
		const choices = [
			{ id: "niviuk", labelEn: "Niviuk", labelFr: "Niviuk", pin: "none" },
			{ id: "ozone", labelEn: "Ozone", labelFr: "Ozone", pin: "none" },
		]
		const values = [
			{
				id: "value-zeno",
				questionId: "model",
				questionLabelEn: child,
				questionLabelFr: child,
				labelEn: value,
				labelFr: null,
				typedIn: "en-CA",
				isRemoved: false,
				answerCount: 1,
				addedAt: "2026-09-26T12:00:00Z",
				mergeTargets: [],
				parent: {
					questionId: "make",
					questionLabelEn: "Make",
					questionLabelFr: "Make",
					parentChoiceIds: [present(choices.find((choice) => choice.labelEn === parentChoice)).id],
					choices,
				},
			},
		]
		const sentLinks: { id: string; body: unknown }[] = []
		relinks.set(page, sentLinks)
		reviewValues.set(page, values)

		await stubAuth(page)
		await page.route("**/api/admin/type-ahead-values/**", async (route) => {
			const url = new URL(route.request().url())
			if (url.pathname.endsWith("/awaiting-review")) {
				return route.fulfill({ json: { values, count: values.length } })
			}
			sentLinks.push({ id: decodeURIComponent(url.pathname.split("/")[4] ?? ""), body: route.request().postDataJSON() })
			return route.fulfill({ status: 204 })
		})
		await signInAs(page, "safety_officer")
		await page.goto("/admin/type-ahead-values")
	},
)

const reviewValues = new WeakMap<Page, { parent: { parentChoiceIds: string[] } }[]>()

Given("{string} is also linked to {string}, a {string} value since removed", async ({ page }, _value: string, removed: string, _parent: string) => {
	// The page lists only the parent's live values, so this link is never shown, counted, or sent.
	present(reviewValues.get(page))[0].parent.parentChoiceIds.push(`${removed.toLowerCase()}-removed`)
	await page.reload()
})

function valueParents(page: Page) {
	return page.getByTestId("type-ahead-value-parent-choice")
}

Then("the value shows that it is offered under {string}", async ({ page }, parentChoice: string) => {
	await expect(page.getByTestId("type-ahead-value-parent")).toContainText(parentChoice)
})

Then("its {string} control lists {string}'s choices", async ({ page }, control: string, _parent: string) => {
	const trigger = valueParents(page).getByRole("combobox")
	await expect(trigger).toHaveAccessibleName(new RegExp(`^${control}`))
	await trigger.click()
	await expect(valueParents(page).locator("li")).toHaveText(["Niviuk", "Ozone"])
})

When("they also tick {string}", async ({ page }, parentChoice: string) => {
	await valueParents(page).getByRole("checkbox", { name: parentChoice }).check()
	await page.keyboard.press("Escape")
	await page.getByRole("button", { name: "Change" }).click()
})

Then("the page sends {string} and {string}", async ({ page }, first: string, second: string) => {
	await expect.poll(() => relinks.get(page)?.length ?? 0).toBe(1)
	const sentIds = (present(relinks.get(page))[0].body as { parentChoiceIds: string[] }).parentChoiceIds
	expect(present(relinks.get(page))[0].id).toBe("value-zeno")
	expect([...sentIds].sort()).toEqual([first.toLowerCase(), second.toLowerCase()].sort())
})

Then("the page does not let them untick the last parent choice, and says why", async ({ page }) => {
	await page.reload()
	await valueParents(page).getByRole("combobox").click()
	const ozone = valueParents(page).getByRole("checkbox", { name: "Ozone" })
	await expect(ozone).toBeChecked()
	await expect(ozone).toBeDisabled()
	await expect(ozone).toHaveAccessibleDescription("A value is offered under at least one answer, so the last one stays ticked.")
	await expect(page.getByRole("button", { name: "Change" })).toBeDisabled()
})

// ---- A child that cannot be answered yet, at submission (REQ-QB-201) ----

When("they consent and send the report", async ({ page }) => {
	await page.getByRole("radio", { name: "Yes" }).click()
	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: "Submit report" }).click()
	sent.set(page, await request)
})

Then("the report is sent with no answer to {string}", ({ page }, _child: string) => {
	const body = present(sent.get(page)).postDataJSON() as { answers: { questionRevisionId: string }[] }
	expect(body.answers.map((answer) => answer.questionRevisionId)).not.toContain("rev-model")
	expect(body.answers.map((answer) => answer.questionRevisionId)).toContain("rev-consent")
})

// ---- A picker child with nothing under the parent's answer (REQ-QB-204) ----

Given(
	"a required single-select {string} question's choices depend on the single-select {string} question, and nothing is offered under {string}",
	async ({ page }, _child: string, _parent: string, empty: string) => {
		await openForm(page, wingForm("single_select", "single_select", { modelRequired: true, makes: ["Niviuk", "Ozone", empty] }))
	},
)

When("a reporter answers {string} with {string}", async ({ page }, _parent: string, make: string) => {
	await answerMake(page, make)
})

Then("{string} is disabled, and says no choice is listed for that answer", async ({ page }, _child: string) => {
	await expect(modelField(page)).toBeDisabled()
	await expect(page.getByTestId("question-note")).toContainText("No choice is listed for your answer to")
})

Then("pressing Next moves on", async ({ page }) => {
	await page.getByRole("button", { name: "Next" }).click()
	await expect(page.getByRole("radio", { name: "Yes" })).toBeVisible()
})

// ---- A reorder the API refuses (REQ-QB-205) ----

When("they move a question whose choices depend on the question above it up, and the API refuses the new order", async ({ page }) => {
	await stubQuestionBank(page, [
		adminQuestion("make", "Make", "single_select", 0, MAKE),
		adminQuestion("model", "Model", "autocomplete", 1, [adminChoice("mentor_7", "Mentor 7", "niviuk")], "make"),
	])
	await page.route("**/api/admin/questions/order", (route) =>
		route.fulfill({
			status: 400,
			contentType: "application/problem+json",
			body: JSON.stringify({
				title: "That change is not allowed.",
				detail: "'Make' must come before 'Model' on the form, because the choices of 'Model' depend on it.",
			}),
		}),
	)
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")
	await rows.nth(1).getByRole("button", { name: "Move up" }).click()
})

Then("the page shows the refusal, naming both questions", async ({ page }) => {
	await expect(page.getByRole("alert")).toContainText("'Make' must come before 'Model'")
})

Then("the list keeps its order", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")
	await expect(rows.nth(0)).toContainText("Make")
	await expect(rows.nth(1)).toContainText("Model")
})
