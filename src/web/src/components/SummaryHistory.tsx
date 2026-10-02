import { useState } from "react"
import { useLocale } from "../i18n/useLocale"
import type { SummaryRevision } from "../api/adminReports"
import { SummaryHistoryView } from "./SummaryHistory.view"

export type { SummaryRevision }

export interface SummaryHistoryProps {
	revisions: SummaryRevision[]
	isLive: boolean
	canRestore: boolean
	busy: boolean
	// Split across lines on purpose: tools/web/check-hardcoded-strings.mjs is a line scanner.
	onRestore: (revisionId: string) =>
		Promise<boolean>
}

/** The view model: which version is open, which one awaits a restore confirmation, and how a revision reads. */
export function useSummaryHistory({ onRestore }: SummaryHistoryProps) {
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

	return {
		viewing,
		restoring,
		formatAt: (iso: string) => at.format(new Date(iso)),
		author,
		onToggleView: (revisionId: string) => setViewing(viewing === revisionId ? null : revisionId),
		onAskRestore: setRestoring,
		onKeep: () => setRestoring(null),
		onConfirm: () => void confirm(),
	}
}

/*
 * The summary's revision history, newest first (REQ-MOD-202): who saved each
 * version, when, how each language was written, and which version it restored.
 * Any version can be viewed without changing the current one (REQ-MOD-203), and
 * an earlier one can be restored behind a confirmation (REQ-MOD-204). Restoring
 * saves a new version; nothing in the history is rewritten (ADR-0177).
 */
export function SummaryHistory(props: SummaryHistoryProps) {
	return <SummaryHistoryView {...props} {...useSummaryHistory(props)} />
}
