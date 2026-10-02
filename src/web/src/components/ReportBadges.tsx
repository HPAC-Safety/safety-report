export type { ReportConsent, ReportStatus } from "../api/adminReports"
import { ReportBadgesView, type ReportBadgesViewProps } from "./ReportBadges.view"

/** A markup-only component: the pass-through the split convention asks for (ADR-0188). */
export function ReportBadges(props: ReportBadgesViewProps) {
	return <ReportBadgesView {...props} />
}
