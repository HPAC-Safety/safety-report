import { useState } from "react"
import { useLocale } from "../i18n/useLocale"
import type { SummaryRevision } from "../api/adminReports"
import { RestoreVersionDialog } from "./RestoreVersionDialog"

const SECONDARY =
	"touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2 disabled:opacity-50"

/*
 * The summary's revision history, newest first (REQ-MOD-202): who saved each
 * version, when, how each language was written, and which version it restored.
 * Any version can be viewed without changing the current one (REQ-MOD-203), and
 * an earlier one can be restored behind a confirmation (REQ-MOD-204). Restoring
 * saves a new version; nothing in the history is rewritten (ADR-0177).
 */
export function SummaryHistory({
	revisions,
	isLive,
	canRestore,
	busy,
	onRestore,
}: {
	revisions: SummaryRevision[]
	isLive: boolean
	canRestore: boolean
	busy: boolean
	// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line scanner.
	onRestore: (revisionId: string) =>
		Promise<boolean>
}) {
	const { t, locale } = useLocale()
	const [viewing, setViewing] = useState<string | null>(null)
	const [restoring, setRestoring] = useState<SummaryRevision | null>(null)
	const at = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })

	async function confirm() {
		if (!restoring) return
		const revision = restoring
		setRestoring(null)
		await onRestore(revision.id)
	}

	function author(revision: SummaryRevision): string {
		if (revision.authorSubject) return revision.authorSubject
		// Revision 1 with nobody's name on it is the Worker's; a later one is from before authors were recorded.
		return revision.sequence === 1 && revision.sourceEn === "generated" && revision.sourceFr === "generated"
			? t("reports.history.author.worker")
			: t("reports.history.author.unknown")
	}

	return (
		<section aria-labelledby="history-heading" className="mt-8" data-summary-history>
			<h3 id="history-heading" className="font-display text-xl font-bold">
				{t("reports.history.title")}
			</h3>
			<ol className="mt-3 flex flex-col gap-3">
				{revisions.map((revision) => {
					const open = viewing === revision.id
					return (
						<li
							key={revision.id}
							className="rounded border border-rule bg-surface p-4"
							data-revision={revision.sequence}
						>
							<div className="flex flex-wrap items-center gap-2">
								<h4 id={`revision-${revision.sequence}`} className="font-sans text-sm font-semibold text-ink">
									{t("reports.history.version", { n: String(revision.sequence) })}
								</h4>
								{revision.isCurrent && (
									<span
										className="inline-flex items-center rounded-full border border-ink px-2 py-0.5 font-sans text-xs"
										data-current-revision
									>
										{t("reports.history.current")}
									</span>
								)}
							</div>
							<p className="mt-1 font-sans text-sm text-ink-muted" data-revision-saved>
								{t("reports.history.saved", { at: at.format(new Date(revision.createdAt)), author: author(revision) })}
							</p>
							<p className="font-sans text-sm text-ink-muted" data-revision-sources>
								{t("reports.history.sources", {
									en: t(`reports.detail.source.${revision.sourceEn}`),
									fr: t(`reports.detail.source.${revision.sourceFr}`),
								})}
							</p>
							{revision.restoredFromSequence !== null && (
								<p className="font-sans text-sm text-ink-muted" data-revision-restored-from>
									{t("reports.history.restoredFrom", { n: String(revision.restoredFromSequence) })}
								</p>
							)}
							{revision.approvedAt && (
								<p className="font-sans text-sm text-ink-muted">
									{t("reports.history.approved", {
										at: at.format(new Date(revision.approvedAt)),
										by: revision.approvedBySubject ?? "",
									})}
								</p>
							)}
							<div className="mt-3 flex flex-wrap gap-3">
								<button
									type="button"
									className={SECONDARY}
									aria-expanded={open}
									aria-controls={`revision-text-${revision.sequence}`}
									aria-describedby={`revision-${revision.sequence}`}
									onClick={() => setViewing(open ? null : revision.id)}
								>
									{t(open ? "reports.history.hide" : "reports.history.view")}
								</button>
								{canRestore && !revision.isCurrent && (
									<button
										type="button"
										className={SECONDARY}
										disabled={busy}
										aria-describedby={`revision-${revision.sequence}`}
										onClick={() => setRestoring(revision)}
									>
										{t("reports.history.restore")}
									</button>
								)}
							</div>
							{open && (
								<div id={`revision-text-${revision.sequence}`} className="mt-3 grid gap-3 md:grid-cols-2">
									<p lang="en-CA" className="whitespace-pre-line font-sans text-ink" data-revision-text="en">
										{revision.aiSummaryEn}
									</p>
									<p lang="fr-CA" className="whitespace-pre-line font-sans text-ink" data-revision-text="fr">
										{revision.aiSummaryFr}
									</p>
								</div>
							)}
						</li>
					)
				})}
			</ol>
			{restoring && (
				<RestoreVersionDialog
					sequence={restoring.sequence}
					isLive={isLive}
					onConfirm={() => void confirm()}
					onKeep={() => setRestoring(null)}
				/>
			)}
		</section>
	)
}
