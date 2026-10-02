import { useLocale } from "../i18n/useLocale"
import type { Locale } from "../i18n/locales"
import { LanguageToggleView, type LanguageToggleViewProps } from "./LanguageToggle.view"

const localeCode: Record<Locale, string> = {
	"en-CA": "en",
	"fr-CA": "fr",
}

export function useLanguageToggle(): LanguageToggleViewProps {
	const { locale, setLocale, t } = useLocale()
	const other: Locale = locale === "en-CA" ? "fr-CA" : "en-CA"
	const otherLabel = other === "en-CA" ? t("language.optionEnglish") : t("language.optionFrench")

	return {
		code: localeCode[locale],
		label: t("language.switchTo", { language: otherLabel }),
		onToggle: () => setLocale(other),
	}
}

export function LanguageToggle() {
	return <LanguageToggleView {...useLanguageToggle()} />
}
