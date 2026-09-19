import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"

export function Footer() {
	const { t } = useLocale()

	return (
		<footer className="border-t border-rule bg-surface-2">
			<div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-4 px-6 py-6 font-sans text-sm text-ink-muted">
				<p>{t("footer.copyright", { year: new Date().getFullYear() })}</p>
				<nav aria-label={t("nav.primaryLabel")} className="flex flex-wrap gap-4">
					<Link to="/reports" className="touch-target inline-flex items-center underline-offset-4 hover:underline">
						{t("nav.viewReports")}
					</Link>
					<Link to="/report" className="touch-target inline-flex items-center underline-offset-4 hover:underline">
						{t("nav.submitReport")}
					</Link>
					<Link to="/contact" className="touch-target inline-flex items-center underline-offset-4 hover:underline">
						{t("nav.contact")}
					</Link>
				</nav>
			</div>
		</footer>
	)
}
