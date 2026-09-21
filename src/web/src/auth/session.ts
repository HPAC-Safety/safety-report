/*
 * The signed-in session, as this browser holds it.
 *
 * The token is stored, never parsed. Role and expiry come from the API's token
 * response, because a browser reading claims out of a JWT is a browser deciding
 * what it is allowed to do — and the authorization boundary is the API
 * (ADR-0048). What is kept here only decides what chrome to draw.
 */

export const MEMBER_ROLES = ["user", "safety_officer", "administrator"] as const

export type MemberRole = (typeof MEMBER_ROLES)[number]

export interface MemberSession {
	accessToken: string
	expiresAt: string
	subject: string
	role: MemberRole
}

// A new key rather than the old `hpac.memberSession` marker: what is stored
// now is a different shape, and a stale marker must not read as a session.
const STORAGE_KEY = "hpac.session"

export function readSession(): MemberSession | null {
	try {
		const stored = sessionStorage.getItem(STORAGE_KEY)
		if (!stored) return null

		const parsed = JSON.parse(stored) as Partial<MemberSession>
		return isSession(parsed) ? parsed : null
	} catch {
		// Storage can be unavailable, and stored JSON can be anything. Either
		// way this browser is signed out rather than half signed in.
		return null
	}
}

export function writeSession(session: MemberSession): void {
	try {
		sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
	} catch {
		// The session still applies to this page view.
	}
}

export function clearSession(): void {
	try {
		sessionStorage.removeItem(STORAGE_KEY)
	} catch {
		// Signed out for this page view regardless.
	}
}

function isSession(value: Partial<MemberSession>): value is MemberSession {
	return (
		typeof value.accessToken === "string" &&
		value.accessToken.length > 0 &&
		typeof value.subject === "string" &&
		typeof value.expiresAt === "string" &&
		MEMBER_ROLES.includes(value.role as MemberRole)
	)
}
