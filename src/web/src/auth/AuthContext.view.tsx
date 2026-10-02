import type { ReactNode } from "react"
import { AuthContext, type AuthContextValue } from "./authSessionContext"

export interface AuthProviderViewProps {
	value: AuthContextValue
	children: ReactNode
}

export function AuthProviderView({ value, children }: AuthProviderViewProps) {
	return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
