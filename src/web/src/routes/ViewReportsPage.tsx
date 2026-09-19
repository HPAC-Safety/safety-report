import { useLocale } from "../i18n/useLocale"
import { PlaceholderPage } from "./PlaceholderPage"

export function ViewReportsPage() {
	const { t } = useLocale()
	return <PlaceholderPage pageName={t("nav.viewReports")} />
}
