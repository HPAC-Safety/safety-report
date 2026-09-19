import { useLocale } from "../i18n/useLocale"
import { PlaceholderPage } from "./PlaceholderPage"

// Placeholder only — not a real authentication flow. See IMemberAuthenticator
// in the API for the eventual credential proxy this button will front.
export function MemberLoginPage() {
	const { t } = useLocale()
	return <PlaceholderPage pageName={t("nav.memberLogin")} />
}
