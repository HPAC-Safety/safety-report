import { createContext } from "react"
import type { Locale } from "./locales"

export interface LocaleContextValue {
	locale: Locale
	setLocale: (locale: Locale) => void
	/** Looks up `key` in the current catalogue, interpolating `{token}` placeholders from `params`. */
	t: (key: string, params?: Record<string, string | number>) => string
}

export const LocaleContext = createContext<LocaleContextValue | null>(null)
