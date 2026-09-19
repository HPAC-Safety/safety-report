import { NavLink } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"

const linkClassName = ({ isActive }: { isActive: boolean }) =>
	`touch-target inline-flex items-center rounded px-2 font-sans text-sm font-medium underline-offset-4 ${
		isActive ? "text-brand-700 underline" : "text-ink hover:underline"
	}`

export function Nav() {
	const { t } = useLocale()

	return (
		<nav aria-label={t("nav.primaryLabel")} className="flex flex-wrap items-center gap-1">
			<NavLink to="/reports" className={linkClassName}>
				{t("nav.viewReports")}
			</NavLink>
			<NavLink to="/report" className={linkClassName}>
				{t("nav.submitReport")}
			</NavLink>
			<NavLink to="/contact" className={linkClassName}>
				{t("nav.contact")}
			</NavLink>
		</nav>
	)
}
