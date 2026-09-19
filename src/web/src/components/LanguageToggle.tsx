import { useLocale } from "../i18n/useLocale"
import type { Locale } from "../i18n/locales"

export function LanguageToggle() {
	const { locale, setLocale, t } = useLocale()
	const other: Locale = locale === "en-CA" ? "fr-CA" : "en-CA"
	const otherLabel = other === "en-CA" ? t("language.optionEnglish") : t("language.optionFrench")

	return (
		<button
			type="button"
			onClick={() => setLocale(other)}
			aria-label={t("language.toggleLabel")}
			className="touch-target rounded border border-rule bg-surface-2 px-3 font-sans text-sm font-medium text-ink"
		>
			{otherLabel}
		</button>
	)
}
