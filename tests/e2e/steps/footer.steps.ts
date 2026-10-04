import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"
import { present } from "./present"

const { Given, Then } = createBdd()

Given("a visitor loads a page whose content is shorter than the window", async ({ page }) => {
	// The not-found page renders far less content than a typical viewport.
	await page.goto("/this-page-does-not-exist")
})

Then("the footer sits flush with the bottom of the window", async ({ page }) => {
	const footer = page.locator("footer")
	const box = await footer.boundingBox()
	const viewport = page.viewportSize()
	expect(box).not.toBeNull()
	expect(viewport).not.toBeNull()
	const { y, height } = present(box, "the footer's box")
	expect(Math.round(y + height)).toBe(present(viewport, "the viewport").height)
})

Given("a visitor loads a page whose content is taller than the window", async ({ page }) => {
	await page.goto("/")
	await page.evaluate(() => {
		document.body.style.minHeight = "3000px"
	})
})

Then("the footer sits below the content, not pinned to the window", async ({ page }) => {
	const footer = page.locator("footer")
	const box = await footer.boundingBox()
	const viewport = page.viewportSize()
	expect(box).not.toBeNull()
	expect(viewport).not.toBeNull()
	// A pinned/sticky footer would already be visible at the bottom of the
	// viewport here. This one only appears once the content above it ends.
	expect(present(box, "the footer's box").y).toBeGreaterThan(present(viewport, "the viewport").height)
})
