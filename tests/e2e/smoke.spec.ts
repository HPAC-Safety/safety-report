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

test("on a mobile-width viewport, navigation is reached through a hamburger dropdown", async ({ page }) => {
	await page.setViewportSize({ width: 375, height: 812 })
	await page.goto("/")

	const nav = page.getByRole("dialog", { name: "Primary" })
	await expect(nav).toBeHidden()

	const toggle = page.getByRole("button", { name: "Open menu" })
	await toggle.click()
	await expect(nav).toBeVisible()
	await expect(nav.getByRole("link", { name: "View safety reports" })).toBeVisible()
	await expect(nav.getByRole("link", { name: "Member login" })).toBeVisible()

	await page.getByRole("button", { name: "Close menu" }).click()
	await expect(nav).toBeHidden()
})
