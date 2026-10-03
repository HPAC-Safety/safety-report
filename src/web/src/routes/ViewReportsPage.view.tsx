import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { AttachmentCountBadge } from "../components/AttachmentCountBadge"
import { InfiniteScrollStatus } from "../components/InfiniteScrollStatus"
import { OwnReportPill } from "../components/OwnReportPill"

/** One feed row, ready to show: its date and preview are already in the site's language. */
export interface ViewReportsRow {
	id: string
	published: string
	preview: string
	commentCount: number
	attachmentCount: number
}

/** One of the visitor's own, not yet published reports, ready to show. */
export interface ViewReportsOwnRow {
	id: string
	submitted: string
	forPublication: boolean
	/** Null before the Worker has made a summary, and always for a report not for publication. */
	preview: string | null
	attachmentCount: number
}

export interface ViewReportsPageViewProps {
	q: string
	searchBox: string
	onSearchBoxChange: (value: string) => void
	ownRows: ViewReportsOwnRow[]
	rows: ViewReportsRow[]
	loading: boolean
	loadingMore: boolean
	failed: boolean
	hasMore: boolean
	loadMore: () => void
	sentinelRef: (node: Element | null) => void
	announcement: string
}

export function ViewReportsPageView({
	q,
	searchBox,
	onSearchBoxChange,
	ownRows,
	rows,
	loading,
	loadingMore,
	failed,
	hasMore,
	loadMore,
	sentinelRef,
	announcement,
}: ViewReportsPageViewProps) {
	const { t } = useLocale()

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

			{ownRows.length > 0 && (
				<ul aria-label={t("feed.own.listLabel")} data-own-reports className="mt-8 flex flex-col gap-4">
					{ownRows.map((report) => (
						<li key={report.id} data-own-report-id={report.id}>
							<Link
								to={`/reports/${report.id}`}
								className="flex flex-col gap-2 rounded border border-ink bg-surface p-5 hover:bg-surface-2"
							>
								<span className="flex flex-wrap items-center gap-x-3 gap-y-1">
									<OwnReportPill forPublication={report.forPublication} />
									<span className="font-sans text-sm text-ink-muted">{t("feed.own.submittedAt", { at: report.submitted })}</span>
								</span>
								{report.forPublication &&
									(report.preview === null ? (
										<span className="font-sans text-ink-muted">{t("feed.own.summaryPending")}</span>
									) : (
										<>
											<span className="line-clamp-3 whitespace-pre-line font-sans text-ink">{report.preview}</span>
											<span className="font-sans text-sm text-ink-muted">{t("feed.own.draftNote")}</span>
										</>
									))}
								<span className="flex flex-wrap items-center gap-x-4 font-sans text-sm">
									<span className="font-medium text-brand-700 underline">{t("feed.read")}</span>
									<AttachmentCountBadge count={report.attachmentCount} />
								</span>
							</Link>
						</li>
					))}
				</ul>
			)}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("feed.loading")}</p>
			) : failed && rows.length === 0 ? (
				<p role="alert" className="mt-8 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{t("feed.error")}
				</p>
			) : rows.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{q ? t("feed.search.empty") : t("feed.empty")}</p>
			) : (
				<>
					<ul aria-label={t("feed.listLabel")} className="mt-8 flex flex-col gap-4">
						{rows.map((report) => (
							<li key={report.id} data-report-id={report.id}>
								<Link
									to={`/reports/${report.id}`}
									className="flex flex-col gap-2 rounded border border-rule bg-surface p-5 hover:bg-surface-2"
								>
									<span className="font-sans text-sm text-ink-muted">
										{t("feed.publishedAt", { at: report.published })}
									</span>
									<span className="line-clamp-3 whitespace-pre-line font-sans text-ink">{report.preview}</span>
									<span className="flex flex-wrap items-center gap-x-4 font-sans text-sm">
										<span className="font-medium text-brand-700 underline">{t("feed.read")}</span>
										<span data-comment-count={report.commentCount} className="text-ink-muted">
											{t(report.commentCount === 1 ? "feed.comments.one" : "feed.comments.other", {
												count: String(report.commentCount),
											})}
										</span>
										<AttachmentCountBadge count={report.attachmentCount} />
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
