import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from "react"
import { DEFAULT_LOCALE, STORAGE_KEY, type Locale } from "./locales"
import { loadCatalogue, type Catalogue } from "./loadCatalogue"
import { resolveInitialLocale } from "./resolveInitialLocale"

export interface LocaleContextValue {
	locale: Locale
	setLocale: (locale: Locale) => void
	/** Looks up `key` in the current catalogue, interpolating `{token}` placeholders from `params`. */
	t: (key: string, params?: Record<string, string | number>) => string
}

export const LocaleContext = createContext<LocaleContextValue | null>(null)

function readStoredLocale(): string | null {
	try {
		return localStorage.getItem(STORAGE_KEY)
	} catch {
		return null
	}
}

function interpolate(text: string, params?: Record<string, string | number>): string {
	if (!params) return text
	return text.replace(/\{(\w+)\}/g, (match, token) => (token in params ? String(params[token]) : match))
}

export function LocaleProvider({ children }: { children: ReactNode }) {
	const [locale, setLocaleState] = useState<Locale>(() =>
		resolveInitialLocale(readStoredLocale(), navigator.languages ?? [navigator.language]),
	)
	const [catalogue, setCatalogue] = useState<Catalogue>({})

	useEffect(() => {
		let cancelled = false
		loadCatalogue(locale).then((loaded) => {
			if (!cancelled) setCatalogue(loaded)
		})
		return () => {
			cancelled = true
		}
	}, [locale])

	useEffect(() => {
		document.documentElement.lang = locale
	}, [locale])

	useEffect(() => {
		if (catalogue["app.title"]) document.title = catalogue["app.title"]
	}, [catalogue])

	// Persisted only on an explicit user choice, not on automatic detection,
	// so a visitor who never picks a language keeps following browser changes.
	const setLocale = useCallback((next: Locale) => {
		setLocaleState(next)
		try {
			localStorage.setItem(STORAGE_KEY, next)
		} catch {
			// Storage may be unavailable (private browsing, quota); the choice
			// still applies for this page view.
		}
	}, [])

	const t = useCallback(
		(key: string, params?: Record<string, string | number>) => interpolate(catalogue[key] ?? key, params),
		[catalogue],
	)

	const value = useMemo<LocaleContextValue>(() => ({ locale, setLocale, t }), [locale, setLocale, t])

	return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}

export { DEFAULT_LOCALE }
