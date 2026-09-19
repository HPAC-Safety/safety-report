import { test, expect } from "@playwright/test"

// Proves the pipeline, not the product: src/web is still a placeholder
// (see App.tsx). Real journeys are added here per ADR-0045 as features land.
test("the built web app loads in a browser", async ({ page }) => {
	await page.goto("/")

	await expect(page).toHaveTitle("HPAC Safety")
	await expect(page.getByRole("heading", { name: "Hello World" })).toBeVisible()
})
