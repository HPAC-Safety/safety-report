import { useTheme } from "../theme/useTheme"
import { ThemeToggleView, type ThemeToggleViewProps } from "./ThemeToggle.view"

function prefersDark(): boolean {
	return typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches
}

export function useThemeToggle(): ThemeToggleViewProps {
	const { theme, setTheme } = useTheme()
	const effectiveDark = theme === "dark" || (theme === null && prefersDark())

	return { effectiveDark, onToggle: () => setTheme(effectiveDark ? "light" : "dark") }
}

export function ThemeToggle() {
	return <ThemeToggleView {...useThemeToggle()} />
}
