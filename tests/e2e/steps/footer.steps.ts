import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

const { Given, Then } = createBdd()

Given("a visitor loads a page whose content is shorter than the viewport", async ({ page }) => {
	// The Contact placeholder page renders far less content than a typical viewport.
	await page.goto("/contact")
})

Then("the footer sits flush with the bottom of the viewport", async ({ page }) => {
	const footer = page.locator("footer")
	const box = await footer.boundingBox()
	const viewport = page.viewportSize()
	expect(box).not.toBeNull()
	expect(viewport).not.toBeNull()
	expect(Math.round(box!.y + box!.height)).toBe(viewport!.height)
})

Given("a visitor loads a page whose content is taller than the viewport", async ({ page }) => {
	await page.goto("/")
	await page.evaluate(() => {
		document.body.style.minHeight = "3000px"
	})
})

Then("the footer sits below the content, not pinned to the viewport", async ({ page }) => {
	const footer = page.locator("footer")
	const box = await footer.boundingBox()
	const viewport = page.viewportSize()
	expect(box).not.toBeNull()
	expect(viewport).not.toBeNull()
	// A pinned/sticky footer would already be visible at the bottom of the
	// viewport here. This one only appears once the content above it ends.
	expect(box!.y).toBeGreaterThan(viewport!.height)
})
