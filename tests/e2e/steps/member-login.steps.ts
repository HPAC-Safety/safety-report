import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

import { signInAs, stubAuth, type Role } from "./auth"

const { Given, When, Then } = createBdd()

Given("a visitor activates the member-login action", async ({ page }) => {
	await stubAuth(page)
	await page.goto("/")
	await page.locator("header").getByRole("link", { name: "Member login" }).click()
})

Then("the login page shows a username field, a password field, and a login action", async ({ page }) => {
	await expect(page.getByLabel("Username")).toBeVisible()
	await expect(page.getByLabel("Password")).toBeVisible()
	await expect(page.getByRole("button", { name: "Log in" })).toBeVisible()
})

Then("the login page shows no third-party sign-in option", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Continue with Google" })).toBeHidden()
})

Given("the API reports that a third-party provider is configured", async ({ page }) => {
	await stubAuth(page, { thirdPartySignIn: true })
})

Then("the login page also shows a third-party sign-in option", async ({ page }) => {
	await expect(page.getByRole("button", { name: "Continue with Google" })).toBeVisible()
})

Given("a visitor signs in from the member login page", async ({ page }) => {
	await signInAs(page, "administrator")
})

Given("a visitor signs in with valid member credentials", async ({ page }) => {
	await signInAs(page, "administrator")
})

Given(/^a visitor signs in as an? (Administrator|SafetyOfficer|User)$/, async ({ page }, role: string) => {
	const roles: Record<string, Role> = {
		Administrator: "administrator",
		SafetyOfficer: "safety_officer",
		User: "user",
	}
	await signInAs(page, roles[role])
})

Given("a visitor submits credentials that are not valid", async ({ page }) => {
	await stubAuth(page)
	await page.goto("/login")
	await page.getByLabel("Username").fill("nobody")
	await page.getByLabel("Password").fill("wrong")
	await page.getByRole("button", { name: "Log in" }).click()
})

Then("the login page shows one generic failure message", async ({ page }) => {
	const alerts = page.getByRole("alert")
	await expect(alerts).toHaveCount(1)
	await expect(alerts).toBeVisible()
})

Then("the failure does not say whether the username or the password was wrong", async ({ page }) => {
	// Whatever the message says, it must not name which half was at fault.
	const message = (await page.getByRole("alert").textContent()) ?? ""
	expect(message).not.toMatch(/unknown|no such|incorrect password|wrong password/i)
})

Then("the header still shows the member-login action", async ({ page }) => {
	await expect(page.locator("header").getByRole("link", { name: "Member login" })).toBeVisible()
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

Then("it opens with manage-reports, manage-questions, and manage-answer-translations options", async ({ page }) => {
	const menu = page.getByRole("menu", { name: "Admin" })
	await expect(menu.getByRole("menuitem", { name: "Manage reports" })).toBeVisible()
	await expect(menu.getByRole("menuitem", { name: "Manage questions" })).toBeVisible()
	await expect(menu.getByRole("menuitem", { name: "Answers awaiting translation" })).toBeVisible()
	// Shared choice lists are gone: each question owns its choices (ADR-0095).
	await expect(menu.getByRole("menuitem")).toHaveCount(3)
})

Then("it opens with a manage-reports option", async ({ page }) => {
	await expect(page.getByRole("menu", { name: "Admin" }).getByRole("menuitem", { name: "Manage reports" })).toBeVisible()
})

Then("it offers no manage-questions or manage-answer-translations option", async ({ page }) => {
	const menu = page.getByRole("menu", { name: "Admin" })
	await expect(menu.getByRole("menuitem", { name: "Manage questions" })).toBeHidden()
	await expect(menu.getByRole("menuitem", { name: "Answers awaiting translation" })).toBeHidden()
})

Then("the header shows a logout action", async ({ page }) => {
	await expect(page.locator("header").getByRole("button", { name: "Log out" })).toBeVisible()
})

Then("every option is on one line and none is truncated", async ({ page }) => {
	const options = await page
		.getByRole("menu", { name: "Admin" })
		.getByRole("menuitem")
		.evaluateAll((items) =>
			items.map((item) => {
				const range = document.createRange()
				range.selectNodeContents(item)
				return {
					text: item.textContent ?? "",
					lineBoxes: range.getClientRects().length,
					clientWidth: item.clientWidth,
					scrollWidth: item.scrollWidth,
				}
			}),
		)

	expect(options.length).toBeGreaterThan(0)
	for (const option of options) {
		expect(option.lineBoxes, `"${option.text}" wraps onto more than one line`).toBe(1)
		expect(option.scrollWidth, `"${option.text}" is truncated`).toBeLessThanOrEqual(option.clientWidth)
	}
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
