import { useEffect, useState } from "react"
import { Link, useSearchParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { fetchPublicReports, summaryIn, type PublicReportPage } from "../api/publicReports"

/*
 * View safety reports: every published report, newest first, each linking to
 * its own address (/reports/<id>). The feed's page cursor lives in the address
 * bar, so the back button returns to the page the visitor came from and a page
 * can be bookmarked (REQ-MOD-079, REQ-MOD-082). Anonymous.
 */
export function ViewReportsPage() {
	const { t, locale } = useLocale()
	const [searchParams] = useSearchParams()
	const after = searchParams.get("after")
	const [page, setPage] = useState<PublicReportPage | null>(null)
	const [failed, setFailed] = useState(false)

	useEffect(() => {
		let current = true
		setPage(null)
		setFailed(false)
		fetchPublicReports(after)
			.then((loaded) => {
				if (current) {
					setPage(loaded)
				}
			})
			.catch(() => {
				if (current) {
					setFailed(true)
				}
			})
		return () => {
			current = false
		}
	}, [after])

	const published = new Intl.DateTimeFormat(locale, { dateStyle: "long" })

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("nav.viewReports")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("feed.intro")}</p>

			{failed ? (
				<p role="alert" className="mt-8 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{t("feed.error")}
				</p>
			) : !page ? (
				<p className="mt-8 font-sans text-ink-muted">{t("feed.loading")}</p>
			) : page.items.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{t("feed.empty")}</p>
			) : (
				<>
					<ul aria-label={t("feed.listLabel")} className="mt-8 flex flex-col gap-4">
						{page.items.map((report) => (
							<li key={report.id} data-report-id={report.id}>
								<Link
									to={`/reports/${report.id}`}
									className="flex flex-col gap-2 rounded border border-rule bg-surface p-5 hover:bg-surface-2"
								>
									<span className="font-sans text-sm text-ink-muted">
										{t("feed.publishedAt", { at: published.format(new Date(report.publishedAt)) })}
									</span>
									<span className="line-clamp-3 whitespace-pre-line font-sans text-ink">{summaryIn(report, locale)}</span>
									<span className="flex flex-wrap items-center gap-x-4 font-sans text-sm">
										<span className="font-medium text-brand-700 underline">{t("feed.read")}</span>
										<span data-comment-count={report.commentCount} className="text-ink-muted">
											{t(report.commentCount === 1 ? "feed.comments.one" : "feed.comments.other", {
												count: String(report.commentCount),
											})}
										</span>
									</span>
								</Link>
							</li>
						))}
					</ul>

					{(after || page.next) && (
						<nav aria-label={t("feed.pagesLabel")} className="mt-8 flex flex-wrap gap-3">
							{after && (
								<Link
									to="/reports"
									className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
								>
									{t("feed.newest")}
								</Link>
							)}
							{page.next && (
								<Link
									to={`/reports?after=${encodeURIComponent(page.next)}`}
									className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse"
								>
									{t("feed.next")}
								</Link>
							)}
						</nav>
					)}
				</>
			)}
		</main>
	)
}
