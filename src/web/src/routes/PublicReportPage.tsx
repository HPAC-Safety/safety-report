import { useEffect, useState } from "react"
import { Link, useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { ReportComments } from "../components/ReportComments"
import { ReportMedia } from "../components/ReportMedia"
import { fetchPublicReport, PublicReportNotFound, summaryIn, type PublicReportDetail } from "../api/publicReports"

type Loaded = { state: "loading" } | { state: "ready"; report: PublicReportDetail } | { state: "missing" } | { state: "failed" }

/*
 * One published report at its own address, /reports/<id>, which can be opened
 * directly, reloaded, and shared (REQ-MOD-079, REQ-MOD-080). The summary shows
 * in the site's language, which the header's language toggle chooses; the page
 * has no language control of its own (REQ-WLD-019). A report that is not public gets the same "not found" as one
 * that never existed (REQ-MOD-081). Its photos and video, when the reporter
 * agreed to share them, follow the summary (ADR-0117).
 */
export function PublicReportPage() {
	const { t, locale } = useLocale()
	const { reportId = "" } = useParams()
	const [loaded, setLoaded] = useState<Loaded>({ state: "loading" })

	useEffect(() => {
		let current = true
		setLoaded({ state: "loading" })
		fetchPublicReport(reportId)
			.then((report) => {
				if (current) {
					setLoaded({ state: "ready", report })
				}
			})
			.catch((cause: unknown) => {
				if (current) {
					setLoaded({ state: cause instanceof PublicReportNotFound ? "missing" : "failed" })
				}
			})
		return () => {
			current = false
		}
	}, [reportId])

	const published = new Intl.DateTimeFormat(locale, { dateStyle: "long" })

	return (
		<main className="mx-auto max-w-measure px-6 py-12">
			<Link to="/reports" className="font-sans text-sm text-ink underline">
				{t("feed.back")}
			</Link>

			{loaded.state === "loading" && <p className="mt-8 font-sans text-ink-muted">{t("feed.loadingOne")}</p>}

			{loaded.state === "failed" && (
				<p role="alert" className="mt-8 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{t("feed.errorOne")}
				</p>
			)}

			{loaded.state === "missing" && (
				<>
					<h1 className="mt-4 font-display text-3xl font-bold">{t("feed.notFound.title")}</h1>
					<p className="mt-4 font-sans text-ink-muted">{t("feed.notFound.body")}</p>
				</>
			)}

			{loaded.state === "ready" && (
				<article>
					<h1 className="mt-4 font-display text-3xl font-bold">{t("feed.reportTitle")}</h1>
					<p className="mt-2 font-sans text-sm text-ink-muted">
						{t("feed.publishedAt", { at: published.format(new Date(loaded.report.publishedAt)) })}
					</p>
					<p lang={locale} data-summary={locale} className="mt-6 whitespace-pre-line font-sans text-lg text-ink">
						{summaryIn(loaded.report, locale)}
					</p>
					<ReportMedia reportId={loaded.report.id} media={loaded.report.media} />
					<ReportComments reportId={loaded.report.id} />
				</article>
			)}
		</main>
	)
}
