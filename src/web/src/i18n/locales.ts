export const SUPPORTED_LOCALES = ["en-CA", "fr-CA"] as const

export type Locale = (typeof SUPPORTED_LOCALES)[number]

export const DEFAULT_LOCALE: Locale = "en-CA"

export const STORAGE_KEY = "hpac.locale"

export function isSupportedLocale(value: string): value is Locale {
	return (SUPPORTED_LOCALES as readonly string[]).includes(value)
}
