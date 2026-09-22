import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

const { When, Then } = createBdd()

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

async function stubTypeformImport(page: Page) {
	await page.route("**/api/admin/typeform/pending-logic", async (route) => {
		await route.fulfill({ status: 200, contentType: "application/json", body: "[]" })
	})

	await page.route("**/api/admin/typeform/import", async (route) => {
		await route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({ drafts: [IMPORTED_DRAFT], rejected: [], pendingLogicNoteIds: [] }),
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

Then("the editor is filled with that draft's key, type, and both languages", async ({ page }) => {
	await expect(page.getByLabel("Key")).toHaveValue(IMPORTED_DRAFT.key)
	await expect(page.getByLabel("Type")).toHaveValue(IMPORTED_DRAFT.type)
	await expect(page.getByLabel("Question (English)")).toHaveValue(IMPORTED_DRAFT.labelEn)
	await expect(page.getByLabel("Question (French)")).toHaveValue(IMPORTED_DRAFT.labelFr)
})
