import { useLocale } from "../i18n/useLocale"
import type { SummaryRevision } from "./SummaryHistory"
import { Markdown } from "./Markdown"
import { RestoreVersionDialog } from "./RestoreVersionDialog"

const SECONDARY =
	"touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2 disabled:opacity-50"

export interface SummaryHistoryViewProps {
	revisions: SummaryRevision[]
	isLive: boolean
	canRestore: boolean
	busy: boolean
	viewing: string | null
	restoring: SummaryRevision | null
	formatAt: (iso: string) => string
	author: (revision: SummaryRevision) => string
	onToggleView: (revisionId: string) => void
	onAskRestore: (revision: SummaryRevision) => void
	onKeep: () => void
	onConfirm: () => void
}

export function SummaryHistoryView({
	revisions,
	isLive,
	canRestore,
	busy,
	viewing,
	restoring,
	formatAt,
	author,
	onToggleView,
	onAskRestore,
	onKeep,
	onConfirm,
}: SummaryHistoryViewProps) {
	const { t } = useLocale()

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
								{t("reports.history.saved", { at: formatAt(revision.createdAt), author: author(revision) })}
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
										at: formatAt(revision.approvedAt),
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
									onClick={() => onToggleView(revision.id)}
								>
									{t(open ? "reports.history.hide" : "reports.history.view")}
								</button>
								{canRestore && !revision.isCurrent && (
									<button
										type="button"
										className={SECONDARY}
										disabled={busy}
										aria-describedby={`revision-${revision.sequence}`}
										onClick={() => onAskRestore(revision)}
									>
										{t("reports.history.restore")}
									</button>
								)}
							</div>
							{open && (
								<div id={`revision-text-${revision.sequence}`} className="mt-3 grid gap-3 md:grid-cols-2">
									<Markdown lang="en-CA" headingOffset={3} className="font-sans text-ink" data-revision-text="en">
										{revision.aiSummaryEn}
									</Markdown>
									<Markdown lang="fr-CA" headingOffset={3} className="font-sans text-ink" data-revision-text="fr">
										{revision.aiSummaryFr}
									</Markdown>
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
					onConfirm={onConfirm}
					onKeep={onKeep}
				/>
			)}
		</section>
	)
}
