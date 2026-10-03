import { createContext } from "react"

import type { MemberRole } from "./session"

export type AuthStatus = "unknown" | "signedOut" | "signedIn"

export interface AuthContextValue {
	/** Whether the stored session has been checked yet. */
	status: AuthStatus
	isSignedIn: boolean
	/** The role the API says this token carries, once signed in. */
	role: MemberRole | null
	/**
	 * Signs in with member credentials. Throws when they are not accepted.
	 */
	signInWithPassword: (username: string, password: string) => Promise<void>
	signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
