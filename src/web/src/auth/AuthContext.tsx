import { createContext, useCallback, useMemo, useState, type ReactNode } from "react"

const STORAGE_KEY = "hpac.memberSession"

export interface AuthContextValue {
	isSignedIn: boolean
	signIn: () => void
	signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

function readInitialSignedIn(): boolean {
	try {
		return sessionStorage.getItem(STORAGE_KEY) !== null
	} catch {
		return false
	}
}

// Stands in for the session IMemberAuthenticator/OidcAuthenticator will
// establish (ADR-0005). Gets replaced wholesale, not extended, once that
// adapter and a real token exist.
export function AuthProvider({ children }: { children: ReactNode }) {
	const [isSignedIn, setIsSignedIn] = useState(readInitialSignedIn)

	const signIn = useCallback(() => {
		setIsSignedIn(true)
		try {
			sessionStorage.setItem(STORAGE_KEY, "true")
		} catch {
			// Storage may be unavailable; the signed-in state still applies for this page view.
		}
	}, [])

	const signOut = useCallback(() => {
		setIsSignedIn(false)
		try {
			sessionStorage.removeItem(STORAGE_KEY)
		} catch {
			// Storage may be unavailable; the signed-out state still applies for this page view.
		}
	}, [])

	const value = useMemo<AuthContextValue>(() => ({ isSignedIn, signIn, signOut }), [isSignedIn, signIn, signOut])

	return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
