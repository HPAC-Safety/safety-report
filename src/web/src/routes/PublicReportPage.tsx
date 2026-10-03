import { useCallback, useEffect, useState } from "react"
import { useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import { fetchOwnReport, ownSummaryIn, receiptFor, type OwnReportDetail } from "../api/ownReports"
import { fetchPublicReport, PublicReportNotFound, summaryIn, type PublicReportDetail } from "../api/publicReports"
import { PublicReportPageView, type PublicReportLoaded } from "./PublicReportPage.view"

type Loaded =
	| { state: "loading" }
	| { state: "ready"; report: PublicReportDetail }
	| { state: "own"; report: OwnReportDetail; receipt: string }
	| { state: "missing" }
	| { state: "failed" }

export function usePublicReportPage() {
	const { locale } = useLocale()
	const { reportId = "" } = useParams()
	const { isSignedIn, role } = useAuth()
	const isReviewer = isSignedIn && role !== "user"
	const [loaded, setLoaded] = useState<Loaded>({ state: "loading" })

	const load = useCallback((showLoading: boolean) => {
		if (showLoading) setLoaded({ state: "loading" })

		// A browser that holds the receipt for this report asks for its own page
		// first; once the report is public, or gone, that answers nothing and the
		// public page is read as for anyone (issue no. 820, ADR-0196).
		const loadPublic = () =>
			fetchPublicReport(reportId)
				.then((report) => setLoaded({ state: "ready", report }))
				.catch((cause: unknown) => setLoaded({ state: cause instanceof PublicReportNotFound ? "missing" : "failed" }))

		const receipt = receiptFor(reportId)
		if (receipt === null) return loadPublic()

		return fetchOwnReport(reportId)
			.then((report) => (report === null ? loadPublic() : setLoaded({ state: "own", report, receipt })))
			.catch(() => setLoaded({ state: "failed" }))
	}, [reportId])

	useEffect(() => {
		void load(true)
	}, [load])

	const shown: PublicReportLoaded =
		loaded.state === "ready"
			? { state: "ready", report: loaded.report, summary: summaryIn(loaded.report, locale) }
			: loaded.state === "own"
				? { state: "own", report: loaded.report, receipt: loaded.receipt, summary: ownSummaryIn(loaded.report, locale) }
				: loaded

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
 * The browser that filed a report and still holds its receipt sees the report here
 * before it is published, with a pill and its latest summary as a draft (issue
 * no. 820, REQ-PUB-014).
 */
export function PublicReportPage() {
	return <PublicReportPageView {...usePublicReportPage()} />
}
