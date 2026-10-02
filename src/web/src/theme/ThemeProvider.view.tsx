import type { ReactNode } from "react"
import { ThemeContext, type ThemeContextValue } from "./themeContext"

export interface ThemeProviderViewProps {
	value: ThemeContextValue
	children: ReactNode
}

export function ThemeProviderView({ value, children }: ThemeProviderViewProps) {
	return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
