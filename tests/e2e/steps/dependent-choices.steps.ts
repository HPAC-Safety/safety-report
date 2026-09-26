import { createBdd } from "playwright-bdd"
import { expect, type Page, type Request } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions, stubSubmission, type StubQuestion } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * REQ-QB-195 to REQ-QB-202: a picker or type-ahead's choices depending on
 * another's answer, in the editor, on the report form, and on the type-ahead
 * review page (ADR-0146). The API is stubbed at the network boundary; what the
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
	parentChoiceId: string | null
}

function adminChoice(id: string, label: string, parentChoiceId: string | null = null, pin = "none"): AdminChoice {
	return { id, code: id, labelEn: label, labelFr: label, addedByReporter: false, needsTranslation: false, reporterLocale: null, pin, parentChoiceId }
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

type SavedOption = { code: string | null; labelEn: string; parentChoiceId?: string | null }
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
		const current = questions[index]!
		questions[index] = {
			...current,
			choicesDependOnQuestionId: body.choicesDependOnQuestionId,
			options: body.options.map((option) => {
				const existing = current.options.find((candidate) => candidate.code === option.code)
				return { ...(existing ?? adminChoice(option.labelEn.toLowerCase(), option.labelEn)), parentChoiceId: option.parentChoiceId ?? existing?.parentChoiceId ?? null }
			}),
		}
		await route.fulfill({ json: questions[index] })
	})
	await page.reload()
}

function lastSave(page: Page): Saved {
	const all = saves.get(page) ?? []
	expect(all.length).toBeGreaterThan(0)
	return all[all.length - 1]!
}

async function editQuestion(page: Page, label: string) {
	await page.getByRole("listitem").filter({ hasText: label }).getByRole("button", { name: "Edit" }).click()
}

const MAKE = [adminChoice("niviuk", "Niviuk"), adminChoice("ozone", "Ozone")]

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
	for (const select of await page.getByTestId("question-choice-parent").all()) {
		await select.selectOption({ label: "Niviuk" })
	}
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the save names {string} as the question its choices depend on", async ({ page }, _parent: string) => {
	const save = lastSave(page)
	expect(save.choicesDependOnQuestionId).toBe("make")
	expect(save.options.map((option) => option.parentChoiceId)).toEqual(["niviuk", "niviuk"])
})

When("they clear {string} and save", async ({ page }, _control: string) => {
	await editQuestion(page, "Harness")
	await page.getByLabel("Choices depend on the answer to").selectOption({ label: "No other question" })
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the save names no parent question and keeps every choice's link", async ({ page }) => {
	const save = lastSave(page)
	expect(save.choicesDependOnQuestionId).toBeNull()
	expect(save.options.map((option) => option.parentChoiceId)).toEqual(["niviuk", "niviuk"])
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
	"every choice row, a new one included, has a required parent-choice control listing {string}, {string}, {string}",
	async ({ page }, first: string, second: string, third: string) => {
		const controls = page.getByTestId("question-choice-parent")
		await expect(controls).toHaveCount(3)
		for (const control of await controls.all()) {
			await expect(control).toHaveAttribute("required", "")
			await expect(control.locator("option:not([value=''])")).toHaveText([first, second, third])
		}
	},
)

Then("Save is refused while a choice has no parent choice, naming that choice", async ({ page }) => {
	const controls = page.getByTestId("question-choice-parent")
	await controls.nth(0).selectOption({ label: "Niviuk" })
	await controls.nth(1).selectOption({ label: "Ozone" })
	await expect(page.getByRole("button", { name: "Save" })).toBeDisabled()
	await expect(page.getByTestId("question-choices-unlinked")).toContainText("Zeno 2")
	await expect(page.getByTestId("question-choices-unlinked")).not.toContainText("Mentor 7")
})

When("they pick a parent choice for every row and save", async ({ page }) => {
	await page.getByTestId("question-choice-parent").nth(2).selectOption({ label: "Ozone" })
	await expect(page.getByTestId("question-choices-unlinked")).toHaveCount(0)
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the save sends each choice with the parent choice picked for it", async ({ page }) => {
	const save = lastSave(page)
	expect(save.choicesDependOnQuestionId).toBe("make")
	expect(save.options.map((option) => [option.labelEn, option.parentChoiceId])).toEqual([
		["Mentor 7", "niviuk"],
		["Rush 6", "ozone"],
		["Zeno 2", "ozone"],
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

function formChoice(id: string, label: string, parentChoiceId: string | null = null) {
	return { id, code: id, labelEn: label, labelFr: label, onlyIn: null, pin: "none", parentChoiceId }
}

const MODELS = [
	formChoice("mentor_7", "Mentor 7", "niviuk"),
	formChoice("ikuma", "Ikuma", "niviuk"),
	formChoice("rush_6", "Rush 6", "ozone"),
]

/** One page asking the make and the model together, then publication consent. */
function wingForm(makeType: string, modelType: string, { modelRequired = false } = {}): StubQuestion[] {
	return [
		formQuestion({
			id: "wing",
			labelEn: "Your wing",
			type: "group",
			displayOrder: 0,
			children: [
				formQuestion({ id: "make", labelEn: "Make", type: makeType, displayOrder: 1, options: [formChoice("niviuk", "Niviuk"), formChoice("ozone", "Ozone")] }),
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
	return type === "autocomplete" ? page.getByRole("combobox", { name: "Model" }) : page.getByLabel("Model", { exact: true })
}

async function modelOffers(page: Page): Promise<string[]> {
	const type = forms.get(page)?.[0]?.children[1]?.type
	if (type === "autocomplete") {
		await page.getByRole("button", { name: /Show choices|Afficher les choix|#Show choices/ }).last().click()
		const offered = await page.getByRole("listbox", { name: "Model" }).getByRole("option").allTextContents()
		await page.keyboard.press("Escape")
		return offered
	}
	return (await page.getByLabel("Model", { exact: true }).locator("option:not([value=''])").allTextContents()).filter((text) => !text.startsWith("─"))
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
		await page.getByLabel("Make", { exact: true }).selectOption({ label: make })
	}
}

Given(
	"a {word} {string} question's choices depend on a single-select {string} question offering {string} and {string}",
	async ({ page }, childType: string, _child: string, _make: string, _first: string, _second: string) => {
		forms.set(page, wingForm("single_select", childType))
	},
)

Given("{string} offers {string} and {string} linked to {string}, and {string} linked to {string}", async ({}, _child: string, _first: string, _second: string, _niviuk: string, _third: string, _ozone: string) => {
	// The form above already offers exactly these, linked this way.
})

When("a reporter using {word} opens the page asking both", async ({ page }, language: string) => {
	await openForm(page, forms.get(page)!, language)
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
	expect(await modelOffers(page)).toEqual([first, second])
})

Given("the type-ahead {string} question's choices depend on the single-select {string} question", async ({ page }, _child: string, _parent: string) => {
	await openForm(page, wingForm("single_select", "autocomplete"))
})

Given("a reporter answered {string} with {string} and picked {string} for {string}", async ({ page }, _parent: string, make: string, model: string, _child: string) => {
	await answerMake(page, make)
	await page.getByRole("button", { name: "Show choices" }).last().click()
	await page.getByRole("listbox", { name: "Model" }).getByRole("option", { name: model }).click()
	await expect(modelField(page)).toHaveValue(model)
})

When("they change {string} to {string}", async ({ page }, _parent: string, make: string) => {
	await answerMake(page, make)
})

Then("{string} is empty and offers only {string}", async ({ page }, _child: string, only: string) => {
	await expect(modelField(page)).toHaveValue("")
	expect(await modelOffers(page)).toEqual([only])
})

When("they type {string}, a value {string} does not offer, and change {string} to {string}", async ({ page }, typed: string, _child: string, _parent: string, make: string) => {
	await modelField(page).fill(typed)
	await page.keyboard.press("Tab")
	await answerMake(page, make)
})

Then("{string} still holds {string}", async ({ page }, _child: string, typed: string) => {
	await expect(modelField(page)).toHaveValue(typed)
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
	expect(await modelOffers(page)).toEqual([])
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

Then("{string} is sent as {string} and {string} as {string}, both as the words typed", async ({ page }, _parent: string, make: string, _child: string, model: string) => {
	const body = sent.get(page)!.postDataJSON() as { answers: { questionRevisionId: string; value: string | null; choices: string[] | null }[] }
	expect(body.answers.find((answer) => answer.questionRevisionId === "rev-make")).toMatchObject({ value: make, choices: null })
	expect(body.answers.find((answer) => answer.questionRevisionId === "rev-model")).toMatchObject({ value: model, choices: null })
})

/** Saves a report in the browser answering the make and the model, before the page loads. */
async function savedDraft(page: Page, make: string, model: string) {
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
	await openForm(page, forms.get(page)!)
	await page.getByRole("dialog", { name: "Continue where you left off?" }).getByRole("button", { name: "Yes, continue" }).click()
})

Then("{string} holds {string}, and {string} holds {string} and offers only the {string} models", async ({ page }, _parent: string, make: string, _child: string, model: string, _models: string) => {
	await expect(page.getByLabel("Make", { exact: true }).locator("option:checked")).toHaveText(make)
	await expect(modelField(page)).toHaveValue(model)
	expect(await modelOffers(page)).toEqual(["Ikuma", "Mentor 7"])
})

Then("a saved {string} answer no longer linked to the saved {string} answer is restored empty", async ({ browser }, _child: string, _parent: string) => {
	const page = await browser.newPage()
	forms.set(page, wingForm("single_select", "autocomplete"))
	await savedDraft(page, "ozone", { id: "mentor_7", label: "Mentor 7" })
	await openForm(page, forms.get(page)!)
	await page.getByRole("dialog", { name: "Continue where you left off?" }).getByRole("button", { name: "Yes, continue" }).click()
	await expect(page.getByLabel("Make", { exact: true }).locator("option:checked")).toHaveText("Ozone")
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

// ---- The type-ahead review page ----

const relinks = new WeakMap<Page, { id: string; body: unknown }[]>()

Given(
	"a signed-in Safety Officer reviews the reporter-added {string} value {string}, linked to {string}",
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
					parentChoiceId: choices.find((choice) => choice.labelEn === parentChoice)!.id,
					choices,
				},
			},
		]
		const sentLinks: { id: string; body: unknown }[] = []
		relinks.set(page, sentLinks)

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

Then("the value shows that it is linked to {string}", async ({ page }, parentChoice: string) => {
	await expect(page.getByTestId("type-ahead-value-parent")).toContainText(parentChoice)
})

Then("its link control lists {string}'s choices and offers no empty choice", async ({ page }, _parent: string) => {
	await expect(page.getByTestId("type-ahead-value-parent-choice").locator("option")).toHaveText(["Niviuk", "Ozone"])
})

When("they link it to {string}", async ({ page }, parentChoice: string) => {
	await page.getByTestId("type-ahead-value-parent-choice").selectOption({ label: parentChoice })
	await page.getByRole("button", { name: "Change" }).click()
})

Then("the page sends the new link", async ({ page }) => {
	await expect.poll(() => relinks.get(page)?.length ?? 0).toBe(1)
	expect(relinks.get(page)![0]).toEqual({ id: "value-zeno", body: { parentChoiceId: "niviuk" } })
})
