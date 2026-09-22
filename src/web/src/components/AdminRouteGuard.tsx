import type { ReactNode } from "react"
import { Navigate } from "react-router-dom"
import { useAuth } from "../auth/useAuth"
import { ForbiddenPage } from "../routes/ForbiddenPage"

/**
 * Wraps an `/admin/*` route element (ADR-0091).
 *
 * The API authorizes every admin request on its own (ADR-0048) — this guard
 * only decides what the browser draws before a request is even made, so a
 * visitor who is signed out is sent to sign in, and a signed-in visitor whose
 * role cannot use the route sees a real forbidden view rather than the page's
 * content or a 404.
 */
export function AdminRouteGuard({
	requires,
	children,
}: {
	requires: "reviewer" | "administrator"
	children: ReactNode
}) {
	const { status, isSignedIn, role } = useAuth()

	// The stored session is still being checked against the API — render
	// nothing rather than guess, so no admin content ever shows first.
	if (status === "unknown") {
		return null
	}

	if (!isSignedIn) {
		return <Navigate to="/login" replace />
	}

	const allowed = requires === "administrator" ? role === "administrator" : role === "administrator" || role === "safety_officer"

	if (!allowed) {
		return <ForbiddenPage />
	}

	return children
}
