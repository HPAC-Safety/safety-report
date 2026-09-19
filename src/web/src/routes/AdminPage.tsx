import { useLocale } from "../i18n/useLocale"
import { PlaceholderPage } from "./PlaceholderPage"

// Confirms ADR-0048's shape only: /admin is a route in this same app, not a
// separate application. No real admin UI yet — that is future work.
export function AdminPage() {
	const { t } = useLocale()
	return <PlaceholderPage pageName={t("admin.placeholderName")} />
}
