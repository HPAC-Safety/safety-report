import { DEFAULT_LOCALE, isSupportedLocale, type Locale } from "./locales"

/**
 * Priority order per skills/localize-hpac-app/SKILL.md: an explicit stored
 * choice, then the browser's languages, then English. Pure and
 * browser-free so it can be exercised without a DOM.
 */
export function resolveInitialLocale(storedValue: string | null, navigatorLanguages: readonly string[]): Locale {
	if (storedValue && isSupportedLocale(storedValue)) {
		return storedValue
	}

	for (const language of navigatorLanguages) {
		if (isSupportedLocale(language)) {
			return language
		}
	}

	const prefixMatch = navigatorLanguages
		.map((language) => language.split("-")[0])
		.find((languagePrefix) => isSupportedLocale(`${languagePrefix}-CA`))

	if (prefixMatch) {
		return `${prefixMatch}-CA` as Locale
	}

	return DEFAULT_LOCALE
}
