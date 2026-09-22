import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

const { Given, Then } = createBdd()

Given("a visitor loads the contact page", async ({ page }) => {
	await page.goto("/contact")
})

Then("the page shows the organization name and mailing address", async ({ page }) => {
	const main = page.locator("main")
	await expect(main.getByText("Hang Gliding and Paragliding Association of Canada (HPAC)")).toBeVisible()
	await expect(main.getByText("612 – 5307 Victoria Drive", { exact: false })).toBeVisible()
})

Then("the page shows an email link addressed to the current locale's contact address", async ({ page }) => {
	const link = page.locator("main").getByRole("link", { name: "safety@hpac.ca" })
	await expect(link).toBeVisible()
	await expect(link).toHaveAttribute("href", "mailto:safety@hpac.ca")
})

Then("the page shows Facebook, YouTube, and WhatsApp links that open in a new tab", async ({ page }) => {
	const main = page.locator("main")
	for (const name of ["Facebook", "YouTube", "WhatsApp"]) {
		const link = main.getByRole("link", { name })
		await expect(link).toBeVisible()
		await expect(link).toHaveAttribute("target", "_blank")
	}
})
