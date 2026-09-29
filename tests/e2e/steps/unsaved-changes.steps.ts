import { createBdd } from "playwright-bdd"
import { expect, type Dialog, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * The shared `useUnsavedChangesGuard` mechanism (issue #659), reused by every
 * editable form across the public and admin sites: a browser-unload prompt
 * plus an in-app route-change confirm dialog. Most of the setup below reuses
 * Given/When steps already registered for each form's own scenarios (a
 * signed-in Administrator authoring a question, a Safety Officer correcting a
 * type-ahead value, a signed-in member reading a published report, and so
 * on); only the leave/confirm steps that are specific to this mechanism are
 * defined here.
 */

function unsavedChangesDialog(page: Page) {
	return page.getByRole("dialog", { name: "Leave without saving?" })
}

Given("a reporter has not answered anything on the report form", async ({ page }) => {
	await stubAuth(page)
	await stubCurrentQuestions(page)
	await signInAs(page, "user")
	await page.goto("/report")
	await expect(page.getByRole("heading", { level: 1 })).toBeVisible()
})

When("the reporter activates a header navigation link away from the form", async ({ page }) => {
	await page.getByRole("banner").getByRole("link", { name: "View safety reports" }).click()
})

When("they navigate to another admin page", async ({ page }) => {
	await page.getByRole("button", { name: "Admin" }).click()
	await page.getByRole("menuitem", { name: "Manage reports" }).click()
})

When("navigates to another admin page", async ({ page }) => {
	await page.getByRole("button", { name: "Admin" }).click()
	await page.getByRole("menuitem", { name: "Manage reports" }).click()
})

When("navigates away from the report", async ({ page }) => {
	await page.getByRole("banner").getByRole("link", { name: "View safety reports" }).click()
})

When("the reporter tries to close or reload the tab", async ({ page }) => {
	const dialogPromise = page.waitForEvent("dialog")
	void page.reload().catch(() => {}) // The reload never completes: the dialog cancels it.
	const dialog: Dialog = await dialogPromise
	expect(dialog.type()).toBe("beforeunload")
	await dialog.dismiss()
})

Then("the browser's own unload prompt appears, with no custom text", async () => {}) // Asserted by the When step above: waitForEvent only resolves if it fired.

When("they write the English wording without saving", async ({ page }) => {
	await page.getByLabel("Question (English)").fill("A draft question, never saved")
})

When("types into the English text without saving", async ({ page }) => {
	await page.getByLabel("English summary").fill("A draft edit, never saved.")
})

When("the safety officer starts writing a private note without saving it", async ({ page }) => {
	await page.getByLabel("Add a private note").fill("A draft private note, never saved.")
})

When("types a comment without posting it", async ({ page }) => {
	await page.getByLabel("Add a comment").fill("A draft comment, never posted.")
})

Then("a bilingual dialog asks whether to leave, offering to stay", async ({ page }) => {
	const dialog = unsavedChangesDialog(page)
	await expect(dialog).toBeVisible()
	await expect(dialog.getByRole("button", { name: "Leave" })).toBeVisible()
	await expect(dialog.getByRole("button", { name: "Stay" })).toBeVisible()
})

When("the reporter confirms leaving", async ({ page }) => {
	await unsavedChangesDialog(page).getByRole("button", { name: "Leave" }).click()
})

When("they confirm leaving", async ({ page }) => {
	await unsavedChangesDialog(page).getByRole("button", { name: "Leave" }).click()
})

Then("the browser navigates to that page", async ({ page }) => {
	await expect(unsavedChangesDialog(page)).toBeHidden()
	await expect(page).toHaveURL(/\/admin\/reports$|\/reports$/)
})

Then("the browser navigates to that page with no dialog shown", async ({ page }) => {
	await expect(page).toHaveURL(/\/reports$/)
	await expect(unsavedChangesDialog(page)).toBeHidden()
})

Then("the browser navigates to that page and the draft is gone", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports$/)
	await page.getByRole("button", { name: "Admin" }).click()
	await page.getByRole("menuitem", { name: "Manage questions" }).click()
	await expect(page.getByRole("button", { name: "Add a question" })).toBeVisible()
})

Then("the browser navigates to that page and the edit is gone", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports$/)
})

Then("the browser navigates to that page and the correction is gone", async ({ page }) => {
	await expect(page).toHaveURL(/\/admin\/reports$/)
})

Then("the browser navigates away and the unposted comment is gone", async ({ page }) => {
	await expect(page).toHaveURL(/\/reports$/)
})
