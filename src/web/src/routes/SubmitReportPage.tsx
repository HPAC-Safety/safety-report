import { useAuth } from "../auth/useAuth"
import { SubmitReportPageView } from "./SubmitReportPage.view"

export function useSubmitReportPage() {
	const { status, isSignedIn } = useAuth()
	return { status, isSignedIn }
}

/**
 * Filing a report requires a signed-in HPAC member, and records nothing about
 * them (ADR-0067).
 *
 * The notice is not decoration. An anonymity guarantee the reporter cannot see
 * is worth nothing, because the only thing that changes what somebody is
 * willing to write down is what they believe while they are typing it.
 */
export function SubmitReportPage() {
	return <SubmitReportPageView {...useSubmitReportPage()} />
}
