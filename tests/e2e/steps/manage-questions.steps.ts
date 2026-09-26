import { createBdd } from "playwright-bdd"

import { signInAs } from "./auth"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

// The last question body each page sent, for the scenario that asserts what
// the editor puts on the wire rather than what the stub answers.
const savedBodies = new WeakMap<Page, { options: { code: string | null }[] }>()

/*
 * The @ui scenarios for the manage-questions page (ADR-0053).
 *
 * The admin API is stubbed at the network boundary rather than run for real:
 * Playwright here drives the built static site (see playwright.config.ts's
 * preview server), and what these scenarios assert is browser-observable
 * behaviour — which controls appear, what the list shows, and that reordering
 * works from the keyboard. The server side of the same behaviour is covered by
 * HpacSafety.Api.Tests, which runs against a real PostgreSQL container, per
 * ADR-0045.
 *
 * Every question below is synthetic.
 */

interface StubOption {
	id: string
	code: string
	labelEn: string | null
	labelFr: string | null
	addedByReporter: boolean
	needsTranslation: boolean
	reporterLocale: string | null
	/** `first`, `last`, or `none` (ADR-0136). */
	pin: string
}

/** A choice an administrator wrote, complete in both languages. */
function written(code: string, labelEn: string, labelFr: string, pin = "none"): StubOption {
	return { id: `choice-${code}`, code, labelEn, labelFr, addedByReporter: false, needsTranslation: false, reporterLocale: null, pin }
}

interface StubQuestion {
	id: string
	key: string
	revisionId: string
	revisionNumber: number
	type: string
	isSystem: boolean
	isRequired: boolean
	isPrivate: boolean
	isTranslatable: boolean
	isActive: boolean
	displayOrder: number
	dependsOnQuestionId: string | null
	dependsOnChoiceId: string | null
	labelEn: string
	labelFr: string
	helpTextEn: string | null
	helpTextFr: string | null
	placeholderEn: string | null
	placeholderFr: string | null
	options: StubOption[]
	reporterChoicesAwaitingReview: number
	hasBeenAnswered: boolean
}

function question(
	id: string,
	key: string,
	labelEn: string,
	type: string,
	displayOrder: number,
	options: StubOption[] = [],
): StubQuestion {
	return {
		id,
		key,
		revisionId: `rev${id}`,
		revisionNumber: 1,
		type,
		isSystem: false,
		isRequired: false,
		isPrivate: true,
		isTranslatable: type === "long_text",
		isActive: true,
		displayOrder,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		labelEn,
		labelFr: `${labelEn} (fr)`,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options,
		reporterChoicesAwaitingReview: awaitingReview(options),
		hasBeenAnswered: false,
	}
}

function awaitingReview(options: StubOption[]) {
	return options.filter((option) => option.addedByReporter && option.needsTranslation).length
}

function codeOf(wording: string) {
	return wording
		.toLowerCase()
		.normalize("NFD")
		.replace(/[\u0300-\u036f]/g, "")
		.replace(/[^a-z0-9]+/g, "_")
		.replace(/^_+|_+$/g, "")
}

/**
 * A fake admin API held in the test process. It behaves the way the real one
 * does in the ways these scenarios depend on: an edit or a reorder writes a
 * new revision number, and the list comes back in display order.
 */
async function stubAdminApi(page: Page) {
	const questions: StubQuestion[] = [
		question("aaaaaaaaaaa", "were_you_injured", "Were you injured?", "yes_no", 0),
		question("bbbbbbbbbbb", "occurrence_notes", "What happened?", "long_text", 1),
		// In the server's order — pinned last at the end, the rest by ID — which
		// is not alphabetical (ADR-0136).
		question("ddddddddddd", "aircraft_type", "Hang glider or paraglider?", "single_select", 2, [
			written("paraglider", "Paraglider", "Parapente"),
			written("hang_glider", "Hang glider", "Deltaplane"),
			written("other", "Other", "Autre", "last"),
		]),
		// A type-ahead a reporter has added a site to, still missing its French.
		question("eeeeeeeeeee", "launch_site", "Where did you launch?", "autocomplete", 3, [
			written("coopers", "Cooper's", "Cooper's"),
			{
				id: "choice-mount_7",
				code: "mount_7",
				labelEn: "mount 7",
				labelFr: null,
				addedByReporter: true,
				needsTranslation: true,
				reporterLocale: "en-CA",
				pin: "none",
			},
		]),
	]

	await page.route("**/api/admin/questions/order", async (route) => {
		const { questionIdsInOrder } = JSON.parse(route.request().postData() ?? "{}") as {
			questionIdsInOrder: string[]
		}

		questionIdsInOrder.forEach((id, position) => {
			const moved = questions.find((candidate) => candidate.id === id)
			if (moved && moved.displayOrder !== position) {
				moved.displayOrder = position
				moved.revisionNumber += 1
			}
		})

		questions.sort((left, right) => left.displayOrder - right.displayOrder)

		await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(questions) })
	})

	await page.route("**/api/admin/questions", async (route) => {
		if (route.request().method() === "POST") {
			const saved = JSON.parse(route.request().postData() ?? "{}") as Partial<StubQuestion>

			// The real API derives each choice's code from its English wording and
			// refuses two that reduce to the same code, naming both wordings.
			const wordings = (saved.options ?? []).map((option) => option.labelEn)
			const alike = wordings.filter(
				(wording, index) => wordings.findIndex((other) => codeOf(other) === codeOf(wording)) !== index,
			)
			if (alike.length > 0) {
				const first = wordings.find((wording) => codeOf(wording) === codeOf(alike[0]))
				await route.fulfill({
					status: 400,
					contentType: "application/problem+json",
					body: JSON.stringify({
						title: "Two choices read alike.",
						detail: `The choices '${first}' and '${alike[0]}' reduce to the same code.`,
					}),
				})
				return
			}

			// The real API derives the key from the English wording; the page never sends one.
			const created = {
				...question(`q${questions.length}`.padEnd(11, "0"), saved.key ?? codeOf(saved.labelEn ?? "question"), saved.labelEn ?? "", saved.type ?? "short_text", questions.length),
				labelFr: saved.labelFr ?? "",
			}

			questions.push(created)
			await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(created) })
			return
		}

		await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(questions) })
	})

	// Matches /api/admin/questions/<id> for PUT and DELETE. /order has its own
	// route above; Playwright prefers the most recently registered match, so
	// this one hands that path back rather than swallowing it.
	await page.route("**/api/admin/questions/*", async (route) => {
		if (route.request().url().endsWith("/order")) {
			await route.fallback()
			return
		}

		const id = route.request().url().split("/").pop()
		const index = questions.findIndex((candidate) => candidate.id === id)

		if (index < 0) {
			await route.fulfill({ status: 404, contentType: "application/json", body: "{}" })
			return
		}

		if (route.request().method() === "DELETE") {
			questions.splice(index, 1)
			await route.fulfill({ status: 204, body: "" })
			return
		}

		// An edit to the question itself is a new revision, never a patch — the
		// version number moves. Its choices are its own and are saved in place,
		// so an edit that changes only them leaves the version where it was
		// (ADR-0095).
		const current = questions[index]
		const saved = JSON.parse(route.request().postData() ?? "{}") as Partial<StubQuestion> & {
			options?: { code: string | null; labelEn: string; labelFr: string; pin?: string }[]
		}
		const options = (saved.options ?? []).map((option): StubOption => {
			const existing = current.options.find((candidate) => candidate.code === option.code)
			const labelEn = option.labelEn.trim() || null
			const labelFr = option.labelFr.trim() || null
			const code = option.code ?? codeOf(option.labelEn)
			return {
				id: existing?.id ?? `choice-${code}`,
				code,
				labelEn,
				labelFr,
				addedByReporter: existing?.addedByReporter ?? false,
				needsTranslation: labelEn === null || labelFr === null,
				reporterLocale: existing?.reporterLocale ?? null,
				pin: option.pin ?? "none",
			}
		})
		const fields: Partial<StubQuestion> = { ...saved, options: undefined }
		delete fields.options
		const revisionChanged = (Object.keys(fields) as (keyof StubQuestion)[]).some(
			(field) => fields[field] !== current[field],
		)
		const revised = {
			...current,
			...fields,
			options,
			reporterChoicesAwaitingReview: awaitingReview(options),
			revisionNumber: revisionChanged ? current.revisionNumber + 1 : current.revisionNumber,
			revisionId: revisionChanged ? `rev${current.revisionNumber + 1}${id}` : current.revisionId,
		}

		questions[index] = revised
		await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(revised) })
	})
}

/**
 * Stubs the translation endpoint. The prefix makes a translation obviously
 * machine-made, so a scenario asserts that the field was filled from the other
 * language rather than asserting the quality of any French.
 */
async function stubTranslation(page: Page, { available = true }: { available?: boolean } = {}) {
	await page.route("**/api/admin/translate", async (route) => {
		if (route.request().method() === "GET") {
			await route.fulfill({
				status: 200,
				contentType: "application/json",
				body: JSON.stringify({ available }),
			})
			return
		}

		const { texts, to } = JSON.parse(route.request().postData() ?? "{}") as { texts: string[]; to: string }

		await route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({ texts: texts.map((text) => (text ? `[${to}] ${text}` : "")) }),
		})
	})
}

async function signInAndOpenQuestions(page: Page, { translation = true }: { translation?: boolean } = {}) {
	// Genuinely signed in as an Administrator. Clicking "Log in" with empty
	// fields used to be enough only because the admin routes are unguarded by
	// design (ADR-0048) — the page would render without a session at all.
	await signInAs(page, "administrator")
	await stubAdminApi(page)
	await stubTranslation(page, { available: translation })
	await page.goto("/admin/questions")
}

// The feature's Background states two facts about the stored model. They are
// contextual for a browser scenario — the assertions that hold them up live in
// HpacSafety.Acceptance.Tests, which runs the same Background against the
// domain — but playwright-bdd still needs a definition for every step it sees.
Given("the question bank stores each question as a stable, non-localized key", async () => {})

Given("each revision has a monotonically increasing revision number for its key", async () => {})

Given("at most one live question exists for a stable key", async () => {})

Given("a signed-in Administrator opens the manage-questions page", async ({ page }) => {
	await signInAndOpenQuestions(page)
	await expect(page.getByRole("list", { name: "Questions on the form" })).toBeVisible()
})

Given("a signed-in Administrator is authoring a new question", async ({ page }) => {
	await signInAndOpenQuestions(page)
	await page.getByRole("button", { name: "Add a question" }).click()
})

When("they add a paragraph-text question in both official languages", async ({ page }) => {
	await page.getByRole("button", { name: "Add a question" }).click()
	await page.getByLabel("Type").selectOption("long_text")
	await page.getByLabel("Question (English)").fill("Describe the weather")
	await page.getByLabel("Question (French)").fill("Décrivez la météo")
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the new question appears in the list with its type and version", async ({ page }) => {
	const list = page.getByRole("list", { name: "Questions on the form" })
	const row = list.getByRole("listitem").filter({ hasText: "Describe the weather" })

	await expect(row).toBeVisible()
	await expect(row).toContainText("Paragraph text")
	await expect(row).toContainText("Version 1")
})

When("they choose the type-ahead list type", async ({ page }) => {
	await page.getByLabel("Type").selectOption("autocomplete")
})

When("they choose the single-line text type instead", async ({ page }) => {
	await page.getByLabel("Type").selectOption("short_text")
})

Then("the page offers an option editor", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Add a choice" })).toBeVisible()
	await expect(page.getByLabel("Shared choice list")).toHaveCount(0)
})

Then("the page offers neither", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Add a choice" })).toBeHidden()
})

// Cucumber expressions read `yes/no` as an alternation, so this one is a
// regular expression rather than an expression string.
Then(/^the condition picker offers only the yes\/no and single-select questions on the form$/, async ({ page }) => {
	const picker = page.getByLabel("Only ask when another question is answered a certain way")
	const offered = await picker.locator("option").allTextContents()

	expect(offered).toEqual(["Always ask this question", "Were you injured?", "Hang glider or paraglider?"])
})

// Cucumber expressions read `yes/no` as an alternation, so this one is a
// regular expression rather than an expression string.
When(/^they choose a yes\/no question as the condition$/, async ({ page }) => {
	await page.getByLabel("Only ask when another question is answered a certain way").selectOption("aaaaaaaaaaa")
})

Then("no required-option control is offered", async ({ page }) => {
	await expect(page.getByLabel("Required answer")).toBeHidden()
})

When("they choose a single-select question as the condition instead", async ({ page }) => {
	await page.getByLabel("Only ask when another question is answered a certain way").selectOption("ddddddddddd")
})

Then("a required-option control offers that question's live options", async ({ page }) => {
	const picker = page.getByLabel("Required answer")
	const offered = await picker.locator("option").allTextContents()

	await expect(picker).toBeVisible()
	expect(offered).toEqual(["Choose the required option", "Hang glider", "Paraglider", "Other"])
})

When("they move the second question up using its move-up control", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await rows.nth(1).getByRole("button", { name: "Move up" }).click()
})

Then("the two questions have swapped places in the list", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await expect(rows.nth(0)).toContainText("What happened?")
	await expect(rows.nth(1)).toContainText("Were you injured?")
})

Given("the first question has never been answered", async ({ page }) => {
	// The default stub, stated so the scenario reads on its own.
	await expect(page.getByRole("list", { name: "Questions on the form" })).toBeVisible()
})

When("they edit its English wording and save", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await rows.nth(0).getByRole("button", { name: "Edit" }).click()
	await page.getByLabel("Question (English)").fill("Were you hurt?")
	await page.getByRole("button", { name: "Save" }).click()
})

When("they edit the first question's English wording and save", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await rows.nth(0).getByRole("button", { name: "Edit" }).click()
	await page.getByLabel("Question (English)").fill("Were you hurt?")
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the list shows the new wording and a higher version number", async ({ page }) => {
	const row = page
		.getByRole("list", { name: "Questions on the form" })
		.getByRole("listitem")
		.filter({ hasText: "Were you hurt?" })

	await expect(row).toBeVisible()
	await expect(row).toContainText("Version 2")
})

let countBeforeDelete = 0

When("they delete the second question", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	countBeforeDelete = await rows.count()
	await rows.nth(1).getByRole("button", { name: "Delete" }).click()
})

Then("it is gone from the list", async ({ page }) => {
	const list = page.getByRole("list", { name: "Questions on the form" })

	await expect(list.getByRole("listitem")).toHaveCount(countBeforeDelete - 1)
	await expect(list).not.toContainText("What happened?")
})

When("they save a question whose two choices read alike", async ({ page }) => {
	await page.getByLabel("Type").selectOption("single_select")
	await page.getByLabel("Question (English)").fill("A duplicate")
	await page.getByLabel("Question (French)").fill("Un doublon")
	for (const wording of ["Site A-1", "Site A 1"]) {
		await page.getByRole("button", { name: "Add a choice" }).click()
		await page.getByLabel("Choice (English)").last().fill(wording)
		await page.getByLabel("Choice (French)").last().fill(wording)
	}
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the page shows the reason the save was refused", async ({ page }) => {
	await expect(page.getByRole("alert")).toContainText("Site A-1")
})

Then("the question is not added to the list", async ({ page }) => {
	const list = page.getByRole("list", { name: "Questions on the form" })

	await expect(list).not.toContainText("A duplicate")
})

When("they open the first question for editing", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await rows.nth(0).getByRole("button", { name: "Edit" }).click()
})

Then("the form is filled with its current wording, type, and behaviour", async ({ page }) => {
	await expect(page.getByLabel("Question (English)")).toHaveValue("Were you injured?")
	await expect(page.getByLabel("Question (French)")).toHaveValue("Were you injured? (fr)")
	await expect(page.getByLabel("Type")).toHaveValue("yes_no")
	await expect(page.getByLabel("Private")).toBeChecked()
	await expect(page.getByLabel("Reporters must answer")).not.toBeChecked()
})

Given(
	"a signed-in Administrator is authoring a question on a server with no translation provider",
	async ({ page }) => {
		await signInAndOpenQuestions(page, { translation: false })
		await page.getByRole("button", { name: "Add a question" }).click()
	},
)

// One button, labelled "Translate", whichever way it is about to go: the
// direction is decided by which language has been written, not by the caller.
const translateButton = (page: Page) => page.getByRole("button", { name: "Translate", exact: true })

When("they write the English wording and press Translate", async ({ page }) => {
	await page.getByLabel("Question (English)").fill("Were you injured?")
	await translateButton(page).click()
})

When("they write the French wording and press Translate", async ({ page }) => {
	await page.getByLabel("Question (French)").fill("Avez-vous été blessé ?")
	await translateButton(page).click()
})

Then("the French field is filled with the translation", async ({ page }) => {
	await expect(page.getByLabel("Question (French)")).toHaveValue("[fr-CA] Were you injured?")
})

Then("the English field is filled with the translation", async ({ page }) => {
	await expect(page.getByLabel("Question (English)")).toHaveValue("[en-CA] Avez-vous été blessé ?")
})

Then("the French field remains editable", async ({ page }) => {
	// A translation is a draft, not a locked value — the administrator corrects
	// it and what they save is theirs.
	const french = page.getByLabel("Question (French)")

	await expect(french).not.toHaveAttribute("readonly", "")
	await french.fill("Avez-vous été blessé ?")
	await expect(french).toHaveValue("Avez-vous été blessé ?")
})

When("only one official language has been written", async ({ page }) => {
	await page.getByLabel("Question (English)").fill("Were you injured?")
})

Then("saving is unavailable", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Save" })).toBeDisabled()
})

When("the other language is written as well", async ({ page }) => {
	await page.getByLabel("Question (French)").fill("Avez-vous été blessé ?")
})

Then("saving becomes available", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Save" })).toBeEnabled()
})

Then("the Translate action is unavailable and says so", async ({ page }) => {
	await expect(translateButton(page)).toBeDisabled()
	await expect(page.getByText("Translation is not available on this server.")).toBeVisible()
})

Then("no question key is shown", async ({ page }) => {
	// A key is the system's handle for a question, derived and never edited,
	// so the editor has nothing to show an administrator about it.
	await expect(page.getByLabel("Key", { exact: true })).toHaveCount(0)
	await expect(page.getByText("were_you_injured")).toHaveCount(0)
})

/*
 * Editing an answered question retires it and creates a new one in its place
 * (ADR-0071), so the editor says so before the administrator saves.
 */

Given("the first question has been answered", async ({ page }) => {
	const listed = [
		{ ...question("aaaaaaaaaaa", "injury", "Were you injured?", "yes_no", 1), hasBeenAnswered: true },
		question("bbbbbbbbbbb", "narrative", "What happened?", "long_text", 2),
	]

	await page.route("**/api/admin/questions", async (route) => {
		if (route.request().method() !== "GET") {
			await route.fallback()
			return
		}

		await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(listed) })
	})

	// Saving an answered question retires it and returns a new one carrying
	// the same key, so the list shows one question for that key either way.
	await page.route("**/api/admin/questions/*", async (route) => {
		const saved = JSON.parse(route.request().postData() ?? "{}") as { labelEn: string }
		const replacement = {
			...question("ccccccccccc", "injury", saved.labelEn, "yes_no", 1),
			revisionNumber: 1,
		}

		listed[0] = replacement
		await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(replacement) })
	})

	await page.reload()
	await expect(page.getByRole("list", { name: "Questions on the form" })).toBeVisible()
})

When("they edit its English wording", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await rows.nth(0).getByRole("button", { name: "Edit" }).click()
	await page.getByLabel("Question (English)").fill("Did you need medical attention?")
})

Then("the page says that saving retires this question and creates a new one", async ({ page }) => {
	// Scoped to the editor: dnd-kit keeps its own empty role="status" live
	// region on this page.
	await expect(
		page.getByRole("status").filter({ hasText: "This question has been answered" }),
	).toContainText("retires it and creates a new question")
})

When("they save", async ({ page }) => {
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the list shows one question for that key, with the new wording", async ({ page }) => {
	const rows = page
		.getByRole("list", { name: "Questions on the form" })
		.getByRole("listitem")
		.filter({ hasText: "Did you need medical attention?" })

	await expect(rows).toHaveCount(1)
})

When("they add a choice", async ({ page }) => {
	await page.getByRole("button", { name: "Add a choice" }).click()
})

// A choice is written by its wording in the two official languages and nothing else.
Then("the choice asks only for its English and French wording", async ({ page }) => {
	await expect(page.getByLabel("Choice (English)").last()).toBeVisible()
	await expect(page.getByLabel("Choice (French)").last()).toBeVisible()
	await expect(page.getByLabel("Code", { exact: true })).toHaveCount(0)
	await expect(page.getByPlaceholder("Code", { exact: true })).toHaveCount(0)
})

When("they save the question with that choice", async ({ page }) => {
	await page.getByLabel("Question (English)").fill("Where did you launch?")
	await page.getByLabel("Question (French)").fill("D'où avez-vous décollé?")
	await page.getByLabel("Choice (English)").last().fill("King Eddy")
	await page.getByLabel("Choice (French)").last().fill("King Eddy")

	const saving = page.waitForRequest(
		(request) => request.method() === "POST" && request.url().endsWith("/api/admin/questions"),
	)
	await page.getByRole("button", { name: "Save" }).click()
	savedBodies.set(page, JSON.parse((await saving).postData() ?? "{}") as { options: { code: string | null }[] })
})

Then("the choice is sent without a code", async ({ page }) => {
	const body = savedBodies.get(page)

	expect(body?.options).toEqual([{ code: null, labelEn: "King Eddy", labelFr: "King Eddy" }])
})


When("they open the second question for editing", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await rows.nth(1).getByRole("button", { name: "Edit" }).click()
})

Then("the editor takes the second question's place in the list", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await expect(rows.nth(1).getByRole("heading", { name: "Edit question" })).toBeVisible()
	await expect(rows.nth(1).getByLabel("Question (English)")).toHaveValue("What happened?")
	await expect(page.getByRole("heading", { name: "Edit question" })).toHaveCount(1)
})

Then("the editor's top edge lines up with that row's move-up control", async ({ page }) => {
	const row = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem").nth(1)
	const editor = await row.locator("form").boundingBox()
	const moveUp = await row.getByRole("button", { name: "Move up" }).boundingBox()

	expect(Math.abs((editor?.y ?? 0) - (moveUp?.y ?? Number.POSITIVE_INFINITY))).toBeLessThanOrEqual(1)
})

Then("every other question is still shown in its place", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await expect(rows).toHaveCount(4)
	await expect(rows.nth(0)).toContainText("Were you injured?")
	await expect(rows.nth(2)).toContainText("Hang glider or paraglider?")
})

When("they cancel the edit", async ({ page }) => {
	await page.getByRole("button", { name: "Cancel" }).click()
})

Then("the second question is shown in its place again", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await expect(page.getByRole("heading", { name: "Edit question" })).toHaveCount(0)
	await expect(rows.nth(1)).toContainText("What happened?")
	await expect(rows.nth(1).getByRole("button", { name: "Edit" })).toBeVisible()
})

// ------------------------------------------ reporter-added choices (ADR-0095) --

const launchSite = "Where did you launch?"

function launchSiteRow(page: Page) {
	return page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem").filter({ hasText: launchSite })
}

Then("a type-ahead question with reporter-added choices says how many are waiting to be reviewed", async ({ page }) => {
	await expect(launchSiteRow(page)).toContainText("Reporter-added choices waiting to be reviewed: 1")
})

When("they open that question", async ({ page }) => {
	await launchSiteRow(page).getByRole("button", { name: "Edit" }).click()
})

Then("each reporter-added choice is marked as such", async ({ page }) => {
	const choices = page.getByTestId("question-choice")

	await expect(choices).toHaveCount(2)
	await expect(choices.nth(0)).not.toContainText("Added by a reporter")
	await expect(choices.nth(1)).toContainText("Added by a reporter")
	await expect(choices.nth(1)).toContainText("Waiting for the French wording")
})

Given("a signed-in Administrator opens a type-ahead question with a reporter-added choice", async ({ page }) => {
	await signInAndOpenQuestions(page)
	await launchSiteRow(page).getByRole("button", { name: "Edit" }).click()
})

When("they correct the wording of that choice and save it", async ({ page }) => {
	const choice = page.getByTestId("question-choice").nth(1)

	await choice.getByLabel("Choice (English)").fill("Mount 7")
	await choice.getByLabel("Choice (French)").fill("Mont 7")
	await page.getByRole("button", { name: "Save" }).click()
	await expect(page.getByRole("heading", { name: "Edit question" })).toHaveCount(0)
})

Then("the corrected wording is shown on the question", async ({ page }) => {
	const row = launchSiteRow(page)

	// Correcting a choice is not a new version of the question.
	await expect(row).toContainText("Version 1")
	await expect(row).not.toContainText("waiting to be reviewed")

	await row.getByRole("button", { name: "Edit" }).click()
	const choice = page.getByTestId("question-choice").nth(1)
	await expect(choice.getByLabel("Choice (English)")).toHaveValue("Mount 7")
	await expect(choice.getByLabel("Choice (French)")).toHaveValue("Mont 7")
	await expect(choice).not.toContainText("Waiting for")
})

// REQ-QB-111: Auto-translate answer is offered only for free text (ADR-0112).

const needsTranslation = (page: Page) => page.getByRole("checkbox", { name: "Auto-translate answer" })

When("they choose long text", async ({ page }) => {
	await page.getByLabel("Type").selectOption("long_text")
})

When("they choose short text", async ({ page }) => {
	await page.getByLabel("Type").selectOption("short_text")
})

When("they choose email", async ({ page }) => {
	await page.getByLabel("Type").selectOption("email")
})

Then("Auto-translate answer is offered and checked", async ({ page }) => {
	await expect(needsTranslation(page)).toBeChecked()
})

Then("Auto-translate answer is offered and unchecked", async ({ page }) => {
	await expect(needsTranslation(page)).toBeVisible()
	await expect(needsTranslation(page)).not.toBeChecked()
})

Then("Auto-translate answer is not offered", async ({ page }) => {
	await expect(needsTranslation(page)).toHaveCount(0)
})

// ---------------------------------------- fix or replace a picker option (ADR-0128) --

const replacedBodies = new WeakMap<Page, { options: { code: string | null; labelEn: string; replace?: boolean }[] }>()

function questionRow(page: Page, label: string) {
	return page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem").filter({ hasText: label })
}

When(
	'they reword the "Paraglider" option of a single-select question and mark it to be replaced',
	async ({ page }) => {
		await questionRow(page, "Hang glider or paraglider?").getByRole("button", { name: "Edit" }).click()
		const choice = page.getByTestId("question-choice").nth(1)

		await choice.getByLabel("Choice (English)").fill("Paraglider (solo)")
		await choice.getByLabel("Choice (French)").fill("Parapente (solo)")
		await choice.getByLabel("Replace with a new option").check()

		const saving = page.waitForRequest(
			(request) => request.method() === "PUT" && request.url().endsWith("/api/admin/questions/ddddddddddd"),
		)
		await page.getByRole("button", { name: "Save" }).click()
		replacedBodies.set(page, JSON.parse((await saving).postData() ?? "{}"))
	},
)

Then("the save sends that option to be replaced, under its old code with its new wording", async ({ page }) => {
	const options = replacedBodies.get(page)?.options ?? []

	expect(options.find((option) => option.code === "paraglider")).toMatchObject({
		labelEn: "Paraglider (solo)",
		replace: true,
	})
	expect(options.find((option) => option.code === "hang_glider")?.replace).toBeFalsy()
})

Then("a type-ahead question's values offer no replace choice", async ({ page }) => {
	await launchSiteRow(page).getByRole("button", { name: "Edit" }).click()

	await expect(page.getByTestId("question-choice").first()).toBeVisible()
	await expect(page.getByLabel("Replace with a new option")).toHaveCount(0)
})

// --------------------------- instructional text is a title and a description (REQ-QB-141) --

const wordingFields = {
	statement: ["Title (English)", "Title (French)", "Description (English)", "Description (French)"],
	question: ["Question (English)", "Question (French)", "Help text (English)", "Help text (French)"],
}

async function expectWordingLabels(page: Page, shown: string[], hidden: string[]) {
	for (const label of shown) await expect(page.getByLabel(label, { exact: true })).toBeVisible()
	for (const label of hidden) await expect(page.getByLabel(label, { exact: true })).toHaveCount(0)
}

When("they choose instructional text", async ({ page }) => {
	await page.getByLabel("Type").selectOption("statement")
})

Then("its wording is asked for as a title and a description in each language", async ({ page }) => {
	await expectWordingLabels(page, wordingFields.statement, wordingFields.question)
})

Then("each description takes several lines", async ({ page }) => {
	for (const label of ["Description (English)", "Description (French)"]) {
		const description = page.getByLabel(label, { exact: true })
		await description.fill("First paragraph.\n\nSecond paragraph.")
		await expect(description).toHaveValue("First paragraph.\n\nSecond paragraph.")
	}
})

Then("its wording is asked for as a question and help text in each language", async ({ page }) => {
	await expectWordingLabels(page, wordingFields.question, wordingFields.statement)
})

// ------------------------ each option's position, listed as the form lists it (ADR-0136) --

const pinnedBodies = new WeakMap<Page, { options: { code: string | null; labelEn: string; pin?: string }[] }>()

function aircraftRow(page: Page) {
	return questionRow(page, "Hang glider or paraglider?")
}

/** The English wording of each option, in the order the editor lists them. */
async function editorOptions(page: Page): Promise<string[]> {
	await expect(page.getByTestId("question-choice").first()).toBeVisible()
	return page.getByTestId("question-choice").getByLabel("Choice (English)").evaluateAll((inputs) =>
		(inputs as HTMLInputElement[]).map((input) => input.value),
	)
}

function stubbedWording(page: Page, quoted: string[]) {
	// The stub's single-select question is the one the scenario describes.
	expect(quoted.sort()).toEqual(["Hang glider", "Other", "Paraglider"])
	return aircraftRow(page)
}

When(
	"they open a single-select question offering {string} pinned last, and {string} and {string} not pinned",
	async ({ page }, last: string, first: string, second: string) => {
		await stubbedWording(page, [last, first, second]).getByRole("button", { name: "Edit" }).click()
	},
)

Then("its options are listed {string}, {string}, {string}", async ({ page }, first: string, second: string, third: string) => {
	expect(await editorOptions(page)).toEqual([first, second, third])
})

Then("each option offers the positions {string}, {string}, and {string}", async ({ page }, none: string, top: string, bottom: string) => {
	const choices = page.getByTestId("question-choice")
	const count = await choices.count()
	expect(count).toBeGreaterThan(0)

	for (let index = 0; index < count; index++) {
		expect(await choices.nth(index).getByLabel("Position").locator("option").allTextContents()).toEqual([none, top, bottom])
	}
	await expect(choices.nth(count - 1).getByLabel("Position")).toHaveValue("last")
	await expect(choices.nth(0).getByLabel("Position")).toHaveValue("none")
})

When(
	"they reword {string} to {string} and set {string} to {string}",
	async ({ page }, before: string, after: string, pinned: string, position: string) => {
		const choices = page.getByTestId("question-choice")
		const options = await editorOptions(page)

		await choices.nth(options.indexOf(before)).getByLabel("Choice (English)").fill(after)
		await choices.nth(options.indexOf(pinned)).getByLabel("Position").selectOption({ label: position })
	},
)

Then("the options stay where they were while the Administrator edits", async ({ page }) => {
	expect(await editorOptions(page)).toEqual(["Speed wing", "Paraglider", "Other"])
})

Then(
	"the save sends {string} pinned first, {string} pinned last, and {string} not pinned",
	async ({ page }, first: string, last: string, none: string) => {
		const saving = page.waitForRequest(
			(request) => request.method() === "PUT" && request.url().endsWith("/api/admin/questions/ddddddddddd"),
		)
		await page.getByRole("button", { name: "Save" }).click()
		pinnedBodies.set(page, JSON.parse((await saving).postData() ?? "{}"))

		const pins = Object.fromEntries((pinnedBodies.get(page)?.options ?? []).map((option) => [option.labelEn, option.pin]))
		expect(pins).toEqual({ [first]: "first", [last]: "last", [none]: "none" })
	},
)

When(
	"they make a question conditional on a single-select question offering {string} pinned last, and {string} and {string} not pinned",
	async ({ page }, last: string, first: string, second: string) => {
		stubbedWording(page, [last, first, second])
		await page.getByRole("button", { name: "Add a question" }).click()
		await page.getByLabel("Only ask when another question is answered a certain way").selectOption("ddddddddddd")
	},
)

Then("the required-option control lists {string}, {string}, {string}", async ({ page }, first: string, second: string, third: string) => {
	const picker = page.getByLabel("Required answer")

	await expect(picker).toBeVisible()
	expect(await picker.locator("option").allTextContents()).toEqual(["Choose the required option", first, second, third])
})

