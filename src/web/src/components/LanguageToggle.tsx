import { useLocale } from "../i18n/useLocale"
import type { Locale } from "../i18n/locales"

function GlobeIcon() {
	return (
		<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
			<circle cx="12" cy="12" r="9" />
			<path d="M3 12h18M12 3c2.5 2.5 3.75 5.5 3.75 9S14.5 20.5 12 21c-2.5-.5-3.75-5.5-3.75-9S9.5 5.5 12 3Z" />
		</svg>
	)
}

const localeCode: Record<Locale, string> = {
	"en-CA": "en",
	"fr-CA": "fr",
}

export function LanguageToggle() {
	const { locale, setLocale, t } = useLocale()
	const other: Locale = locale === "en-CA" ? "fr-CA" : "en-CA"
	const otherLabel = other === "en-CA" ? t("language.optionEnglish") : t("language.optionFrench")

	return (
		<button
			type="button"
			onClick={() => setLocale(other)}
			aria-label={t("language.switchTo", { language: otherLabel })}
			className="touch-target inline-flex items-center justify-center rounded text-ink-muted hover:text-ink"
		>
			<span className="relative inline-flex h-5 w-5 items-center justify-center">
				<GlobeIcon />
				<span aria-hidden="true" className="absolute -bottom-1.5 -right-1.5 rounded-sm bg-surface font-sans text-[9px] font-semibold leading-none text-ink">
					{localeCode[locale]}
				</span>
			</span>
		</button>
	)
}
