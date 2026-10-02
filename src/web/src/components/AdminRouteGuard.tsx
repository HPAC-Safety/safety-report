import type { ReactNode } from "react"
import { useAuth } from "../auth/useAuth"
import { AdminRouteGuardView, type AdminRouteGuardOutcome } from "./AdminRouteGuard.view"

export interface AdminRouteGuardProps {
	requires: "reviewer" | "administrator"
	children: ReactNode
}

export function useAdminRouteGuard({ requires }: AdminRouteGuardProps): { outcome: AdminRouteGuardOutcome } {
	const { status, isSignedIn, role } = useAuth()

	if (status === "unknown") {
		return { outcome: "checking" }
	}

	if (!isSignedIn) {
		return { outcome: "signedOut" }
	}

	const allowed = requires === "administrator" ? role === "administrator" : role === "administrator" || role === "safety_officer"

	return { outcome: allowed ? "allowed" : "forbidden" }
}

/**
 * Wraps an `/admin/*` route element (ADR-0092).
 *
 * The API authorizes every admin request on its own (ADR-0048) — this guard
 * only decides what the browser draws before a request is even made, so a
 * visitor who is signed out is sent to sign in, and a signed-in visitor whose
 * role cannot use the route sees a real forbidden view rather than the page's
 * content or a 404.
 */
export function AdminRouteGuard(props: AdminRouteGuardProps) {
	return <AdminRouteGuardView {...useAdminRouteGuard(props)}>{props.children}</AdminRouteGuardView>
}
