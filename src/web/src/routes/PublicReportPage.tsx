import { useCallback, useEffect, useState } from "react"
import { useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import { fetchPublicReport, PublicReportNotFound, summaryIn, type PublicReportDetail } from "../api/publicReports"
import { PublicReportPageView, type PublicReportLoaded } from "./PublicReportPage.view"

type Loaded = { state: "loading" } | { state: "ready"; report: PublicReportDetail } | { state: "missing" } | { state: "failed" }

export function usePublicReportPage() {
	const { locale } = useLocale()
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

	const shown: PublicReportLoaded =
		loaded.state === "ready" ? { state: "ready", report: loaded.report, summary: summaryIn(loaded.report, locale) } : loaded

	return { loaded: shown, isReviewer, onChanged: () => void load(false) }
}

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
	return <PublicReportPageView {...usePublicReportPage()} />
}
