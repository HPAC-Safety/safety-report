import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

const { Given, When, Then } = createBdd()

const NAV_DESTINATIONS = [
	{ name: "View safety reports", path: "/reports" },
	{ name: "Submit a safety report", path: "/report" },
	{ name: "Contact", path: "/contact" },
]
const MEMBER_LOGIN = { name: "Member login", path: "/login" }

Given("a visitor loads the homepage", async ({ page }) => {
	await page.goto("/")
})

Then("the header shows links to view safety reports, submit a safety report, and contact", async ({ page }) => {
	const header = page.locator("header")
	for (const { name } of NAV_DESTINATIONS) {
		await expect(header.getByRole("link", { name })).toBeVisible()
	}
})

Then("the header shows a visually distinct member-login action", async ({ page }) => {
	const login = page.locator("header").getByRole("link", { name: MEMBER_LOGIN.name })
	await expect(login).toBeVisible()
	// "Distinct" is the filled brand button treatment, not the plain nav-link
	// underline style — checked by background color rather than a class name,
	// since class names aren't a contract.
	await expect(login).not.toHaveCSS("background-color", "rgba(0, 0, 0, 0)")
})

When("a visitor activates any of those links or the member-login action", async ({ page }) => {
	for (const destination of [...NAV_DESTINATIONS, MEMBER_LOGIN]) {
		await page.goto("/")
		await page.locator("header").getByRole("link", { name: destination.name }).click()
		await expect(page).toHaveURL(new RegExp(`${destination.path}$`))
	}
})

Then("the browser navigates to that destination's page", async ({ page }) => {
	await expect(page.locator("main h1")).toBeVisible()
})

Given("a visitor loads the homepage on a mobile-width viewport", async ({ page }) => {
	await page.setViewportSize({ width: 375, height: 812 })
	await page.goto("/")
})

Then("the header nav is hidden and a menu toggle is shown instead", async ({ page }) => {
	await expect(page.getByRole("dialog", { name: "Primary" })).toBeHidden()
	await expect(page.getByRole("button", { name: "Open menu" })).toBeVisible()
})

When("the visitor activates the menu toggle", async ({ page }) => {
	await page.getByRole("button", { name: /^(Open|Close) menu$/ }).click()
})

Then("a dialog containing the header's navigation links and member-login action opens", async ({ page }) => {
	const nav = page.getByRole("dialog", { name: "Primary" })
	await expect(nav).toBeVisible()
	for (const { name } of NAV_DESTINATIONS) {
		await expect(nav.getByRole("link", { name })).toBeVisible()
	}
	await expect(nav.getByRole("link", { name: MEMBER_LOGIN.name })).toBeVisible()
})

When("the visitor activates the menu toggle again", async ({ page }) => {
	await page.getByRole("button", { name: /^(Open|Close) menu$/ }).click()
})

Then("the dialog closes", async ({ page }) => {
	await expect(page.getByRole("dialog", { name: "Primary" })).toBeHidden()
})
