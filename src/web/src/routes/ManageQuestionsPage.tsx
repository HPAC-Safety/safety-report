import { useLocale } from "../i18n/useLocale"
import { PlaceholderPage } from "./PlaceholderPage"

export function ManageQuestionsPage() {
	const { t } = useLocale()
	return <PlaceholderPage pageName={t("nav.manageQuestions")} />
}
