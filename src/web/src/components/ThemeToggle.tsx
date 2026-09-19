import { useTheme } from "../theme/useTheme"
import { useLocale } from "../i18n/useLocale"

function prefersDark(): boolean {
	return typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches
}

function SunIcon() {
	return (
		<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
			<circle cx="12" cy="12" r="4" />
			<path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
		</svg>
	)
}

function MoonIcon() {
	return (
		<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
			<path d="M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5Z" />
		</svg>
	)
}

export function ThemeToggle() {
	const { theme, setTheme } = useTheme()
	const { t } = useLocale()
	const effectiveDark = theme === "dark" || (theme === null && prefersDark())

	return (
		<button
			type="button"
			onClick={() => setTheme(effectiveDark ? "light" : "dark")}
			aria-label={t("theme.toggleLabel")}
			aria-pressed={effectiveDark}
			className="touch-target inline-flex items-center justify-center rounded text-ink-muted hover:text-ink"
		>
			{effectiveDark ? <SunIcon /> : <MoonIcon />}
		</button>
	)
}
