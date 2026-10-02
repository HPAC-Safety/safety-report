import { NavLink } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"

export interface NavViewProps {
	/** The class of the `<nav>` itself. */
	navClassName: string
	/** The class of each link, given whether its route is the current one. */
	linkClassName: (state: { isActive: boolean }) => string
	onNavigate?: () => void
}

export function NavView({ navClassName, linkClassName, onNavigate }: NavViewProps) {
	const { t } = useLocale()

	return (
		<nav aria-label={t("nav.primaryLabel")} className={navClassName}>
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
