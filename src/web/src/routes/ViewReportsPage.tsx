import { useEffect, useRef, useState } from "react"
import { useSearchParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { fetchOwnReports, ownSummaryIn, type OwnReport } from "../api/ownReports"
import { fetchPublicReports, summaryIn, type PublicReport } from "../api/publicReports"
import { useInfiniteReportList } from "../hooks/useInfiniteReportList"
import { firstSectionPreview } from "../lib/markdownPreview"
import { ViewReportsPageView, type ViewReportsOwnRow, type ViewReportsRow } from "./ViewReportsPage.view"

/** How long to wait, after the visitor stops typing, before searching (ms). */
const SEARCH_DEBOUNCE_MS = 300

export function useViewReportsPage() {
	const { locale } = useLocale()
	const [searchParams, setSearchParams] = useSearchParams()
	const q = searchParams.get("q") ?? ""
	const [searchBox, setSearchBox] = useState(q)

	// The address bar is the source of truth; typing only debounces into it.
	useEffect(() => {
		setSearchBox(q)
	}, [q])

	const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

	function onSearchBoxChange(value: string) {
		setSearchBox(value)

		if (debounceRef.current) {
			clearTimeout(debounceRef.current)
		}

		debounceRef.current = setTimeout(() => {
			// Not `{ replace: true }`: each settled search commits its own
			// history entry, so the back button returns to whatever was on
			// screen before it, including the unfiltered feed (REQ-MOD-149).
			setSearchParams((current) => {
				const next = new URLSearchParams(current)
				if (value) {
					next.set("q", value)
				} else {
					next.delete("q")
				}
				return next
			})
		}, SEARCH_DEBOUNCE_MS)
	}

	useEffect(() => {
		return () => {
			if (debounceRef.current) {
				clearTimeout(debounceRef.current)
			}
		}
	}, [])

	const {
		items: reports,
		initialLoading: loading,
		loadingMore,
		failed,
		hasMore,
		loadMore,
		sentinelRef,
		announcement,
	} = useInfiniteReportList<PublicReport>({
		storageKey: `public:${q}`,
		getId: (report) => report.id,
		fetchPage: (after) => fetchPublicReports(after, q, locale),
	})

	// This browser's own reports that are not published yet, above the first page
	// of the plain feed only (issue no. 820, ADR-0196): never under a search, never
	// on a later page. Makes no request when the browser holds no receipt.
	const [own, setOwn] = useState<OwnReport[]>([])

	useEffect(() => {
		if (q) {
			setOwn([])
			return
		}

		let current = true
		void fetchOwnReports().then((reports) => {
			if (current) setOwn((previous) => (previous.length === 0 && reports.length === 0 ? previous : reports))
		})
		return () => {
			current = false
		}
	}, [q])

	const published = new Intl.DateTimeFormat(locale, { dateStyle: "long" })

	const ownRows: ViewReportsOwnRow[] = own.map((report) => {
		const summary = ownSummaryIn(report, locale)

		return {
			id: report.id,
			submitted: published.format(new Date(report.submittedAt)),
			forPublication: report.forPublication,
			preview: summary === null ? null : firstSectionPreview(summary),
			attachmentCount: report.attachmentCount,
		}
	})

	const rows: ViewReportsRow[] = reports.map((report) => ({
		id: report.id,
		published: published.format(new Date(report.publishedAt)),
		preview: firstSectionPreview(summaryIn(report, locale)),
		commentCount: report.commentCount,
		attachmentCount: report.attachmentCount,
	}))


	return { q, searchBox, onSearchBoxChange, ownRows, rows, loading, loadingMore, failed, hasMore, loadMore, sentinelRef, announcement }
}

/*
 * View safety reports: every published report, newest first, each linking to
 * its own address (/reports/<id>). The list loads more automatically as the
 * visitor nears its end (issue no. 572); the back button restores the same
 * accumulated results and scroll position rather than resetting to the first
 * page (REQ-MOD-079, REQ-MOD-082). A search box at the top fuzzy-searches the
 * published summary and visible member comments, in the visitor's current
 * site language, best match first; a blank box is the plain feed above,
 * unchanged. Its query lives in ?q=, bookmarkable the same way the page
 * cursor is (issue no. 574, REQ-MOD-149), and folds into the infinite-scroll
 * hook's storage key so a changed search never restores another search's — or
 * the plain feed's — accumulated list. Anonymous.
 *
 * A browser that filed a report and still holds its receipt sees that report at
 * the top of the first page, newest submitted first, marked not yet published
 * (or not for publication), before the public feed; every other visitor sees
 * nothing of it until it is published (issue no. 820, REQ-MOD-221).
 */
export function ViewReportsPage() {
	return <ViewReportsPageView {...useViewReportsPage()} />
}
