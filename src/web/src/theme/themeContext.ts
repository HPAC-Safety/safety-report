import { createContext } from "react"
import type { Theme } from "./resolveInitialTheme"

export interface ThemeContextValue {
	/** `null` means no explicit override — the page follows prefers-color-scheme. */
	theme: Theme | null
	setTheme: (theme: Theme | null) => void
}

export const ThemeContext = createContext<ThemeContextValue | null>(null)
