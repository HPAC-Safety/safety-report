import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from "react"

import { fetchIdentity, requestToken } from "./authApi"
import { clearSession, readSession, writeSession, type MemberRole, type MemberSession } from "./session"

export type AuthStatus = "unknown" | "signedOut" | "signedIn"

export interface AuthContextValue {
	/** Whether the stored session has been checked yet. */
	status: AuthStatus
	isSignedIn: boolean
	/** The role the API says this token carries, once signed in. */
	role: MemberRole | null
	/**
	 * Signs in with member credentials. Throws when they are not accepted.
	 *
	 * Declared as a method rather than an arrow property so the line carries no
	 * `=>` before its generic: tools/check-hardcoded-strings.mjs is a line
	 * scanner and reads `=> Promise<void>` as JSX text between a `>` and a `<`.
	 * adminQuestions.ts wraps its `call` signature for the same reason.
	 */
	signInWithPassword(username: string, password: string): Promise<void>
	signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * Holds the bearer token this browser signed in with.
 *
 * The token is never parsed here. Role and expiry come from the API's
 * response, and a stored token is re-checked against `/api/auth/me` on load —
 * so a revoked or expired one becomes a signed-out browser rather than chrome
 * that lies about what it can do.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
	const [session, setSession] = useState<MemberSession | null>(null)
	const [status, setStatus] = useState<AuthStatus>("unknown")

	useEffect(() => {
		const stored = readSession()

		if (!stored) {
			setStatus("signedOut")
			return
		}

		let cancelled = false

		// Ask the API rather than trusting what is in storage: the token may
		// have expired, and only the API can say.
		fetchIdentity(stored.accessToken)
			.then((identity) => {
				if (cancelled) return

				if (!identity) {
					clearSession()
					setStatus("signedOut")
					return
				}

				// The API is the authority on the role, even where storage
				// disagrees.
				const current = { ...stored, subject: identity.subject, role: identity.role }
				writeSession(current)
				setSession(current)
				setStatus("signedIn")
			})
			.catch(() => {
				if (cancelled) return

				// An unreachable API is not proof of a valid session.
				clearSession()
				setStatus("signedOut")
			})

		return () => {
			cancelled = true
		}
	}, [])

	const signInWithPassword = useCallback(async (username: string, password: string) => {
		const issued = await requestToken(username, password)
		writeSession(issued)
		setSession(issued)
		setStatus("signedIn")
	}, [])

	const signOut = useCallback(() => {
		clearSession()
		setSession(null)
		setStatus("signedOut")
	}, [])

	const value = useMemo<AuthContextValue>(
		() => ({
			status,
			isSignedIn: status === "signedIn",
			role: session?.role ?? null,
			signInWithPassword,
			signOut,
		}),
		[status, session, signInWithPassword, signOut],
	)

	return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
