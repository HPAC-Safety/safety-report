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
