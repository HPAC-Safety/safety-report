import { useCallback, useEffect, useState } from "react"
import { Link, useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { Markdown } from "../components/Markdown"
import { useAuth } from "../auth/useAuth"
import { AttachmentStrip } from "../components/AttachmentStrip"
import { ReportComments } from "../components/ReportComments"
import { fetchPublicReport, PublicReportNotFound, summaryIn, type PublicReportDetail } from "../api/publicReports"

type Loaded = { state: "loading" } | { state: "ready"; report: PublicReportDetail } | { state: "missing" } | { state: "failed" }

/*
 * One published report at its own address, /reports/<id>, which can be opened
 * directly, reloaded, and shared (REQ-MOD-079, REQ-MOD-080). The summary shows
 * in the site's language, which the header's language toggle chooses; the page
 * has no language control of its own (REQ-WLD-019). A report that is not public gets the same "not found" as one
 * that never existed (REQ-MOD-081). Its photos and video, when the reporter
 * agreed to share them, follow the summary (ADR-0117). A signed-in
 * Administrator or Safety Officer sees a same-tab link to this report's admin
 * detail page, next to the published date; the public payload carries nothing
 * for it — only the token's role decides (REQ-MOD-164, REQ-MOD-165). When the
 * report was written in the other official language than the one showing, a
 * muted label says the summary was translated from it; it follows the header's
 * language toggle and is absent when the two match (REQ-MOD-190..192, ADR-0176).
 */
export function PublicReportPage() {
	const { t, locale } = useLocale()
	const { reportId = "" } = useParams()
	const { isSignedIn, role } = useAuth()
	const isReviewer = isSignedIn && role !== "user"
	const [loaded, setLoaded] = useState<Loaded>({ state: "loading" })

	const load = useCallback((showLoading: boolean) => {
		if (showLoading) setLoaded({ state: "loading" })
		return fetchPublicReport(reportId)
			.then((report) => setLoaded({ state: "ready", report }))
			.catch((cause: unknown) => setLoaded({ state: cause instanceof PublicReportNotFound ? "missing" : "failed" }))
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [reportId])

	useEffect(() => {
		void load(true)
	}, [load])

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
					{loaded.report.language !== locale && (
						<p className="mt-1 font-sans text-sm text-ink-muted" data-translated-from={loaded.report.language}>
							{t(`feed.translatedFrom.${loaded.report.language}`)}
						</p>
					)}
					{isReviewer && (
						<Link
							to={`/admin/reports/${loaded.report.id}`}
							className="mt-1 inline-block font-sans text-sm text-ink underline"
							data-admin-link
						>
							{t("feed.adminPage")}
						</Link>
					)}
					<Markdown lang={locale} data-summary={locale} className="mt-6 font-sans text-ink-muted">
						{summaryIn(loaded.report, locale)}
					</Markdown>
					<AttachmentStrip
						reportId={loaded.report.id}
						media={loaded.report.media}
						staffAttachments={loaded.report.staffAttachments}
						onChanged={() => void load(false)}
					/>
					<ReportComments reportId={loaded.report.id} />
				</article>
			)}
		</main>
	)
}
