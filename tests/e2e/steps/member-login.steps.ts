import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

const { Given, When, Then } = createBdd()

Given("a visitor activates the member-login action", async ({ page }) => {
	await page.goto("/")
	await page.locator("header").getByRole("link", { name: "Member login" }).click()
})

Then("the login page shows a username field, a password field, a third-party sign-in option, and a login action", async ({ page }) => {
	await expect(page.getByLabel("Username")).toBeVisible()
	await expect(page.getByLabel("Password")).toBeVisible()
	await expect(page.getByRole("button", { name: "Continue with Google" })).toBeVisible()
	await expect(page.getByRole("button", { name: "Log in" })).toBeVisible()
})

Given("a visitor signs in from the member login page", async ({ page }) => {
	await page.goto("/login")
	await page.getByRole("button", { name: "Log in" }).click()
})

Then("the header shows a logout action instead of the member-login action", async ({ page }) => {
	await expect(page.locator("header").getByRole("button", { name: "Log out" })).toBeVisible()
	await expect(page.locator("header").getByRole("link", { name: "Member login" })).toBeHidden()
})

When("the page reloads", async ({ page }) => {
	await page.reload()
})

Then("the header still shows the logout action", async ({ page }) => {
	await expect(page.locator("header").getByRole("button", { name: "Log out" })).toBeVisible()
})

When("the visitor activates the logout action", async ({ page }) => {
	await page.locator("header").getByRole("button", { name: "Log out" }).click()
})

Then("the header shows the member-login action again", async ({ page }) => {
	await expect(page.locator("header").getByRole("link", { name: "Member login" })).toBeVisible()
})

Then("the header shows an Admin menu and no other header nav change", async ({ page }) => {
	const header = page.locator("header")
	await expect(header.getByRole("button", { name: "Admin" })).toBeVisible()
	await expect(header.getByRole("link", { name: "View safety reports" })).toBeVisible()
	await expect(header.getByRole("link", { name: "Submit a safety report" })).toBeVisible()
	await expect(header.getByRole("link", { name: "Contact" })).toBeVisible()
})

When("the visitor activates the Admin menu", async ({ page }) => {
	await page.locator("header").getByRole("button", { name: "Admin" }).click()
})

Then("it opens with manage-reports and manage-questions options", async ({ page }) => {
	const menu = page.getByRole("menu", { name: "Admin" })
	await expect(menu.getByRole("menuitem", { name: "Manage reports" })).toBeVisible()
	await expect(menu.getByRole("menuitem", { name: "Manage questions" })).toBeVisible()
})

const ADMIN_MENU_DESTINATIONS: Record<string, string> = {
	"manage-reports": "/admin/reports",
	"manage-questions": "/admin/questions",
}

When(/^the visitor activates the (.+) option$/, async ({ page }, option: string) => {
	await page.getByRole("menuitem", { name: option }).click()
})

Then(/^the browser navigates to the (.+) placeholder page$/, async ({ page }, destination: string) => {
	const path = ADMIN_MENU_DESTINATIONS[destination]
	await expect(page).toHaveURL(new RegExp(`${path}$`))
	await expect(page.locator("main h1")).toBeVisible()
})

Then("the header shows no Admin menu", async ({ page }) => {
	await expect(page.locator("header").getByRole("button", { name: "Admin" })).toBeHidden()
})
