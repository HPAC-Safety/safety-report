import { useCallback, useEffect, useState } from "react"
import { useNavigate, useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import {
	consentKey,
	deleteReport,
	getReport,
	publishReport,
	rollBackSummary,
	saveSummaryPair,
	STALE_REPORT,
	unpublishReport,
	type ReportDetail,
	type SummarySource,
} from "../api/adminReports"
import { usePrivateAttachments } from "../components/PrivateAttachments"
import { ReportDetailPageView } from "./ReportDetailPage.view"

export type { ReportAnswer, ReportConsent, ReportDetail } from "../api/adminReports"

/*
 * One report as a reviewer judges it: every question as it was asked, with
 * each private answer marked, the bilingual summary pair with its provenance,
 * and the actions its state allows (REQ-MOD-062). Every action sends the
 * version this page loaded; if another reviewer changed the report since, the
 * page says so and offers to reload rather than overwriting their work
 * (ADR-0105). Loading this page is an audited read.
 */
export function useReportDetailPage() {
	const { t } = useLocale()
	const { reportId = "" } = useParams()
	const navigate = useNavigate()
	const [report, setReport] = useState<ReportDetail | null>(null)
	const [error, setError] = useState<string | null>(null)
	const [stale, setStale] = useState(false)
	const [busy, setBusy] = useState(false)
	const [confirmingDelete, setConfirmingDelete] = useState(false)
	const [reloads, setReloads] = useState(0)
	const privateAttachments = usePrivateAttachments(reportId)

	const reload = useCallback(() => {
		setStale(false)
		setError(null)
		setReloads((count) => count + 1)
	}, [])

	/** Runs one review command and shows its result; false when it was refused. */
	async function run(
		command: (current: ReportDetail) =>
			Promise<ReportDetail>,
	): Promise<boolean> {
		if (!report) return false
		setBusy(true)
		setError(null)
		try {
			setReport(await command(report))
			return true
		} catch (cause) {
			if (cause instanceof ApiError && cause.type === STALE_REPORT) {
				setStale(true)
			} else {
				setError(cause instanceof ApiError ? cause.detail : t("reports.error.unexpected"))
			}
			return false
		} finally {
			setBusy(false)
		}
	}

	async function remove() {
		setConfirmingDelete(false)
		setBusy(true)
		try {
			await deleteReport(reportId)
			void navigate("/admin/reports")
		} catch (cause) {
			setError(cause instanceof ApiError ? cause.detail : t("reports.error.unexpected"))
		} finally {
			setBusy(false)
		}
	}

	useEffect(() => {
		let current = true
		getReport(reportId)
			.then((loaded) => {
				if (current) {
					setReport(loaded)
				}
			})
			.catch((cause: unknown) => {
				if (current) {
					setError(
						cause instanceof ApiError && cause.status === 404
							? t("reports.detail.notFound")
							: cause instanceof ApiError
								? cause.detail
								: t("reports.error.unexpected"),
					)
				}
			})
		return () => {
			current = false
		}
	}, [reportId, t, reloads])

	return {
		report,
		error,
		stale,
		busy,
		confirmingDelete,
		privateAttachments,
		privateAnswers: report?.answers.filter((answer) => answer.isPrivate) ?? [],
		ordinaryAnswers: report?.answers.filter((answer) => !answer.isPrivate) ?? [],
		consentKey,
		reload,
		remove: () => void remove(),
		askDelete: () => setConfirmingDelete(true),
		keepReport: () => setConfirmingDelete(false),
		save: (en: string, fr: string, sourceEn: SummarySource, sourceFr: SummarySource) =>
			run((current) => saveSummaryPair(current.id, current.version, en, fr, sourceEn, sourceFr)),
		publish: () => void run((current) => publishReport(current.id, current.version)),
		unpublish: (note: string) => run((current) => unpublishReport(current.id, current.version, note)),
		restore: (revisionId: string) => run((current) => rollBackSummary(current.id, current.version, revisionId)),
	}
}

export function ReportDetailPage() {
	return <ReportDetailPageView {...useReportDetailPage()} />
}
