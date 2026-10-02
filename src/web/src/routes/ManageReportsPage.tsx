import { useEffect, useState } from "react"
import { useSearchParams } from "react-router-dom"
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
import type { RowAction } from "../components/ReportRowActions"
import { useInfiniteReportList } from "../hooks/useInfiniteReportList"
import { ManageReportsPageView } from "./ManageReportsPage.view"

export type { ReportFilter, ReportListItem } from "../api/adminReports"

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
export function useManageReportsPage() {
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

	return {
		filters: REPORT_FILTERS,
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
		formatSubmitted: (value: string) => submitted.format(new Date(value)),
		changeSearch: setSearchInput,
		clearSearch: () => setSearchInput(""),
		reload,
		loadMore,
		act: (report: ReportListItem, action: RowAction) => void act(report, action),
		confirmDelete: (id: string) => void remove(id),
		keepReport: () => setConfirmingDelete(null),
	}
}

export function ManageReportsPage() {
	return <ManageReportsPageView {...useManageReportsPage()} />
}
