import { useEffect, useState } from "react"
import { Link, useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { ReportComments } from "../components/ReportComments"
import { fetchPublicReport, PublicReportNotFound, summaryIn, type PublicReport } from "../api/publicReports"

type Loaded = { state: "loading" } | { state: "ready"; report: PublicReport } | { state: "missing" } | { state: "failed" }

/*
 * One published report at its own address, /reports/<id>, which can be opened
 * directly, reloaded, and shared (REQ-MOD-079, REQ-MOD-080). The summary shows
 * first in the visitor's language, and they can switch to the other one
 * (REQ-WLD-019). A report that is not public gets the same "not found" as one
 * that never existed (REQ-MOD-081).
 */
export function PublicReportPage() {
	const { t, locale } = useLocale()
	const { reportId = "" } = useParams()
	const [loaded, setLoaded] = useState<Loaded>({ state: "loading" })
	// Remembered against the locale it was chosen in, so changing the site's
	// language shows that language's text first again.
	const [otherChosenIn, setOtherChosenIn] = useState<string | null>(null)

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

	const showingOther = otherChosenIn === locale
	const other = locale === "fr-CA" ? "en-CA" : "fr-CA"
	const shown = showingOther ? other : locale
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
					<p lang={shown} data-summary={shown} className="mt-6 whitespace-pre-line font-sans text-lg text-ink">
						{summaryIn(loaded.report, shown)}
					</p>
					<button
						type="button"
						onClick={() => setOtherChosenIn(showingOther ? null : locale)}
						className="touch-target mt-6 inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					>
						{t(showingOther ? `feed.readIn.${locale}` : `feed.readIn.${other}`)}
					</button>
					<ReportComments reportId={loaded.report.id} />
				</article>
			)}
		</main>
	)
}
