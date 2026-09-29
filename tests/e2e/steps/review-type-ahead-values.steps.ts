import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"

const { Given, When, Then } = createBdd()

/*
 * REQ-MOD-095 and REQ-MOD-097: a Safety Officer reviews reporter-added type-ahead values on
 * one page (ADR-0129). The API is stubbed at the network boundary: what each
 * review does to the value, and who may do it, is REQ-QB-128..135 and the
 * API tests. Every value here is synthetic.
 */

interface StubValue {
	id: string
	questionId: string
	questionLabelEn: string
	questionLabelFr: string
	labelEn: string | null
	labelFr: string | null
	typedIn: string | null
	isRemoved: boolean
	answerCount: number
	addedAt: string | null
	mergeTargets: { id: string; labelEn: string | null; labelFr: string | null; pin?: string }[]
	parent?: {
		questionId: string
		questionLabelEn: string
		questionLabelFr: string
		parentChoiceIds: string[]
		choices: { id: string; labelEn: string | null; labelFr: string | null; pin: string }[]
	}
}

const WHERE = { questionId: "q-where", questionLabelEn: "Where did this happen?", questionLabelFr: "Où cela s'est-il produit ?" }

const LAUNCH = { questionId: "q-launch", questionLabelEn: "Where did you launch?", questionLabelFr: "D'où avez-vous décollé ?" }

function flaggedValues(): StubValue[] {
	return [
		{ ...WHERE, id: "value-mount7", labelEn: "Mount 7", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 2, addedAt: "2026-09-20T12:00:00Z", mergeTargets: [] },
		{ ...WHERE, id: "value-coopers", labelEn: "coopers", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 1, addedAt: "2026-09-21T12:00:00Z", mergeTargets: [] },
		{ ...WHERE, id: "value-test", labelEn: "Test site", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 3, addedAt: "2026-09-22T12:00:00Z", mergeTargets: [] },
	]
}

function duplicateValues(): StubValue[] {
	const coopersHill = { id: "value-coopers-apostrophe", labelEn: "Cooper's", labelFr: null }
	const coopers = { id: "value-coopers-plain", labelEn: "Coopers", labelFr: null }
	return [
		{ ...WHERE, ...coopers, typedIn: "en-CA", isRemoved: false, answerCount: 2, addedAt: "2026-09-20T12:00:00Z", mergeTargets: [coopersHill] },
		{ ...WHERE, ...coopersHill, typedIn: "en-CA", isRemoved: false, answerCount: 5, addedAt: "2026-09-21T12:00:00Z", mergeTargets: [coopers] },
		{ ...LAUNCH, id: "value-sainte-anne", labelEn: null, labelFr: "Élévation Sainte-Anne", typedIn: "fr-CA", isRemoved: false, answerCount: 1, addedAt: "2026-09-22T12:00:00Z", mergeTargets: [] },
	]
}

const PARENT = { questionId: "q-launch-method", questionLabelEn: "Launch method", questionLabelFr: "Méthode de décollage" }

/**
 * Twenty flagged values under one question, "Site 1".."Site 20" — long enough
 * to scroll. "Site 15" carries a merge target and a dependent parent, so every
 * review action (approve, correct, remove, merge, relink) can act on the same
 * row (issue no. 651).
 */
function twentyValues(): StubValue[] {
	return Array.from({ length: 20 }, (_, index) => {
		const site = index + 1
		const value: StubValue = {
			...WHERE,
			id: `value-site-${site}`,
			labelEn: `Site ${site}`,
			labelFr: null,
			typedIn: "en-CA",
			isRemoved: false,
			answerCount: 1,
			addedAt: `2026-09-${String(site).padStart(2, "0")}T12:00:00Z`,
			mergeTargets: [],
		}
		if (site === 15) {
			value.mergeTargets = [{ id: "value-site-16", labelEn: "Site 16", labelFr: null, pin: "none" }]
			value.parent = {
				...PARENT,
				parentChoiceIds: ["choice-a"],
				choices: [
					{ id: "choice-a", labelEn: "Choice A", labelFr: null, pin: "none" },
					{ id: "choice-b", labelEn: "Choice B", labelFr: null, pin: "none" },
				],
			}
		}
		return value
	})
}

type Review = { method: string; id: string; body: unknown }

const reviews = new WeakMap<Page, Review[]>()
const scrollBefore = new WeakMap<Page, number>()

Given("a signed-in Safety Officer and two type-ahead questions with values flagged for review", async ({ page }) => {
	await reviewPage(page, duplicateValues())
})

When('they merge "Coopers" into "Cooper\'s"', async ({ page }) => {
	const coopers = valueRow(page, "Coopers")
	await coopers.getByLabel("Merge into…").selectOption({ label: "Cooper's" })
	await coopers.getByRole("button", { name: "Merge" }).click()
})

Then('"Coopers" leaves the list', async ({ page }) => {
	await expect(valueRow(page, "Coopers")).toHaveCount(0)
	expect(reviews.get(page)).toEqual([{ method: "POST", id: "value-coopers-plain", body: { intoId: "value-coopers-apostrophe" } }])
})

Then('"Cooper\'s" is no longer flagged', async ({ page }) => {
	await expect(valueRow(page, "Cooper's")).toHaveCount(0)
	await expect(valueRow(page, "Élévation Sainte-Anne")).toHaveCount(1)
})

Then("every flagged value is listed under its question's heading, with its language and how many answers name it", async ({ page }) => {
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(3)
	await expect(page.getByRole("heading", { name: "Where did you launch?" })).toBeVisible()
	const sainteAnne = valueRow(page, "Élévation Sainte-Anne")
	await expect(sainteAnne).toContainText("Typed in fr-CA")
	await expect(valueRow(page, "Cooper's")).toContainText("Answers naming it: 5")
})

Given("a signed-in Safety Officer and three type-ahead values flagged for review", async ({ page }) => {
	await reviewPage(page, flaggedValues())
})

/**
 * Stubs the translation endpoint the same way `manage-questions.steps.ts`
 * does: the prefix makes a translation obviously machine-made, so a scenario
 * asserts a field was filled from the other language rather than the
 * quality of any French (REQ-MOD-166..170, ADR-0141, ADR-0144).
 */
async function stubTranslation(page: Page, { available = true }: { available?: boolean } = {}) {
	await page.route("**/api/admin/translate", async (route) => {
		if (route.request().method() === "GET") {
			return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ available }) })
		}

		const { texts, to } = JSON.parse(route.request().postData() ?? "{}") as { texts: string[]; to: string }
		return route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({ texts: texts.map((text) => (text ? `[${to}] ${text}` : "")) }),
		})
	})
}

/**
 * Stubs the review API with these values. A review removes the value it names
 * from the list; here a merge also removes its target — the shape most of
 * this suite's scenarios exercise. REQ-MOD-163 stubs a merge that leaves an
 * independently-flagged target in place instead (ADR-0129).
 */
async function reviewPage(page: Page, values: StubValue[], options: { translation?: boolean } = {}) {
	const waiting = values
	const sent: Review[] = []
	reviews.set(page, sent)

	await stubAuth(page)
	await stubTranslation(page, { available: options.translation ?? true })
	await page.route("**/api/admin/type-ahead-values/**", async (route) => {
		const request = route.request()
		const url = new URL(request.url())

		if (url.pathname.endsWith("/awaiting-review")) {
			return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ values: waiting, count: waiting.length }) })
		}

		const id = decodeURIComponent(url.pathname.split("/")[4] ?? "")
		const body = request.postDataJSON() as { intoId?: string } | null
		sent.push({ method: request.method(), id, body })
		for (const reviewed of [id, url.pathname.endsWith("/merge") ? body?.intoId : undefined]) {
			const index = waiting.findIndex((value) => value.id === reviewed)
			if (index >= 0) waiting.splice(index, 1)
		}
		return route.fulfill({ status: 204 })
	})
	await signInAs(page, "safety_officer")
}

When("they open the review-type-ahead-values page", async ({ page }) => {
	await page.goto("/admin/type-ahead-values")
	// English by default; REQ-MOD-161's French examples switch the locale first,
	// so this matches either language's page-1 heading.
	await expect(page.getByRole("heading", { level: 1, name: /^(Type-ahead values to review|Valeurs de saisie semi-automatique)/ })).toBeVisible()
})

const valueRow = (page: Page, wording: string) =>
	page.getByTestId("type-ahead-value").filter({ has: page.getByTestId("type-ahead-value-wording").getByText(wording, { exact: true }) })

Then("each value is listed under its question's heading, with the language it was typed in and how many answers name it", async ({ page }) => {
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(3)
	await expect(page.getByRole("heading", { name: "Where did this happen?" })).toBeVisible()
	const mount7 = valueRow(page, "Mount 7")
	await expect(mount7).toContainText("Typed in en-CA")
	await expect(mount7).toContainText("Answers naming it: 2")
})

When('they approve "Mount 7", correct "coopers" to "Cooper\'s", and remove "Test site"', async ({ page }) => {
	await valueRow(page, "Mount 7").getByRole("button", { name: "Approve" }).click()
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(2)

	await valueRow(page, "coopers").getByRole("button", { name: "Correct" }).click()
	await page.getByLabel("English wording").fill("Cooper's")
	await page.getByRole("button", { name: "Save correction" }).click()
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(1)

	await valueRow(page, "Test site").getByRole("button", { name: "Remove" }).click()
})

Then("the API is asked to approve, correct, and remove exactly those values", async ({ page }) => {
	await expect.poll(() => reviews.get(page)!.length).toBe(3)
	const [approve, correct, remove] = reviews.get(page)!

	expect(approve).toMatchObject({ method: "POST", id: "value-mount7" })
	expect(correct).toMatchObject({ method: "PUT", id: "value-coopers", body: { labelEn: "Cooper's", labelFr: "" } })
	expect(remove).toMatchObject({ method: "DELETE", id: "value-test" })
})

Then("the page lists no value left to review", async ({ page }) => {
	await expect(page.getByText("No type-ahead values are waiting for review.")).toBeVisible()
})

// ------------------------ grouped by question, A→Z (REQ-MOD-160, REQ-MOD-161) --

Given(
	"a signed-in Safety Officer and flagged values under two type-ahead questions, returned by the API with the later question first",
	async ({ page }) => {
		await reviewPage(page, [
			{ ...LAUNCH, id: "value-launch-zephyr", labelEn: "Zephyr Ridge", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 1, addedAt: "2026-09-20T12:00:00Z", mergeTargets: [] },
			{ ...WHERE, id: "value-where-coopers", labelEn: "Cooper's", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 1, addedAt: "2026-09-21T12:00:00Z", mergeTargets: [] },
		])
	},
)

Then(
	'the question headings read, top to bottom, "Where did this happen?" then "Where did you launch?"',
	async ({ page }) => {
		await expect(page.getByRole("heading", { level: 2 })).toHaveText(["Where did this happen?", "Where did you launch?"])
	},
)

const REQ_MOD_161_QUESTION = { questionId: "q-site-name", questionLabelEn: "Site name", questionLabelFr: "Nom du site" }

Given(
	/^a Safety Officer who reads (English|French) and one type-ahead question whose flagged values are "([^"]+)", "([^"]+)", and "([^"]+)", in that order$/,
	async ({ page, context }, language: string, second: string, first: string, third: string) => {
		const isFrench = language === "French"
		if (isFrench) await context.addInitScript(() => localStorage.setItem("hpac.locale", "fr-CA"))

		const values: StubValue[] = [second, first, third].map((wording, index) => ({
			...REQ_MOD_161_QUESTION,
			id: `value-order-${index}`,
			labelEn: isFrench ? null : wording,
			labelFr: isFrench ? wording : null,
			typedIn: isFrench ? "fr-CA" : "en-CA",
			isRemoved: false,
			answerCount: 1,
			addedAt: "2026-09-20T12:00:00Z",
			mergeTargets: [],
		}))
		await reviewPage(page, values)
	},
)

Then(
	/^the values under that question's heading read, top to bottom, "([^"]+)", "([^"]+)", and "([^"]+)"$/,
	async ({ page }, first: string, second: string, third: string) => {
		await expect(page.getByTestId("type-ahead-value-wording")).toHaveText([first, second, third])
	},
)

// ------------------------ every action keeps the reviewer's place (REQ-MOD-162, REQ-MOD-163) --

Given("a signed-in Safety Officer and twenty flagged values under one type-ahead question", async ({ page }) => {
	await reviewPage(page, twentyValues())
})

When(/^they scroll to "([^"]+)"$/, async ({ page }, wording: string) => {
	await valueRow(page, wording).scrollIntoViewIfNeeded()
	scrollBefore.set(page, await page.evaluate(() => window.scrollY))
})

When(
	/^they (approve|remove|correct|merge|relink) "([^"]+)"$/,
	async ({ page }, action: string, wording: string) => {
		const row = valueRow(page, wording)
		switch (action) {
			case "approve":
				await row.getByRole("button", { name: "Approve" }).click()
				break
			case "remove":
				await row.getByRole("button", { name: "Remove" }).click()
				break
			case "correct":
				await row.getByRole("button", { name: "Correct" }).click()
				await page.getByLabel("English wording").fill(`${wording} corrected`)
				await page.getByRole("button", { name: "Save correction" }).click()
				break
			case "merge":
				await row.getByLabel("Merge into…").selectOption({ label: "Site 16" })
				await row.getByRole("button", { name: "Merge" }).click()
				break
			case "relink": {
				const parentControl = row.getByTestId("type-ahead-value-parent-choice")
				await parentControl.getByRole("button").click()
				await parentControl.getByRole("checkbox", { name: "Choice B" }).check()
				await page.keyboard.press("Escape")
				await row.getByRole("button", { name: "Change" }).click()
				break
			}
		}
	},
)

Then("the page never shows the loading text", async ({ page }) => {
	await expect(page.getByText("Loading values…")).toHaveCount(0)
})

Then("the scroll position is unchanged", async ({ page }) => {
	await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scrollBefore.get(page))
})

Given(
	"a signed-in Safety Officer and two flagged values of the same question, one also awaiting review in its own right",
	async ({ page }) => {
		await mergeKeepsIndependentlyFlaggedTarget(page)
	},
)

/**
 * The real server only marks the merge's source as merged (deleted); it never
 * marks the target reviewed (`Question.MergeValue` in
 * `src/HpacSafety.Core/Features/QuestionBank/Question.cs`). So a target that
 * was independently flagged stays in the queue, its answer count reflecting
 * the merge (REQ-MOD-163) — unlike `reviewPage`'s default stub, which removes
 * both for the scenarios that expect that shape.
 */
async function mergeKeepsIndependentlyFlaggedTarget(page: Page) {
	const coopersHill = { id: "value-coopers-apostrophe", labelEn: "Cooper's", labelFr: null }
	const coopers = { id: "value-coopers-plain", labelEn: "Coopers", labelFr: null }
	let waiting: StubValue[] = [
		{ ...WHERE, ...coopers, typedIn: "en-CA", isRemoved: false, answerCount: 2, addedAt: "2026-09-20T12:00:00Z", mergeTargets: [coopersHill] },
		{ ...WHERE, ...coopersHill, typedIn: "en-CA", isRemoved: false, answerCount: 3, addedAt: "2026-09-21T12:00:00Z", mergeTargets: [coopers] },
	]
	const sent: Review[] = []
	reviews.set(page, sent)

	await stubAuth(page)
	await page.route("**/api/admin/type-ahead-values/**", async (route) => {
		const request = route.request()
		const url = new URL(request.url())

		if (url.pathname.endsWith("/awaiting-review")) {
			return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ values: waiting, count: waiting.length }) })
		}

		const id = decodeURIComponent(url.pathname.split("/")[4] ?? "")
		const body = request.postDataJSON() as { intoId?: string } | null
		sent.push({ method: request.method(), id, body })
		if (url.pathname.endsWith("/merge") && body?.intoId) {
			const target = waiting.find((value) => value.id === body.intoId)
			const source = waiting.find((value) => value.id === id)
			if (target && source) target.answerCount += source.answerCount
		}
		waiting = waiting.filter((value) => value.id !== id)
		return route.fulfill({ status: 204 })
	})
	await signInAs(page, "safety_officer")
}

Then('"Coopers" leaves the list, and the rows around it stay where they are', async ({ page }) => {
	await expect(valueRow(page, "Coopers")).toHaveCount(0)
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(1)
})

Then('"Cooper\'s" is still listed, showing 5 answers naming it', async ({ page }) => {
	await expect(valueRow(page, "Cooper's")).toContainText("Answers naming it: 5")
})

// ------------------------ merge targets listed as the form lists them (ADR-0136) --

Given(
	"a signed-in Safety Officer reviews a type-ahead value whose question offers {string} pinned last, and {string} and {string} not pinned",
	async ({ page }, last: string, first: string, second: string) => {
		// The server's order: unpinned by ID, then pinned last — not alphabetical.
		const mergeTargets = [
			{ id: "value-a", labelEn: first, labelFr: null, pin: "none" },
			{ id: "value-b", labelEn: second, labelFr: null, pin: "none" },
			{ id: "value-c", labelEn: last, labelFr: null, pin: "last" },
		]
		await reviewPage(page, [
			{ ...WHERE, id: "value-new", labelEn: "Mount Seven", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 1, addedAt: "2026-09-20T12:00:00Z", mergeTargets },
		])
		await page.goto("/admin/type-ahead-values")
	},
)

Then(
	"the value can be merged into {string}, {string}, or {string}, in that order",
	async ({ page }, first: string, second: string, third: string) => {
		await expect(valueRow(page, "Mount Seven").getByLabel("Merge into…").locator("option")).toHaveText([
			"Merge into…",
			first,
			second,
			third,
		])
	},
)

// ------------------------ re-translating a value's wording (REQ-MOD-166..170) --

// The row currently being corrected. Its heading text stays the value's saved
// wording — untouched by the draft — for as long as correction is open, the
// same as `valueRow` finds any other row (issue no. 651's pattern).
const correcting = new WeakMap<Page, string>()
const theValueRow = (page: Page) => valueRow(page, correcting.get(page) ?? "")
const translateButton = (row: ReturnType<typeof valueRow>) => row.getByRole("button", { name: "Translate", exact: true })
const directionSwitch = (row: ReturnType<typeof valueRow>) =>
	row.getByRole("button", { name: /^Translate (English to French|French to English)$/ })

Given(
	"a signed-in Safety Officer and three type-ahead values flagged for review, on a server with no translation provider",
	async ({ page }) => {
		await reviewPage(page, flaggedValues(), { translation: false })
	},
)

When(/^they begin correcting "([^"]+)"$/, async ({ page }, wording: string) => {
	correcting.set(page, wording)
	await valueRow(page, wording).getByRole("button", { name: "Correct" }).click()
})

When(/^they edit its English wording to "([^"]+)"$/, async ({ page }, wording: string) => {
	await theValueRow(page).getByLabel("English wording").fill(wording)
})

When(
	/^they begin correcting "([^"]+)", edit its English wording to "([^"]+)", and press Translate$/,
	async ({ page }, original: string, edited: string) => {
		correcting.set(page, original)
		await valueRow(page, original).getByRole("button", { name: "Correct" }).click()
		await theValueRow(page).getByLabel("English wording").fill(edited)
		await translateButton(theValueRow(page)).click()
	},
)

When("they edit that value's English wording again", async ({ page }) => {
	await theValueRow(page).getByLabel("English wording").fill("Cooper's Hill")
})

Then("that value's Translate action is unavailable", async ({ page }) => {
	await expect(translateButton(theValueRow(page))).toBeDisabled()
})

Then("that value's Translate action becomes available", async ({ page }) => {
	await expect(translateButton(theValueRow(page))).toBeEnabled()
})

Then("that value's Translate action is unavailable and says why", async ({ page }) => {
	await expect(translateButton(theValueRow(page))).toBeDisabled()
	await expect(theValueRow(page).getByText("Translation is not available on this server.")).toBeVisible()
})

Then("that value's French field is filled with the translation and remains editable", async ({ page }) => {
	const french = theValueRow(page).getByLabel("French wording")

	await expect(french).toHaveValue("[fr-CA] Cooper's")
	await expect(french).not.toHaveAttribute("readonly", "")
	await french.fill("Cooper's")
	await expect(french).toHaveValue("Cooper's")
})

Then("nothing is saved until they press Save correction", async ({ page }) => {
	expect(reviews.get(page)?.length ?? 0).toBe(0)

	await theValueRow(page).getByRole("button", { name: "Save correction" }).click()
	await expect.poll(() => reviews.get(page)!.length).toBe(1)
	expect(reviews.get(page)![0]).toMatchObject({ method: "PUT", id: "value-coopers" })
})

Then("that value's direction switch translates English to French", async ({ page }) => {
	await expect(directionSwitch(theValueRow(page))).toHaveAccessibleName("Translate English to French")
})

When("they flip that value's direction switch to French to English", async ({ page }) => {
	await directionSwitch(theValueRow(page)).click()
	await expect(directionSwitch(theValueRow(page))).toHaveAccessibleName("Translate French to English")
})

When(/^they write its French wording as "([^"]+)"$/, async ({ page }, wording: string) => {
	await theValueRow(page).getByLabel("French wording").fill(wording)
})

When(/^they write its French wording as "([^"]+)" and press Translate$/, async ({ page }, wording: string) => {
	await theValueRow(page).getByLabel("French wording").fill(wording)
	await translateButton(theValueRow(page)).click()
})

Then("that value's English field is filled with the translation", async ({ page }) => {
	await expect(theValueRow(page).getByLabel("English wording")).toHaveValue("[en-CA] Site d'essai")
})

