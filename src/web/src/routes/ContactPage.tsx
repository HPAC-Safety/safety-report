import { useLocale } from "../i18n/useLocale"
import { PlaceholderPage } from "./PlaceholderPage"

export function ContactPage() {
	const { t } = useLocale()
	return <PlaceholderPage pageName={t("nav.contact")} />
}
