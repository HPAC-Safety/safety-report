import { useEffect, useState } from "react"
import { Link, useSearchParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import { isReportFilter, listReports, REPORT_FILTERS, type ReportListItem } from "../api/adminReports"
import { ReportBadges } from "../components/ReportBadges"

/*
 * Every live report, newest first, with its workflow status, a Private badge
 * when the reporter refused publication, and a Stuck badge when it has waited
 * on summarization for more than a day. The filter lives in the address bar
 * so a reviewer can bookmark or share "needs action". The list itself carries
 * no answer or summary text; opening a report is the audited read.
 */
export function ManageReportsPage() {
	const { t, locale } = useLocale()
	const [searchParams] = useSearchParams()
	const requested = searchParams.get("filter")
	const filter = isReportFilter(requested) ? requested : "all"
	const [reports, setReports] = useState<ReportListItem[]>([])
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)

	useEffect(() => {
		let current = true
		setLoading(true)
		listReports(filter)
			.then((listed) => {
				if (current) {
					setReports(listed)
					setError(null)
				}
			})
			.catch((cause: unknown) => {
				if (current) {
					setError(cause instanceof ApiError ? cause.detail : t("reports.error.unexpected"))
				}
			})
			.finally(() => {
				if (current) {
					setLoading(false)
				}
			})
		return () => {
			current = false
		}
	}, [filter, t])

	const submitted = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("nav.manageReports")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("reports.intro")}</p>

			<nav aria-label={t("reports.filter.label")} className="mt-6">
				<ul className="flex flex-wrap gap-2">
					{REPORT_FILTERS.map((option) => (
						<li key={option}>
							<Link
								to={option === "all" ? "/admin/reports" : `/admin/reports?filter=${option}`}
								aria-current={option === filter ? "page" : undefined}
								className={
									option === filter
										? "touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse"
										: "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
								}
							>
								{t(`reports.filter.${option}`)}
							</Link>
						</li>
					))}
				</ul>
			</nav>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("reports.loading")}</p>
			) : reports.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{t("reports.empty")}</p>
			) : (
				<ul aria-label={t("reports.listLabel")} className="mt-8 flex flex-col gap-3">
					{reports.map((report) => (
						<li key={report.id} data-report-id={report.id}>
							<Link
								to={`/admin/reports/${report.id}`}
								className="flex flex-col gap-2 rounded border border-rule bg-surface p-4 hover:bg-surface-2 sm:flex-row sm:items-center sm:justify-between"
							>
								<span className="font-sans text-ink">
									{t("reports.submittedAt", { at: submitted.format(new Date(report.submittedAt)) })}
								</span>
								<ReportBadges status={report.status} consent={report.consent} isStuck={report.isStuck} />
							</Link>
						</li>
					))}
				</ul>
			)}
		</main>
	)
}
