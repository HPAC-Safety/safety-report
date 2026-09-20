import { test, expect } from "@playwright/test"

// Broad regression smoke test for the homepage shell. Scenario-level
// behavior (nav destinations, language/theme persistence) lives in
// features/web-localization-and-design/web-localization-and-design.feature
// and executes via the generated specs in ./steps (ADR-0053), not here.
test("the homepage loads with a header, nav, and hero", async ({ page }) => {
	await page.goto("/")

	await expect(page).toHaveTitle("HPAC Safety")
	await expect(page.locator("header").getByRole("link", { name: "View safety reports" })).toBeVisible()
	await expect(page.locator("header").getByRole("link", { name: "Submit a safety report" })).toBeVisible()
	await expect(page.locator("header").getByRole("link", { name: "Contact" })).toBeVisible()
	await expect(page.locator("header").getByRole("link", { name: "Member login" })).toBeVisible()
	await expect(page.locator("main h1")).toBeVisible()
})
