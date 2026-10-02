import { AdminPageView } from "./AdminPage.view"

// Confirms ADR-0048's shape only: /admin is a route in this same app, not a
// separate application. No real admin UI yet — that is future work.
export function AdminPage() {
	return <AdminPageView />
}
