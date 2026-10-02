import { ForbiddenPageView } from "./ForbiddenPage.view"

/** Shown in place of an admin route's content when the signed-in member's role cannot use it (ADR-0092). */
export function ForbiddenPage() {
	return <ForbiddenPageView />
}
