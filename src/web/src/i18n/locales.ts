export const SUPPORTED_LOCALES = ["en-CA", "fr-CA"] as const

export type Locale = (typeof SUPPORTED_LOCALES)[number]

export const DEFAULT_LOCALE: Locale = "en-CA"

export const STORAGE_KEY = "hpac.locale"

export function isSupportedLocale(value: string): value is Locale {
	return (SUPPORTED_LOCALES as readonly string[]).includes(value)
}

// Production serves one site on two hostnames (issue 463): safety.hpac.ca in
// English, securite.acvl.ca in French. An unlisted host — staging's
// *.cloudfront.net address, or localhost — has no entry and falls through to
// the browser language.
const HOSTNAME_LOCALES: Readonly<Record<string, Locale>> = {
	"safety.hpac.ca": "en-CA",
	"securite.acvl.ca": "fr-CA",
}

export function localeForHostname(hostname: string): Locale | null {
	return HOSTNAME_LOCALES[hostname] ?? null
}
