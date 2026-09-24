import { useEffect, useState } from "react"
import { Link, useParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import { getReport, type ReportAnswer, type ReportDetail } from "../api/adminReports"
import { ReportBadges } from "../components/ReportBadges"

/*
 * One report as a reviewer judges it: every question as it was asked, with
 * each private answer marked, and the bilingual summary pair with its
 * provenance. Read-only — editing, approving, rejecting, and publishing are the
 * second half of issue #25. Loading this page is an audited read.
 */
export function ReportDetailPage() {
	const { t, locale } = useLocale()
	const { reportId = "" } = useParams()
	const [report, setReport] = useState<ReportDetail | null>(null)
	const [error, setError] = useState<string | null>(null)

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
	}, [reportId, t])

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

			{!report && !error && <p className="mt-8 font-sans text-ink-muted">{t("reports.loading")}</p>}

			{report && (
				<>
					<div className="mt-4 flex flex-col gap-2">
						<ReportBadges status={report.status} consent={report.consent} isStuck={report.isStuck} />
						<p className="font-sans text-sm text-ink-muted">
							{t("reports.submittedAt", { at: at.format(new Date(report.submittedAt)) })}
						</p>
						<p className="font-sans text-sm text-ink-muted">
							{t(`reports.detail.language.${report.language}`)}
						</p>
						<p className="font-sans text-sm text-ink-muted">{t(`reports.detail.consent.${report.consent}`)}</p>
					</div>

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
										<p className="mt-2 whitespace-pre-line font-sans text-ink" data-summary="en">
											{report.summary.aiSummaryEn}
										</p>
									</article>
									<article lang="fr-CA" className="rounded border border-rule bg-surface p-4">
										<h3 className="font-sans text-xs uppercase tracking-wide text-ink-muted">
											{t("reports.detail.summaryFr")}
										</h3>
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
								<p className="mt-4 font-sans text-ink-muted">{t("reports.detail.noSummary")}</p>
							)
						)}
					</section>

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
												<span lang={value.locale} className="whitespace-pre-line">
													{value.value}
												</span>
												{value.translatedValue && (
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
									<li key={attachment.id} className="font-sans text-ink">
										{t("reports.detail.attachment", {
											kind: t(`reports.attachment.kind.${attachment.kind}`),
											state: t(`reports.attachment.state.${attachment.state}`),
										})}
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
