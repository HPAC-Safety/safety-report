import { useLocale } from "../i18n/useLocale"

/*
 * The pill on a reporter's own report before it is published (issue no. 820,
 * ADR-0196): "Not yet published", or "Not for publication" when the reporter
 * did not consent to publication. Never "Unpublished", which names a lifecycle
 * state a reviewer sets (issue no. 445). Tokens only, as the report badges are.
 */
export interface OwnReportPillViewProps {
	forPublication: boolean
}

export function OwnReportPillView({ forPublication }: OwnReportPillViewProps) {
	const { t } = useLocale()

	return (
		<span
			data-own-pill={forPublication ? "not-yet-published" : "not-for-publication"}
			className="inline-flex items-center rounded-full border border-ink bg-surface px-3 py-0.5 font-sans text-xs font-medium text-ink"
		>
			{t(forPublication ? "feed.own.notYetPublished" : "feed.own.notForPublication")}
		</span>
	)
}
