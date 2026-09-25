import { useCallback, useEffect, useState } from "react"
import { Link, useNavigate, useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import {
	attachmentLink,
	consentKey,
	setAttachmentHidden,
	deleteReport,
	getReport,
	publishReport,
	saveSummaryPair,
	STALE_REPORT,
	unpublishReport,
	type ReportAnswer,
	type ReportAttachment,
	type ReportDetail,
} from "../api/adminReports"
import { ReportBadges } from "../components/ReportBadges"
import { ReviewActions } from "../components/ReviewActions"
import { formatAnswer, isLanguageNeutral } from "../lib/formatAnswer"
import { DeleteReportDialog } from "../components/DeleteReportDialog"

/*
 * One report as a reviewer judges it: every question as it was asked, with
 * each private answer marked, the bilingual summary pair with its provenance,
 * and the actions its state allows (REQ-MOD-062). Every action sends the
 * version this page loaded; if another reviewer changed the report since, the
 * page says so and offers to reload rather than overwriting their work
 * (ADR-0105). Loading this page is an audited read.
 */
export function ReportDetailPage() {
	const { t, locale } = useLocale()
	const { reportId = "" } = useParams()
	const navigate = useNavigate()
	const [report, setReport] = useState<ReportDetail | null>(null)
	const [error, setError] = useState<string | null>(null)
	const [stale, setStale] = useState(false)
	const [busy, setBusy] = useState(false)
	const [confirmingDelete, setConfirmingDelete] = useState(false)
	const [reloads, setReloads] = useState(0)

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
			navigate("/admin/reports")
		} catch (cause) {
			setError(cause instanceof ApiError ? cause.detail : t("reports.error.unexpected"))
		} finally {
			setBusy(false)
		}
	}

	async function setHidden(attachment: ReportAttachment, hidden: boolean) {
		try {
			await setAttachmentHidden(reportId, attachment.id, hidden)
			setReport(await getReport(reportId))
		} catch (cause) {
			setError(cause instanceof ApiError ? cause.detail : t("reports.error.unexpected"))
		}
	}

	async function open(attachment: ReportAttachment) {
		try {
			const link = await attachmentLink(reportId, attachment)
			window.open(link.url, "_blank", "noopener")
		} catch (cause) {
			setError(cause instanceof ApiError ? cause.detail : t("reports.error.unexpected"))
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

	const at = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })
	const label = (answer: ReportAnswer) => (locale === "fr-CA" ? answer.labelFr : answer.labelEn)

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<Link to="/admin/reports" className="font-sans text-sm text-ink underline">
				{t("reports.detail.back")}
			</Link>
			<h1 className="mt-4 font-display text-3xl font-bold">{t("reports.detail.title")}</h1>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			{stale && (
				<div role="alert" className="mt-6 flex flex-wrap items-center gap-3 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					<p>{t("reports.stale.message")}</p>
					<button
						type="button"
						className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse"
						onClick={reload}
					>
						{t("reports.stale.reload")}
					</button>
				</div>
			)}

			{confirmingDelete && <DeleteReportDialog onConfirm={() => void remove()} onKeep={() => setConfirmingDelete(false)} />}

			{!report && !error && <p className="mt-8 font-sans text-ink-muted">{t("reports.loading")}</p>}

			{report && (
				<>
					<div className="mt-4 flex flex-col gap-2">
						<ReportBadges status={report.status} consent={report.consent} isStuck={report.isStuck} />
						{report.status === "published" && (
							<Link to={`/reports/${report.id}`} className="font-sans text-sm text-ink underline" data-public-link>
								{t("reports.detail.publicPage")}
							</Link>
						)}
						<p className="font-sans text-sm text-ink-muted">
							{t("reports.submittedAt", { at: at.format(new Date(report.submittedAt)) })}
						</p>
						<p className="font-sans text-sm text-ink-muted">
							{t(`reports.detail.language.${report.language}`)}
						</p>
						<p className="font-sans text-sm text-ink-muted">{t(`reports.detail.consent.${consentKey(report.consent)}`)}</p>
						{report.attachments.length > 0 && (
							<p className="font-sans text-sm text-ink-muted" data-media-consent>
								{t(`reports.detail.mediaConsent.${consentKey(report.mediaConsent)}`)}
							</p>
						)}
						{report.unpublishNote && (
							<p className="font-sans text-sm text-ink" data-unpublish-note>
								{t("reports.detail.unpublishNote", { note: report.unpublishNote })}
							</p>
						)}
					</div>

					<ReviewActions
						key={report.version}
						report={report}
						busy={busy}
						onSave={(en, fr, sourceEn, sourceFr) =>
							run((current) => saveSummaryPair(current.id, current.version, en, fr, sourceEn, sourceFr))
						}
						onPublish={() => void run((current) => publishReport(current.id, current.version))}
						onUnpublish={(note) => run((current) => unpublishReport(current.id, current.version, note))}
						onDelete={() => setConfirmingDelete(true)}
					/>

					{/* A report without consent is never summarized, so it has no summary panel (REQ-DOM-006). */}
					{report.consent === true && (
					<section aria-labelledby="summary-heading" className="mt-10">
						<h2 id="summary-heading" className="font-display text-2xl font-bold">
							{t("reports.detail.summary")}
						</h2>
						{report.summaryError && (
							<p className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
								{t("reports.detail.summaryFailed", { error: report.summaryError })}
							</p>
						)}
						{report.summary ? (
							<>
								<div className="mt-4 grid gap-4 md:grid-cols-2">
									<article lang="en-CA" className="rounded border border-rule bg-surface p-4">
										<h3 className="font-sans text-xs uppercase tracking-wide text-ink-muted">
											{t("reports.detail.summaryEn")}
										</h3>
										<p className="mt-1 font-sans text-xs text-ink-muted" data-source="en">
											{t(`reports.detail.source.${report.summary.sourceEn}`)}
										</p>
										<p className="mt-2 whitespace-pre-line font-sans text-ink" data-summary="en">
											{report.summary.aiSummaryEn}
										</p>
									</article>
									<article lang="fr-CA" className="rounded border border-rule bg-surface p-4">
										<h3 className="font-sans text-xs uppercase tracking-wide text-ink-muted">
											{t("reports.detail.summaryFr")}
										</h3>
										<p className="mt-1 font-sans text-xs text-ink-muted" data-source="fr">
											{t(`reports.detail.source.${report.summary.sourceFr}`)}
										</p>
										<p className="mt-2 whitespace-pre-line font-sans text-ink" data-summary="fr">
											{report.summary.aiSummaryFr}
										</p>
									</article>
								</div>
								<p className="mt-3 font-sans text-sm text-ink-muted" data-provenance>
									{t("reports.detail.provenance", {
										model: report.summary.model,
										prompt: report.summary.promptVersion,
										at: at.format(new Date(report.summary.generatedAt)),
									})}
								</p>
								<p className="font-sans text-sm text-ink-muted">
									{report.summary.approvedAt
										? t("reports.detail.approved", { at: at.format(new Date(report.summary.approvedAt)) })
										: t("reports.detail.notApproved")}
								</p>
							</>
						) : (
							!report.summaryError && (
								<p className="mt-4 font-sans text-ink-muted" data-no-summary>
									{t("reports.detail.noSummary")}
								</p>
							)
						)}
					</section>
					)}

					<section aria-labelledby="answers-heading" className="mt-10">
						<h2 id="answers-heading" className="font-display text-2xl font-bold">
							{t("reports.detail.answers")}
						</h2>
						<dl className="mt-4 flex flex-col gap-4">
							{report.answers.map((answer) => (
								<div
									key={answer.questionKey}
									className="rounded border border-rule bg-surface p-4"
									data-question-key={answer.questionKey}
								>
									<dt className="flex flex-wrap items-center gap-2 font-sans text-sm font-medium text-ink">
										{label(answer)}
										{answer.isPrivate && (
											<span
												className="inline-flex items-center rounded-full border border-ink px-2 py-0.5 font-sans text-xs"
												data-private-answer
											>
												{t("reports.detail.private")}
											</span>
										)}
									</dt>
									{answer.values.length === 0 ? (
										<dd className="mt-1 font-sans text-ink-muted">{t("reports.detail.notAnswered")}</dd>
									) : (
										answer.values.map((value, index) => (
											<dd key={index} className="mt-1 font-sans text-ink">
												{isLanguageNeutral(answer.type) ? (
													<span className="whitespace-pre-line">{formatAnswer(answer.type, value.value, locale, t)}</span>
												) : (
													<span lang={value.locale} className="whitespace-pre-line">
														{value.value}
													</span>
												)}
												{value.translatedValue && !isLanguageNeutral(answer.type) && (
													<span
														lang={value.locale === "fr-CA" ? "en-CA" : "fr-CA"}
														className="mt-1 block whitespace-pre-line text-sm text-ink-muted"
													>
														{t("reports.detail.translation", { text: value.translatedValue })}
													</span>
												)}
											</dd>
										))
									)}
								</div>
							))}
						</dl>
					</section>

					{report.attachments.length > 0 && (
						<section aria-labelledby="attachments-heading" className="mt-10">
							<h2 id="attachments-heading" className="font-display text-2xl font-bold">
								{t("reports.detail.attachments")}
							</h2>
							<ul className="mt-4 flex flex-col gap-2">
								{report.attachments.map((attachment) => (
									<li key={attachment.id} className="flex flex-wrap items-center gap-3 font-sans text-ink">
										{t("reports.detail.attachment", {
											kind: t(`reports.attachment.kind.${attachment.kind}`),
											state: t(`reports.attachment.state.${attachment.state}`),
										})}
										{attachment.state === "ready" && (
											<button
												type="button"
												className="touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
												onClick={() => void open(attachment)}
											>
												{t(attachment.kind === "document" ? "reports.attachment.download" : "reports.attachment.view")}
											</button>
										)}
										<span className="font-sans text-sm text-ink-muted" data-visibility={attachment.visibility}>
											{t(`reports.attachment.visibility.${attachment.visibility}`)}
										</span>
										{(attachment.visibility === "public" || attachment.visibility === "when_published") && (
											<button
												type="button"
												className="touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
												onClick={() => void setHidden(attachment, true)}
											>
												{t("reports.attachment.hide")}
											</button>
										)}
										{attachment.visibility === "hidden" && (
											<button
												type="button"
												className="touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
												onClick={() => void setHidden(attachment, false)}
											>
												{t("reports.attachment.show")}
											</button>
										)}
									</li>
								))}
							</ul>
						</section>
					)}
				</>
			)}
		</main>
	)
}
