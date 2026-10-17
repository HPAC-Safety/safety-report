import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"

const { Given, When, Then } = createBdd()

/*
 * The manage-roles page (REQ-AUTH-019, REQ-AUTH-029). The API authorizes every
 * role-assignment request on its own (REQ-AUTH-020, covered by the acceptance
 * suite); these scenarios only assert what the browser draws. The role-assignment
 * endpoint is stubbed here, with the shape the API answers:
 * { superAdministrator: { email } | null, assignments: [...] | null, assignmentsUnavailable }.
 */

const SUPER_ADMINISTRATOR_EMAIL = "super.admin@example.com"
const LISTED_EMAIL = "listed.officer@example.com"

const superAdministratorConfigured = new WeakSet<Page>()
const assignmentsUnreadable = new WeakSet<Page>()
// Every request to the role-assignment endpoints, per page.
const roleAssignmentRequests = new WeakMap<Page, string[]>()

async function stubRoleAssignments(page: Page) {
	const seen: string[] = []
	roleAssignmentRequests.set(page, seen)

	await page.route("**/api/admin/role-assignments**", (route) => {
		seen.push(`${route.request().method()} ${new URL(route.request().url()).pathname}`)

		const unreadable = assignmentsUnreadable.has(page)

		return route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({
				superAdministrator: superAdministratorConfigured.has(page) ? { email: SUPER_ADMINISTRATOR_EMAIL } : null,
				assignments: unreadable
					? null
					: [{ id: "00000000-0000-0000-0000-000000000001", email: LISTED_EMAIL, role: "safety_officer", grantedAt: "2026-01-01T00:00:00Z" }],
				assignmentsUnavailable: unreadable,
			}),
		})
	})
}

// --- REQ-AUTH-019 ---

When("the visitor navigates directly to the manage-roles page", async ({ page }) => {
	await stubRoleAssignments(page)
	await page.goto("/admin/roles")
})

Then("no role assignment is shown", async ({ page }) => {
	// Wait for the forbidden view first, so an absence is judged after the page has settled.
	await expect(page.getByRole("heading", { name: "You cannot open this page" })).toBeVisible()
	await expect(page.getByText(LISTED_EMAIL)).toBeHidden()
	await expect(page.getByText(SUPER_ADMINISTRATOR_EMAIL)).toBeHidden()
	expect(roleAssignmentRequests.get(page) ?? []).toEqual([])
})

// --- REQ-AUTH-029 ---

Given("a Super Administrator is configured", ({ page }) => {
	superAdministratorConfigured.add(page)
})

Given("the role assignments cannot be read", ({ page }) => {
	assignmentsUnreadable.add(page)
})

When("the Super Administrator opens the manage-roles page", async ({ page }) => {
	// The Super Administrator holds the Administrator role (REQ-AUTH-024).
	await stubAuth(page)
	await stubRoleAssignments(page)
	await signInAs(page, "administrator")
	await page.goto("/admin/roles")
})

Then("the page lists the Super Administrator as a permanent Administrator", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Manage roles" })).toBeVisible()
	await expect(page.getByText(SUPER_ADMINISTRATOR_EMAIL)).toBeVisible()
	await expect(page.getByText("Super Administrator", { exact: true })).toBeVisible()
	await expect(page.getByText(/permanent/i)).toBeVisible()
})

Then("the page says plainly that the other role assignments cannot be shown right now", async ({ page }) => {
	await expect(page.getByText(/other role assignments cannot be shown right now/i)).toBeVisible()
	await expect(page.getByText(LISTED_EMAIL)).toBeHidden()
})

Then("the page offers no way to add or remove a role assignment", async ({ page }) => {
	await expect(page.getByRole("button", { name: /assign role|remove role/i })).toHaveCount(0)
	await expect(page.getByRole("textbox")).toHaveCount(0)
	await expect(page.getByRole("combobox")).toHaveCount(0)
})
