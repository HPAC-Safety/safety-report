import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react"
import { DEFAULT_LOCALE, STORAGE_KEY, type Locale } from "./locales"
import { catalogueFor } from "./catalogueFor"
import { LocaleContext, type LocaleContextValue } from "./localeContext"
import { LocaleProviderView } from "./LocaleProvider.view"
import { resolveInitialLocale } from "./resolveInitialLocale"

function readStoredLocale(): string | null {
	try {
		return localStorage.getItem(STORAGE_KEY)
	} catch {
		return null
	}
}

function interpolate(text: string, params?: Record<string, string | number>): string {
	if (!params) return text
	return text.replace(/\{(\w+)\}/g, (match: string, token: string) => (token in params ? String(params[token]) : match))
}

export function useLocaleProvider(): LocaleContextValue {
	const [locale, setLocaleState] = useState<Locale>(() =>
		// eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- navigator.languages is absent in some older browsers
		resolveInitialLocale(readStoredLocale(), navigator.languages ?? [navigator.language], window.location.hostname),
	)
	// Bundled, so the first render already has its text (REQ-WLD-049).
	const catalogue = useMemo(() => catalogueFor(locale), [locale])

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

	return useMemo<LocaleContextValue>(() => ({ locale, setLocale, t }), [locale, setLocale, t])
}

export function LocaleProvider({ children }: { children: ReactNode }) {
	return <LocaleProviderView value={useLocaleProvider()}>{children}</LocaleProviderView>
}

export { DEFAULT_LOCALE, LocaleContext, type LocaleContextValue }
