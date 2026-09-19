import { useTheme } from "../theme/useTheme"
import { useLocale } from "../i18n/useLocale"

function prefersDark(): boolean {
	return typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches
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
			className="touch-target rounded border border-rule bg-surface-2 px-3 font-sans text-sm font-medium text-ink"
		>
			{effectiveDark ? t("theme.optionDark") : t("theme.optionLight")}
		</button>
	)
}
