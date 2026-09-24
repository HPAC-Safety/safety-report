import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth, type Role } from "./auth"

const { Given, When, Then } = createBdd()

/*
 * The client-side admin route guard (ADR-0092). The API authorizes every
 * request on its own (ADR-0048/REQ-MOD-023/024); these scenarios only assert
 * what the browser draws before any such request is made.
 */

// Requests seen after the guard-relevant navigation, per page, so "no request
// for that route's data" can be asserted without guessing at timing.
const adminRequestsSeenAfterNavigation = new WeakMap<Page, string[]>()

Given("a visitor is signed out", async ({ page }) => {
	await stubAuth(page)
})

When("the visitor navigates directly to an admin route", async ({ page }) => {
	await page.goto("/admin/reports")
})

Then("the browser is redirected to the member-login page", async ({ page }) => {
	await expect(page).toHaveURL(/\/login$/)
})

Then("no admin page content is shown first", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Manage reports" })).toBeHidden()
})

const ROUTE_REQUEST_LISTENERS = new WeakMap<Page, boolean>()

function watchAdminRequests(page: Page) {
	if (ROUTE_REQUEST_LISTENERS.has(page)) return
	ROUTE_REQUEST_LISTENERS.set(page, true)
	adminRequestsSeenAfterNavigation.set(page, [])

	page.on("request", (request) => {
		const url = request.url()
		// The header's Admin menu reads its pending counts on every page; that
		// is the menu's data, not the guarded route's (REQ-MOD-043, REQ-MOD-087).
		if (url.includes("/api/admin/") && !url.includes("/api/admin/counts")) {
			adminRequestsSeenAfterNavigation.get(page)?.push(url)
		}
	})
}

When(/^the visitor navigates directly to (\/admin\/[a-z-]+), which their role cannot use$/, async ({ page }, route: string) => {
	watchAdminRequests(page)
	adminRequestsSeenAfterNavigation.set(page, [])
	await page.goto(route)
})

Then(/^the page shows a forbidden \(403\) view in place of the route's content$/, async ({ page }) => {
	await expect(page.getByRole("heading", { name: "You cannot open this page" })).toBeVisible()
})

Then("it is not the not-found page", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Page not found" })).toBeHidden()
})

Then("no request for that route's data is made, the Admin menu's pending counts aside", async ({ page }) => {
	expect(adminRequestsSeenAfterNavigation.get(page) ?? []).toEqual([])
})
