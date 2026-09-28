import { useEffect, useRef, useState } from "react"
import { Link, useSearchParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { fetchPublicReports, summaryIn, type PublicReport } from "../api/publicReports"
import { InfiniteScrollStatus } from "../components/InfiniteScrollStatus"
import { useInfiniteReportList } from "../hooks/useInfiniteReportList"

/** How long to wait, after the visitor stops typing, before searching (ms). */
const SEARCH_DEBOUNCE_MS = 300

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
 */
export function ViewReportsPage() {
	const { t, locale } = useLocale()
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

	const published = new Intl.DateTimeFormat(locale, { dateStyle: "long" })

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("nav.viewReports")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("feed.intro")}</p>

			<label htmlFor="feed-search" className="sr-only">
				{t("feed.search.label")}
			</label>
			<input
				id="feed-search"
				type="search"
				value={searchBox}
				onChange={(event) => onSearchBoxChange(event.target.value)}
				placeholder={t("feed.search.placeholder")}
				className="touch-target mt-6 w-full rounded border border-rule bg-surface px-4 font-sans text-ink placeholder:text-ink-muted"
			/>

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("feed.loading")}</p>
			) : failed && reports.length === 0 ? (
				<p role="alert" className="mt-8 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{t("feed.error")}
				</p>
			) : reports.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{q ? t("feed.search.empty") : t("feed.empty")}</p>
			) : (
				<>
					<ul aria-label={t("feed.listLabel")} className="mt-8 flex flex-col gap-4">
						{reports.map((report) => (
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

					<InfiniteScrollStatus
						hasMore={hasMore}
						loadingMore={loadingMore}
						failed={failed}
						onLoadMore={loadMore}
						sentinelRef={sentinelRef}
						announcement={announcement}
					/>
				</>
			)}
		</main>
	)
}
