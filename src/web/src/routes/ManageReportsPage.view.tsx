import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import type { ReportFilter, ReportListItem } from "./ManageReportsPage"
import { AttachmentCountBadge } from "../components/AttachmentCountBadge"
import { DeleteReportDialog } from "../components/DeleteReportDialog"
import { InfiniteScrollStatus } from "../components/InfiniteScrollStatus"
import { ReportBadges } from "../components/ReportBadges"
import { ReportRowActions, type RowAction } from "../components/ReportRowActions"

export interface ManageReportsPageViewProps {
	filters: readonly ReportFilter[]
	filter: ReportFilter
	q: string
	searchInput: string
	error: string | null
	stale: boolean
	busyId: string | null
	confirmingDelete: string | null
	reports: ReportListItem[]
	loading: boolean
	loadingMore: boolean
	pageFailed: boolean
	hasMore: boolean
	sentinelRef: (node: Element | null) => void
	announcement: string
	formatSubmitted: (value: string) => string
	changeSearch: (value: string) => void
	clearSearch: () => void
	reload: () => void
	loadMore: () => void
	act: (report: ReportListItem, action: RowAction) => void
	confirmDelete: (id: string) => void
	keepReport: () => void
}

export function ManageReportsPageView({
	filters,
	filter,
	q,
	searchInput,
	error,
	stale,
	busyId,
	confirmingDelete,
	reports,
	loading,
	loadingMore,
	pageFailed,
	hasMore,
	sentinelRef,
	announcement,
	formatSubmitted,
	changeSearch,
	clearSearch,
	reload,
	loadMore,
	act,
	confirmDelete,
	keepReport,
}: ManageReportsPageViewProps) {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("nav.manageReports")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("reports.intro")}</p>

			<div className="mt-6">
				<label htmlFor="report-search" className="sr-only">
					{t("reports.search.label")}
				</label>
				<div className="relative">
					<input
						id="report-search"
						type="search"
						value={searchInput}
						onChange={(event) => changeSearch(event.target.value)}
						placeholder={t("reports.search.placeholder")}
						className="touch-target w-full rounded border border-rule bg-surface px-4 font-sans text-ink [&::-webkit-search-cancel-button]:appearance-none"
					/>
					{searchInput && (
						<button
							type="button"
							aria-label={t("reports.search.clear")}
							className="absolute inset-y-0 right-2 font-sans text-sm text-ink-muted"
							onClick={clearSearch}
						>
							×
						</button>
					)}
				</div>
			</div>

			<nav aria-label={t("reports.filter.label")} className="mt-6">
				<ul className="flex flex-wrap gap-2">
					{filters.map((option) => (
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

			{stale && (
				<div role="alert" className="mt-6 flex flex-wrap items-center gap-3 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					<p>{t("reports.row.stale")}</p>
					<button
						type="button"
						className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse"
						onClick={reload}
					>
						{t("reports.row.reload")}
					</button>
				</div>
			)}

			{confirmingDelete && <DeleteReportDialog onConfirm={() => confirmDelete(confirmingDelete)} onKeep={keepReport} />}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("reports.loading")}</p>
			) : reports.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{q ? t("reports.searchEmpty", { q }) : t("reports.empty")}</p>
			) : (
				<ul aria-label={t("reports.listLabel")} className="mt-8 flex flex-col gap-3">
					{reports.map((report) => {
						const at = formatSubmitted(report.submittedAt)
						return (
							<li
								key={report.id}
								data-report-id={report.id}
								className="flex items-center gap-2 rounded border border-rule bg-surface pr-3 hover:bg-surface-2"
							>
								<Link
									to={`/admin/reports/${report.id}`}
									className="flex min-w-0 flex-1 flex-col gap-2 p-4 sm:flex-row sm:items-center sm:justify-between"
								>
									<span className="flex flex-col gap-0.5">
										<span className="font-sans text-ink">{t("reports.submittedAt", { at })}</span>
										{(report.reporterName || report.pilotName) && (
											<span className="font-sans text-sm text-ink-muted">
												{report.reporterName && t("reports.row.reporterName", { name: report.reporterName })}
												{report.reporterName && report.pilotName && " · "}
												{report.pilotName && t("reports.row.pilotName", { name: report.pilotName })}
											</span>
										)}
										<AttachmentCountBadge count={report.attachmentCount} />
									</span>
									<ReportBadges status={report.status} consent={report.consent} isStuck={report.isStuck} />
								</Link>
								<ReportRowActions
									report={report}
									label={t("reports.row.actions", { at })}
									busy={busyId === report.id}
									onAction={(action) => act(report, action)}
								/>
							</li>
						)
					})}
				</ul>
			)}

			{!loading && reports.length > 0 && (
				<InfiniteScrollStatus
					hasMore={hasMore}
					loadingMore={loadingMore}
					failed={pageFailed}
					onLoadMore={loadMore}
					sentinelRef={sentinelRef}
					announcement={announcement}
				/>
			)}
		</main>
	)
}
