import { useEffect, useState } from "react"
import { Link, useSearchParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import {
	deleteReport,
	isReportFilter,
	listReports,
	publishReport,
	REPORT_FILTERS,
	STALE_REPORT,
	unpublishReport,
	type ReportListItem,
} from "../api/adminReports"
import { DeleteReportDialog } from "../components/DeleteReportDialog"
import { InfiniteScrollStatus } from "../components/InfiniteScrollStatus"
import { ReportBadges } from "../components/ReportBadges"
import { ReportRowActions, type RowAction } from "../components/ReportRowActions"
import { useInfiniteReportList } from "../hooks/useInfiniteReportList"

/*
 * Every live report, newest first, with its workflow status, a Private badge
 * when the reporter refused publication, and a Stuck badge when it has waited
 * on summarization for more than a day. The filter lives in the address bar
 * so a reviewer can bookmark or share "needs action". Each row also shows the
 * reporter's and pilot's names when answered (REQ-MOD-124, ADR-0154) — the
 * list's one exception to carrying no other answer or summary text; opening a
 * report is the audited read.
 *
 * Each row can also be published, unpublished, or deleted in place
 * (REQ-MOD-120..123). A row carries its report's version, so these commands
 * need no detail read; deleting asks first, because nothing restores it.
 */
export function ManageReportsPage() {
	const { t, locale } = useLocale()
	const [searchParams, setSearchParams] = useSearchParams()
	const requested = searchParams.get("filter")
	const filter = isReportFilter(requested) ? requested : "all"
	const q = searchParams.get("q") ?? ""
	const [searchInput, setSearchInput] = useState(q)
	const [error, setError] = useState<string | null>(null)
	const [busyId, setBusyId] = useState<string | null>(null)
	const [stale, setStale] = useState(false)
	const [confirmingDelete, setConfirmingDelete] = useState<string | null>(null)

	const {
		items: reports,
		initialLoading: loading,
		loadingMore,
		failed: pageFailed,
		hasMore,
		loadMore,
		sentinelRef,
		announcement,
		mutate,
		reload: reloadList,
	} = useInfiniteReportList<ReportListItem>({
		// A changed search term starts a fresh accumulated list rather than
		// restoring a stale one, the same rule a changed filter already follows
		// (ADR-0155).
		storageKey: `admin:${filter}:${q}`,
		getId: (report) => report.id,
		fetchPage: (after) => listReports(filter, after, q),
	})

	// The address bar is the source of truth for the search text (REQ-MOD-136):
	// bookmarkable, and it survives back/reload. The input debounces before it
	// updates the address bar, so a keystroke does not fire a request or a
	// history entry on its own.
	useEffect(() => {
		setSearchInput(q)
	}, [q])

	useEffect(() => {
		const trimmed = searchInput.trim()
		if (trimmed === q) return
		const timer = window.setTimeout(() => {
			setSearchParams(
				(current) => {
					const next = new URLSearchParams(current)
					if (trimmed) next.set("q", trimmed)
					else next.delete("q")
					return next
				},
				{ replace: true },
			)
		}, 300)
		return () => window.clearTimeout(timer)
	}, [searchInput, q, setSearchParams])

	function fail(cause: unknown) {
		if (cause instanceof ApiError && cause.type === STALE_REPORT) setStale(true)
		else setError(cause instanceof ApiError ? cause.detail : t("reports.error.unexpected"))
	}

	async function act(report: ReportListItem, action: RowAction) {
		if (action === "delete") {
			setConfirmingDelete(report.id)
			return
		}
		setBusyId(report.id)
		setError(null)
		try {
			const updated =
				action === "publish"
					? await publishReport(report.id, report.version)
					: await unpublishReport(report.id, report.version, "")
			mutate((current) =>
				current.map((row) =>
					row.id === updated.id
						? { ...row, status: updated.status, consent: updated.consent, isStuck: updated.isStuck, version: updated.version }
						: row,
				),
			)
		} catch (cause) {
			fail(cause)
		} finally {
			setBusyId(null)
		}
	}

	async function remove(id: string) {
		setConfirmingDelete(null)
		setBusyId(id)
		setError(null)
		try {
			await deleteReport(id)
			mutate((current) => current.filter((row) => row.id !== id))
		} catch (cause) {
			fail(cause)
		} finally {
			setBusyId(null)
		}
	}

	function reload() {
		setStale(false)
		setError(null)
		reloadList()
	}

	const submitted = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })

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
						onChange={(event) => setSearchInput(event.target.value)}
						placeholder={t("reports.search.placeholder")}
						className="touch-target w-full rounded border border-rule bg-surface px-4 font-sans text-ink [&::-webkit-search-cancel-button]:appearance-none"
					/>
					{searchInput && (
						<button
							type="button"
							aria-label={t("reports.search.clear")}
							className="absolute inset-y-0 right-2 font-sans text-sm text-ink-muted"
							onClick={() => setSearchInput("")}
						>
							×
						</button>
					)}
				</div>
			</div>

			<nav aria-label={t("reports.filter.label")} className="mt-6">
				<ul className="flex flex-wrap gap-2">
					{REPORT_FILTERS.map((option) => (
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

			{confirmingDelete && (
				<DeleteReportDialog onConfirm={() => void remove(confirmingDelete)} onKeep={() => setConfirmingDelete(null)} />
			)}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("reports.loading")}</p>
			) : reports.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{q ? t("reports.searchEmpty", { q }) : t("reports.empty")}</p>
			) : (
				<ul aria-label={t("reports.listLabel")} className="mt-8 flex flex-col gap-3">
					{reports.map((report) => {
						const at = submitted.format(new Date(report.submittedAt))
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
									</span>
									<ReportBadges status={report.status} consent={report.consent} isStuck={report.isStuck} />
								</Link>
								<ReportRowActions
									report={report}
									label={t("reports.row.actions", { at })}
									busy={busyId === report.id}
									onAction={(action) => void act(report, action)}
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
