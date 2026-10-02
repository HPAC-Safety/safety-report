import type { ReactNode } from "react"
import { Navigate } from "react-router-dom"
import { ForbiddenPage } from "../routes/ForbiddenPage"

/** What the guard draws: nothing yet, the sign-in redirect, the forbidden view, or the route itself. */
export type AdminRouteGuardOutcome = "checking" | "signedOut" | "forbidden" | "allowed"

export interface AdminRouteGuardViewProps {
	outcome: AdminRouteGuardOutcome
	children: ReactNode
}

export function AdminRouteGuardView({ outcome, children }: AdminRouteGuardViewProps) {
	// The stored session is still being checked against the API — render
	// nothing rather than guess, so no admin content ever shows first.
	if (outcome === "checking") {
		return null
	}

	if (outcome === "signedOut") {
		return <Navigate to="/login" replace />
	}

	if (outcome === "forbidden") {
		return <ForbiddenPage />
	}

	return children
}
