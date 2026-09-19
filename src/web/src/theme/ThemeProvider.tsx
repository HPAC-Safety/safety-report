import { createContext, useCallback, useMemo, useState, type ReactNode } from "react"
import { STORAGE_KEY, type Theme } from "./resolveInitialTheme"

export interface ThemeContextValue {
	/** `null` means no explicit override — the page follows prefers-color-scheme. */
	theme: Theme | null
	setTheme: (theme: Theme | null) => void
}

export const ThemeContext = createContext<ThemeContextValue | null>(null)

function readInitialTheme(): Theme | null {
	// The inline script in index.html already set (or cleared) this attribute
	// before React mounted, using the same localStorage key. Reading it back
	// rather than re-deriving from localStorage keeps the two in agreement —
	// no separate logic to drift out of sync, no hydration flash.
	const attr = document.documentElement.dataset.theme
	return attr === "light" || attr === "dark" ? attr : null
}

export function ThemeProvider({ children }: { children: ReactNode }) {
	const [theme, setThemeState] = useState<Theme | null>(readInitialTheme)

	const setTheme = useCallback((next: Theme | null) => {
		setThemeState(next)
		if (next) {
			document.documentElement.dataset.theme = next
		} else {
			delete document.documentElement.dataset.theme
		}
		try {
			if (next) {
				localStorage.setItem(STORAGE_KEY, next)
			} else {
				localStorage.removeItem(STORAGE_KEY)
			}
		} catch {
			// Storage may be unavailable; the choice still applies for this page view.
		}
	}, [])

	const value = useMemo<ThemeContextValue>(() => ({ theme, setTheme }), [theme, setTheme])

	return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
