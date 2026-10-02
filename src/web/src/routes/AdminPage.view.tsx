import { useLocale } from "../i18n/useLocale"
import { PlaceholderPage } from "./PlaceholderPage"

export function AdminPageView() {
	const { t } = useLocale()
	return <PlaceholderPage pageName={t("admin.placeholderName")} />
}
