import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

const { Given, When, Then } = createBdd()

Given("a visitor has no stored theme preference", async ({ page }) => {
	// A context-level addInitScript would re-run on every navigation in this
	// context, including the reload later in this scenario, wiping out the
	// choice it is meant to verify persisted. Clear once, directly.
	await page.goto("/")
	await page.evaluate(() => localStorage.removeItem("hpac.theme"))
	await page.reload()
})

Then(/^the page follows the operating system's light\/dark preference$/, async ({ page }) => {
	await expect(page.locator("html")).not.toHaveAttribute("data-theme", /.+/)
})

When("the visitor toggles the theme control", async ({ page }) => {
	await page.getByRole("button", { name: "Theme" }).click()
})

Then("the data-theme attribute updates immediately", async ({ page }) => {
	await expect(page.locator("html")).toHaveAttribute("data-theme", /light|dark/)
})

Then("the header logo matches the active theme", async ({ page }) => {
	const theme = await page.locator("html").getAttribute("data-theme")
	await expect(page.getByRole("banner").getByRole("img")).toHaveAttribute("src", new RegExp(`hpac-${theme}`))
})

Then("the theme choice persists to local storage across a reload", async ({ page }) => {
	const applied = await page.locator("html").getAttribute("data-theme")
	const stored = await page.evaluate(() => localStorage.getItem("hpac.theme"))
	expect(stored).toBe(applied)

	await page.reload()
	await expect(page.locator("html")).toHaveAttribute("data-theme", applied!)
})
