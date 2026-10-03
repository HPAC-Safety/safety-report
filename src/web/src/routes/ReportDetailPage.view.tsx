import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import type { ReportAnswer, ReportConsent, ReportDetail } from "./ReportDetailPage"
import { AttachmentStrip } from "../components/AttachmentStrip"
import { DeleteReportDialog } from "../components/DeleteReportDialog"
import { Markdown } from "../components/Markdown"
import { PrivateAttachments, type PrivateAttachmentsState } from "../components/PrivateAttachments"
import { PrivateNotes } from "../components/PrivateNotes"
import { ReportBadges } from "../components/ReportBadges"
import { ReviewActions, type ReviewActionsProps } from "../components/ReviewActions"
import { SummaryHistory } from "../components/SummaryHistory"
import { formatAnswer, isLanguageNeutral } from "../lib/formatAnswer"
import { labelWithColon } from "../lib/questionPrompt"
import { listedValues } from "./reportAnswers"

export interface ReportDetailPageViewProps {
	report: ReportDetail | null
	error: string | null
	stale: boolean
	busy: boolean
	confirmingDelete: boolean
	privateAttachments: PrivateAttachmentsState
	ordinaryAnswers: ReportAnswer[]
	privateAnswers: ReportAnswer[]
	consentKey: (consent: ReportConsent) => "yes" | "no" | "unanswered"
	reload: () => void
	remove: () => void
	askDelete: () => void
	keepReport: () => void
	save: ReviewActionsProps["onSave"]
	publish: () => void
	unpublish: ReviewActionsProps["onUnpublish"]
	// Split across lines on purpose: tools/web/check-hardcoded-strings.ts is a
	// line scanner, and `=> Promise<…>` on one line reads to it as JSX text.
	restore: (revisionId: string) =>
		Promise<boolean>
}

export function ReportDetailPageView({
	report,
	error,
	stale,
	busy,
	confirmingDelete,
	privateAttachments,
	ordinaryAnswers,
	privateAnswers,
	consentKey,
	reload,
	remove,
	askDelete,
	keepReport,
	save,
	publish,
	unpublish,
	restore,
}: ReportDetailPageViewProps) {
	const { t, locale } = useLocale()
	const at = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })
	const label = (answer: ReportAnswer) => labelWithColon(locale === "fr-CA" ? answer.labelFr : answer.labelEn, answer.type, locale)

	const renderAnswer = (answer: ReportAnswer) => (

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
				listedValues(answer, locale).map((value, index) => (
					<dd key={index} className="mt-1 font-sans text-ink">
						{isLanguageNeutral(answer.type) ? (
							<span className="whitespace-pre-line">{formatAnswer(answer.type, value.value, locale, t)}</span>
						) : answer.type === "long_text" ? (
							// A paragraph answer, and its Worker translation, read as Markdown (ADR-0180).
							<Markdown lang={value.locale} headingOffset={2} data-long-text-answer="">
								{String(value.value)}
							</Markdown>
						) : (
							<span lang={value.locale} className="whitespace-pre-line">
								{value.value}
							</span>
						)}
						{value.translatedValue && !isLanguageNeutral(answer.type) && answer.type === "long_text" && (
							<div
								lang={value.locale === "fr-CA" ? "en-CA" : "fr-CA"}
								className="mt-2 border-l-2 border-rule pl-3 text-sm text-ink-muted"
								data-long-text-translation=""
							>
								<p className="font-medium">{t("reports.detail.translationLabel")}</p>
								<Markdown headingOffset={2} className="mt-1">
									{value.translatedValue}
								</Markdown>
							</div>
						)}
						{value.translatedValue && !isLanguageNeutral(answer.type) && answer.type !== "long_text" && (
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
	)

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

			{confirmingDelete && <DeleteReportDialog onConfirm={remove} onKeep={keepReport} />}

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
						onSave={save}
						onPublish={publish}
						onUnpublish={unpublish}
						onDelete={askDelete}
					/>

					{/* Processing failures and the approval state sit apart from the report's content (REQ-WLD-020). */}
					{report.consent === true && (report.summaryError || report.summary) && (
						<section aria-labelledby="status-heading" className="mt-10" data-section="status">
							<h2 id="status-heading" className="font-display text-2xl font-bold">
								{t("reports.detail.status")}
							</h2>
							{report.summaryError && (
								<p className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
									{t("reports.detail.summaryFailed", { error: report.summaryError })}
								</p>
							)}
							{report.summary && (
								<p className="mt-2 font-sans text-sm text-ink-muted" data-approval>
									{report.summary.approvedAt
										? t("reports.detail.approved", { at: at.format(new Date(report.summary.approvedAt)) })
										: t("reports.detail.notApproved")}
								</p>
							)}
						</section>
					)}

					{/* A report without consent is never summarized, so it has no summary panel (REQ-DOM-006). */}
					{report.consent === true && (
					<section aria-labelledby="summary-heading" className="mt-10" data-section="summary">
						<h2 id="summary-heading" className="font-display text-2xl font-bold">
							{t("reports.detail.summary")}
						</h2>
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
										<Markdown headingOffset={2} className="mt-2 font-sans text-ink" data-summary="en">
											{report.summary.aiSummaryEn}
										</Markdown>
									</article>
									<article lang="fr-CA" className="rounded border border-rule bg-surface p-4">
										<h3 className="font-sans text-xs uppercase tracking-wide text-ink-muted">
											{t("reports.detail.summaryFr")}
										</h3>
										<p className="mt-1 font-sans text-xs text-ink-muted" data-source="fr">
											{t(`reports.detail.source.${report.summary.sourceFr}`)}
										</p>
										<Markdown headingOffset={2} className="mt-2 font-sans text-ink" data-summary="fr">
											{report.summary.aiSummaryFr}
										</Markdown>
									</article>
								</div>
								<p className="mt-3 font-sans text-sm text-ink-muted" data-provenance>
									{t("reports.detail.provenance", {
										model: report.summary.model,
										prompt: report.summary.promptVersion,
										at: at.format(new Date(report.summary.generatedAt)),
									})}
								</p>
								<SummaryHistory
									revisions={report.summaryRevisions}
									isLive={report.status === "published"}
									canRestore={["pending", "published", "unpublished"].includes(report.status)}
									busy={busy}
									onRestore={restore}
								/>
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

					{ordinaryAnswers.length > 0 && (
						<section aria-labelledby="answers-heading" className="mt-10" data-section="answers">
							<h2 id="answers-heading" className="font-display text-2xl font-bold">
								{t("reports.detail.answers")}
							</h2>
							<dl className="mt-4 flex flex-col gap-4">{ordinaryAnswers.map(renderAnswer)}</dl>
						</section>
					)}

					{privateAnswers.length > 0 && (
						<section
							aria-labelledby="private-answers-heading"
							className="mt-10 rounded border-2 border-ink p-4"
							data-section="private-answers"
						>
							<h2 id="private-answers-heading" className="font-display text-2xl font-bold">
								{t("reports.detail.privateAnswers")}
							</h2>
							<dl className="mt-4 flex flex-col gap-4">{privateAnswers.map(renderAnswer)}</dl>
						</section>
					)}

					<AttachmentStrip reportId={report.id} media={[]} staffAttachments={report.attachments} onChanged={reload} />

					<PrivateAttachments reportId={report.id} state={privateAttachments} />
					<PrivateNotes reportId={report.id} attachments={privateAttachments.attachments ?? []} />
				</>
			)}
		</main>
	)
}
