import { NavLink } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"

const rowClassName = ({ isActive }: { isActive: boolean }) =>
	`touch-target inline-flex items-center rounded px-2 font-sans text-sm font-medium underline-offset-4 ${
		isActive ? "text-brand-700 underline" : "text-ink hover:underline"
	}`

const stackedClassName = ({ isActive }: { isActive: boolean }) =>
	`touch-target flex items-center rounded px-2 font-sans text-base font-medium underline-offset-4 ${
		isActive ? "text-brand-700 underline" : "text-ink hover:underline"
	}`

export function Nav({ stacked = false, onNavigate }: { stacked?: boolean; onNavigate?: () => void }) {
	const { t } = useLocale()
	const linkClassName = stacked ? stackedClassName : rowClassName

	return (
		<nav
			aria-label={t("nav.primaryLabel")}
			className={stacked ? "flex flex-col" : "flex flex-nowrap items-center gap-1"}
		>
			<NavLink to="/reports" className={linkClassName} onClick={onNavigate}>
				{t("nav.viewReports")}
			</NavLink>
			<NavLink to="/report" className={linkClassName} onClick={onNavigate}>
				{t("nav.submitReport")}
			</NavLink>
			<NavLink to="/contact" className={linkClassName} onClick={onNavigate}>
				{t("nav.contact")}
			</NavLink>
		</nav>
	)
}
