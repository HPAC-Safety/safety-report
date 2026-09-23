import { createBdd } from "playwright-bdd"
import { expect, type Download, type Page } from "@playwright/test"

const { Given, When, Then } = createBdd()

// The feature file's Background — bound for real against the mapper in
// HpacSafety.Core.Tests (TypeformImportSteps.cs); this @ui scenario builds
// its own stubbed pair below, so there's nothing to do here.
Given("an Administrator has an English Typeform export and a matching French one", async () => {})

/*
 * The @ui scenario for the Typeform import dialog (ADR-0077, ADR-0078).
 *
 * Import itself is a mapping exercise covered end-to-end by
 * HpacSafety.Api.Tests against the real fixture files. What this scenario
 * asserts is browser-observable: the file pair reaches the import endpoint,
 * the returned drafts are listed, and choosing one hands its data to the
 * ordinary QuestionEditor exactly the way any other draft would be.
 */

const IMPORTED_DRAFT = {
	key: "occurrence_narrative",
	type: "short_text",
	labelEn: "Tell us what happened",
	labelFr: "Dites-nous ce qui s'est passé",
	frenchDefaultedToEnglish: false,
	helpTextEn: null,
	helpTextFr: null,
	groupedUnderKey: null,
	allowsReporterAdditions: false,
	options: [],
}

async function stubTypeformImport(page: Page, draft: typeof IMPORTED_DRAFT = IMPORTED_DRAFT) {
	await page.route("**/api/admin/typeform/pending-logic", async (route) => {
		await route.fulfill({ status: 200, contentType: "application/json", body: "[]" })
	})

	await page.route("**/api/admin/typeform/import", async (route) => {
		await route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({ drafts: [draft], rejected: [], pendingLogicNoteIds: [] }),
		})
	})
}

When("they import a Typeform English and French export pair", async ({ page }) => {
	await stubTypeformImport(page)

	await page.getByRole("button", { name: "Import from Typeform" }).click()

	const english = JSON.stringify({
		fields: [{ id: "f1", ref: "occurrence_narrative", title: "Tell us what happened", type: "short_text", properties: {} }],
		logic: [],
	})
	const french = JSON.stringify({
		fields: [
			{ id: "f1", ref: "occurrence_narrative", title: "Dites-nous ce qui s'est passé", type: "short_text", properties: {} },
		],
		logic: [],
	})

	await page.getByLabel("English export (.json)").setInputFiles({
		name: "form-en.json",
		mimeType: "application/json",
		buffer: Buffer.from(english),
	})
	await page.getByLabel("French export (.json)").setInputFiles({
		name: "form-fr.json",
		mimeType: "application/json",
		buffer: Buffer.from(french),
	})

	await page.getByRole("button", { name: "Import", exact: true }).click()
})

Then("the imported drafts are listed", async ({ page }) => {
	await expect(page.getByText(IMPORTED_DRAFT.labelEn, { exact: true })).toBeVisible()
})

When("they choose to review the first imported draft", async ({ page }) => {
	await page.getByRole("button", { name: "Review", exact: true }).first().click()
})

Then("the editor is filled with that draft's type and both languages", async ({ page }) => {
	await expect(page.getByLabel("Type")).toHaveValue(IMPORTED_DRAFT.type)
	await expect(page.getByLabel("Question (English)")).toHaveValue(IMPORTED_DRAFT.labelEn)
	await expect(page.getByLabel("Question (French)")).toHaveValue(IMPORTED_DRAFT.labelFr)
})

let exportedDownload: Download | null = null

When("they choose to export the question bank", async ({ page }) => {
	await page.route("**/api/admin/typeform/export", async (route) => {
		await route.fulfill({
			status: 200,
			contentType: "application/zip",
			headers: { "content-disposition": 'attachment; filename="question-bank.zip"' },
			body: Buffer.from("PK\x05\x06" + "\x00".repeat(18)),
		})
	})

	const [download] = await Promise.all([
		page.waitForEvent("download"),
		page.getByRole("button", { name: "Export to Typeform JSON" }).click(),
	])

	exportedDownload = download
})

Then("a zip file download begins", async () => {
	expect(exportedDownload?.suggestedFilename()).toBe("question-bank.zip")
})

When("they import a Typeform draft whose key matches an existing question", async ({ page }) => {
	await stubTypeformImport(page, {
		...IMPORTED_DRAFT,
		key: "occurrence_notes",
		labelEn: "What happened? (reimported)",
	})

	await page.getByRole("button", { name: "Import from Typeform" }).click()

	const fileContents = JSON.stringify({
		fields: [{ id: "f1", ref: "occurrence_notes", title: "What happened? (reimported)", type: "short_text", properties: {} }],
		logic: [],
	})

	await page.getByLabel("English export (.json)").setInputFiles({
		name: "form-en.json",
		mimeType: "application/json",
		buffer: Buffer.from(fileContents),
	})
	await page.getByLabel("French export (.json)").setInputFiles({
		name: "form-fr.json",
		mimeType: "application/json",
		buffer: Buffer.from(fileContents),
	})

	await page.getByRole("button", { name: "Import", exact: true }).click()
})

Then(
	"choosing to review it opens the existing question for editing instead of creating a new one",
	async ({ page }) => {
		await page.getByRole("button", { name: "Review", exact: true }).first().click()

		await expect(page.getByRole("heading", { name: "Edit question" })).toBeVisible()
	},
)
