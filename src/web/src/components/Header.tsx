import { Link } from "react-router-dom"
import logo from "../../assets/hpac-logo.png"
import { useLocale } from "../i18n/useLocale"
import { Nav } from "./Nav"
import { LanguageToggle } from "./LanguageToggle"
import { ThemeToggle } from "./ThemeToggle"

export function Header() {
	const { t } = useLocale()

	return (
		<header className="border-b border-rule bg-surface">
			<div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-4 px-6 py-4">
				<Link to="/" className="touch-target inline-flex items-center rounded bg-logo-plate px-3 py-2">
					<img src={logo} alt={t("header.logoAlt")} width={260} height={125} className="h-10 w-auto" />
				</Link>

				<Nav />

				<div className="flex items-center gap-3">
					<LanguageToggle />
					<ThemeToggle />
					<Link
						to="/login"
						className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-semibold text-ink-inverse"
					>
						{t("nav.memberLogin")}
					</Link>
				</div>
			</div>
		</header>
	)
}
