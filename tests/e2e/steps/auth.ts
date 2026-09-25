import type { Page } from "@playwright/test"

/*
 * The authentication endpoints, stubbed.
 *
 * The e2e suite runs the built bundle against a preview server with no API
 * behind it, so every auth call is answered here. The shapes match
 * src/HpacSafety.Api/Authentication/AuthEndpoints.cs — what is being asserted
 * is the browser's behaviour, and the API's own is covered by
 * HpacSafety.Api.Tests.
 */

export type Role = "user" | "safety_officer" | "administrator"

export const CREDENTIALS: Record<Role, { username: string; password: string }> = {
	administrator: { username: "admin", password: "admin" },
	safety_officer: { username: "officer", password: "officer" },
	user: { username: "user", password: "user" },
}

// What each page's stubbed /api/auth/config should answer. Held per page so a
// later stubAuth() call with no options does not quietly undo an earlier one
// that asked for a provider — scenarios set the mode in a Given and then walk
// through a When that also needs the endpoints stubbed.
const configured = new WeakMap<Page, boolean>()

// Pages whose scenario stubbed the Admin menu's pending counts itself, so the
// default below does not shadow it: Playwright tries the latest route first.
export const pendingCountsStubbed = new WeakSet<Page>()

/** Answers the three auth endpoints for a member of this role. */
export async function stubAuth(page: Page, options?: { thirdPartySignIn?: boolean }) {
	if (options?.thirdPartySignIn !== undefined) {
		configured.set(page, options.thirdPartySignIn)
	} else if (!configured.has(page)) {
		configured.set(page, false)
	}

	// The header reads the Admin menu's counts on every page; with no API behind
	// the preview server, nothing is waiting unless a scenario says otherwise.
	if (!pendingCountsStubbed.has(page)) {
		await page.route("**/api/admin/counts", (route) =>
			route.fulfill({
				status: 200,
				contentType: "application/json",
				body: JSON.stringify({ reportsNeedingAction: 0, answersAwaitingTranslation: null, typeAheadValuesAwaitingReview: 0 }),
			}),
		)
	}

	await page.route("**/api/auth/config", (route) => {
		const thirdPartySignIn = configured.get(page) ?? false

		return route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({
				mode: thirdPartySignIn ? "provider" : "development",
				thirdPartySignIn,
				authority: thirdPartySignIn ? "https://provider.example.test" : null,
			}),
		})
	})

	await page.route("**/api/auth/token", async (route) => {
		const body = route.request().postDataJSON() as { username?: string; password?: string }

		const match = (Object.entries(CREDENTIALS) as [Role, { username: string; password: string }][]).find(
			([, pair]) => pair.username === body?.username && pair.password === body?.password,
		)

		if (!match) {
			// One generic failure, as the API gives.
			return route.fulfill({
				status: 401,
				contentType: "application/problem+json",
				body: JSON.stringify({
					type: "https://hpac.ca/problems/invalid-credentials",
					title: "Those credentials were not accepted.",
					status: 401,
				}),
			})
		}

		const [role] = match

		return route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify({
				accessToken: `synthetic.${role}.token`,
				expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
				subject: `dev:${CREDENTIALS[role].username}`,
				role,
			}),
		})
	})

	await page.route("**/api/auth/me", (route) => {
		const authorization = route.request().headers()["authorization"] ?? ""
		const role = (["administrator", "safety_officer", "user"] as Role[]).find((candidate) =>
			authorization.includes(candidate),
		)

		return role
			? route.fulfill({
					status: 200,
					contentType: "application/json",
					body: JSON.stringify({ subject: `dev:${CREDENTIALS[role].username}`, role }),
				})
			: route.fulfill({ status: 401, contentType: "application/problem+json", body: "{}" })
	})
}

/** Signs in through the form, as a member would. */
export async function signInAs(page: Page, role: Role) {
	await stubAuth(page)
	await page.goto("/login")

	const { username, password } = CREDENTIALS[role]
	// Either language: a scenario may choose French before the member signs in.
	await page.getByLabel(/^(Username|Nom d'utilisateur)$/).fill(username)
	await page.getByLabel(/^(Password|Mot de passe)$/).fill(password)
	await page.getByRole("button", { name: /^(Log in|Ouvrir une session)$/ }).click()

	// The form navigates home once the token is stored.
	await page.waitForURL((url) => !url.pathname.startsWith("/login"))
}
