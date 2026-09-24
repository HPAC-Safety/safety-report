import { useLocale } from "../i18n/useLocale"
import type { ReportConsent, ReportStatus } from "../api/adminReports"

const BADGE = "inline-flex items-center rounded-full border px-3 py-0.5 font-sans text-xs font-medium"

/*
 * A report's workflow status and, separately, whether its reporter refused
 * publication. Private is about consent and Rejected is a reviewer's decision,
 * so they are never merged into one badge. Tokens only: red is kept for the
 * primary action and errors, so badges differ by weight and fill, not by hue.
 */
export function ReportBadges({
	status,
	consent,
	isStuck,
}: {
	status: ReportStatus
	consent: ReportConsent
	isStuck: boolean
}) {
	const { t } = useLocale()

	return (
		<span className="flex flex-wrap items-center gap-2">
			<span className={`${BADGE} border-rule bg-surface-2 text-ink`} data-badge="status">
				{t(`reports.status.${status}`)}
			</span>
			{consent === "no" && (
				<span className={`${BADGE} border-ink bg-surface text-ink`} data-badge="private">
					{t("reports.badge.private")}
				</span>
			)}
			{isStuck && (
				<span className={`${BADGE} border-brand-700 bg-surface text-ink`} data-badge="stuck">
					{t("reports.badge.stuck")}
				</span>
			)}
		</span>
	)
}
