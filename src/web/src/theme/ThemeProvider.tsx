import { useCallback, useMemo, useState, type ReactNode } from "react"
import { ThemeContext, type ThemeContextValue } from "./themeContext"
import { ThemeProviderView } from "./ThemeProvider.view"
import { STORAGE_KEY, type Theme } from "./resolveInitialTheme"

function readInitialTheme(): Theme | null {
	// The inline script in index.html already set (or cleared) this attribute
	// before React mounted, using the same localStorage key. Reading it back
	// rather than re-deriving from localStorage keeps the two in agreement —
	// no separate logic to drift out of sync, no hydration flash.
	const attr = document.documentElement.dataset.theme
	return attr === "light" || attr === "dark" ? attr : null
}

export function useThemeProvider(): ThemeContextValue {
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

	return useMemo<ThemeContextValue>(() => ({ theme, setTheme }), [theme, setTheme])
}

export function ThemeProvider({ children }: { children: ReactNode }) {
	return <ThemeProviderView value={useThemeProvider()}>{children}</ThemeProviderView>
}

export { ThemeContext, type ThemeContextValue }
