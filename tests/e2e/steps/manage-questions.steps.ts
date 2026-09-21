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
}

async function signInAndOpenQuestions(page: Page) {
	await page.goto("/login")
	await page.getByRole("button", { name: "Log in" }).click()
	await stubAdminApi(page)
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
