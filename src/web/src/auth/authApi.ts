/*
 * The three authentication endpoints, as the browser calls them.
 *
 * The API tells this application which mode it is running in. There is no
 * build flag: one built bundle is promoted across environments, so a
 * build-time switch would either need a second build or ship a bundle that
 * lies about where it is running (ADR-0066).
 */

import type { MemberRole, MemberSession } from "./session"

export interface AuthConfig {
	mode: "development" | "provider"
	thirdPartySignIn: boolean
	authority: string | null
}

export class SignInError extends Error {
	constructor() {
		super("sign-in-failed")
		this.name = "SignInError"
	}
}

let cachedConfig: Promise<AuthConfig> | null = null

/** Reads the authentication configuration once per page load. */
export function loadAuthConfig(): Promise<AuthConfig> {
	cachedConfig ??= fetch("/api/auth/config")
		.then(async (response) => {
			if (!response.ok) throw new Error("config")
			return (await response.json()) as AuthConfig
		})
		.catch(() => {
			// An unreachable API must not offer a sign-in route that cannot
			// work. Assume the narrower option.
			cachedConfig = null
			return { mode: "development", thirdPartySignIn: false, authority: null } satisfies AuthConfig
		})

	return cachedConfig
}

/** Exchanges development credentials for a signed token. */
export async function requestToken(username: string, password: string): Promise<MemberSession> {
	const response = await fetch("/api/auth/token", {
		method: "POST",
		headers: { "Content-Type": "application/json" },
		body: JSON.stringify({ username, password }),
	})

	// One failure for every reason, matching the API. The page must not infer
	// more than it was told.
	if (!response.ok) throw new SignInError()

	const token = (await response.json()) as {
		accessToken: string
		expiresAt: string
		subject: string
		role: MemberRole
	}

	return token
}

/** Confirms a stored token is still valid, and returns who it says you are. */
export async function fetchIdentity(accessToken: string): Promise<{ subject: string; role: MemberRole } | null> {
	const response = await fetch("/api/auth/me", {
		headers: { Authorization: `Bearer ${accessToken}` },
	})

	return response.ok ? ((await response.json()) as { subject: string; role: MemberRole }) : null
}
