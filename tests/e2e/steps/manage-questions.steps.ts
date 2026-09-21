import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

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

interface StubQuestion {
	id: string
	key: string
	revisionId: string
	revisionNumber: number
	type: string
	isSystem: boolean
	isRequired: boolean
	isPrivate: boolean
	isActive: boolean
	displayOrder: number
	sectionKey: string | null
	dependsOnQuestionId: string | null
	optionSetId: string | null
	labelEn: string
	labelFr: string
	helpTextEn: string | null
	helpTextFr: string | null
	placeholderEn: string | null
	placeholderFr: string | null
	options: { code: string; labelEn: string; labelFr: string; sourceItemId: string | null }[]
}

function question(id: string, key: string, labelEn: string, type: string, displayOrder: number): StubQuestion {
	return {
		id,
		key,
		revisionId: `rev${id}`,
		revisionNumber: 1,
		type,
		isSystem: false,
		isRequired: false,
		isPrivate: true,
		isActive: true,
		displayOrder,
		sectionKey: null,
		dependsOnQuestionId: null,
		optionSetId: null,
		labelEn,
		labelFr: `${labelEn} (fr)`,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
	}
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
	]

	await page.route("**/api/admin/option-sets", async (route) => {
		await route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify([
				{ id: "ccccccccccc", key: "aerodromes", nameEn: "Aerodromes", nameFr: "Aérodromes", items: [] },
			]),
		})
	})

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

			if (questions.some((candidate) => candidate.key === saved.key)) {
				await route.fulfill({
					status: 400,
					contentType: "application/problem+json",
					body: JSON.stringify({
						title: "That key is taken.",
						detail: `Another question already uses the key '${saved.key}'.`,
					}),
				})
				return
			}

			const created = {
				...question(`q${questions.length}`.padEnd(11, "0"), saved.key ?? "new_question", saved.labelEn ?? "", saved.type ?? "short_text", questions.length),
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

		// An edit is a new revision, never a patch — the version number moves.
		const saved = JSON.parse(route.request().postData() ?? "{}") as Partial<StubQuestion>
		const revised = {
			...questions[index],
			...saved,
			revisionNumber: questions[index].revisionNumber + 1,
			revisionId: `rev${questions[index].revisionNumber + 1}${id}`,
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
	await page.goto("/login")
	await page.getByRole("button", { name: "Log in" }).click()
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
	await page.getByLabel("Key").fill("weather_notes")
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

Then("the page offers a shared choice list and an option editor", async ({ page }) => {
	await expect(page.getByLabel("Shared choice list")).toBeVisible()
	await expect(page.getByRole("button", { name: "Add a choice" })).toBeVisible()
})

Then("the page offers neither", async ({ page }) => {
	await expect(page.getByLabel("Shared choice list")).toBeHidden()
	await expect(page.getByRole("button", { name: "Add a choice" })).toBeHidden()
})

// Cucumber expressions read `yes/no` as an alternation, so this one is a
// regular expression rather than an expression string.
Then(/^the condition picker offers only the yes\/no questions on the form$/, async ({ page }) => {
	const picker = page.getByLabel("Only ask when another question is answered yes")
	const offered = await picker.locator("option").allTextContents()

	expect(offered).toEqual(["Always ask this question", "Were you injured?"])
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

When("they delete the second question", async ({ page }) => {
	const rows = page.getByRole("list", { name: "Questions on the form" }).getByRole("listitem")

	await rows.nth(1).getByRole("button", { name: "Delete" }).click()
})

Then("it is gone from the list", async ({ page }) => {
	const list = page.getByRole("list", { name: "Questions on the form" })

	await expect(list.getByRole("listitem")).toHaveCount(1)
	await expect(list).not.toContainText("What happened?")
})

When("they save a question whose key is already in use", async ({ page }) => {
	await page.getByLabel("Key").fill("were_you_injured")
	await page.getByLabel("Question (English)").fill("A duplicate")
	await page.getByLabel("Question (French)").fill("Un doublon")
	await page.getByRole("button", { name: "Save" }).click()
})

Then("the page shows the reason the save was refused", async ({ page }) => {
	await expect(page.getByRole("alert")).toContainText("were_you_injured")
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

When("they write the English wording and ask for it to be translated", async ({ page }) => {
	await page.getByLabel("Question (English)").fill("Were you injured?")
	await page.getByRole("button", { name: "Translate into French" }).click()
})

When("they write the French wording and ask for it to be translated", async ({ page }) => {
	await page.getByLabel("Question (French)").fill("Avez-vous été blessé ?")
	await page.getByRole("button", { name: "Translate into English" }).click()
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
	await page.getByLabel("Key").fill("were_you_hurt")
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

Then("the translate action is unavailable and says so", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Translate into French" })).toBeDisabled()
	await expect(page.getByText("Translation is not available on this server.")).toBeVisible()
})

Then("its key cannot be changed", async ({ page }) => {
	// A key is what every stored answer refers to, so an edit may never change it.
	await expect(page.getByLabel("Key")).toHaveAttribute("readonly", "")
	await expect(page.getByLabel("Key")).toHaveValue("were_you_injured")
})
