import { useState } from "react"
import { useLocale } from "../i18n/useLocale"
import type { ReportDetail, ReportStatus } from "../api/adminReports"

/** What a reviewer may do in each state (REQ-MOD-062, ADR-0105). Delete is always offered. */
export type ReviewAction = "edit" | "write" | "approve" | "reject" | "reopen" | "unpublish" | "delete"

const ACTIONS: Record<ReportStatus, ReviewAction[]> = {
	pending_review: ["edit", "approve", "reject", "delete"],
	approved: ["edit", "delete"],
	published: ["edit", "unpublish", "delete"],
	rejected: ["reopen", "delete"],
	summary_failed: ["write", "delete"],
	submitted: ["delete"],
	summarizing: ["delete"],
}

const PRIMARY =
	"touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-50"
const SECONDARY =
	"touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink hover:bg-surface-2 disabled:opacity-50"
const FIELD = "mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"

/*
 * The action bar and the two inline forms it opens: the summary-pair editor
 * (both languages saved together) and the rejection note. The parent owns the
 * requests; this component owns only what the reviewer is typing.
 */
export function ReviewActions({
	report,
	busy,
	onSave,
	onApprove,
	onReject,
	onReopen,
	onUnpublish,
	onDelete,
}: {
	report: ReportDetail
	busy: boolean
	onSave: (aiSummaryEn: string, aiSummaryFr: string) =>
		Promise<boolean>
	onApprove: () => void
	onReject: (note: string) =>
		Promise<boolean>
	onReopen: () => void
	onUnpublish: () => void
	onDelete: () => void
}) {
	const { t } = useLocale()
	const [mode, setMode] = useState<"view" | "edit" | "reject">("view")
	const [draftEn, setDraftEn] = useState("")
	const [draftFr, setDraftFr] = useState("")
	const [note, setNote] = useState("")

	function openEditor() {
		setDraftEn(report.summary?.aiSummaryEn ?? "")
		setDraftFr(report.summary?.aiSummaryFr ?? "")
		setMode("edit")
	}

	async function save() {
		if (await onSave(draftEn, draftFr)) setMode("view")
	}

	async function reject() {
		if (await onReject(note)) {
			setNote("")
			setMode("view")
		}
	}

	if (mode === "edit") {
		return (
			<form
				aria-label={t("reports.edit.label")}
				className="mt-4 flex flex-col gap-4 rounded border border-rule bg-surface p-4"
				onSubmit={(event) => {
					event.preventDefault()
					void save()
				}}
			>
				<label className="block font-sans text-sm text-ink">
					{t("reports.edit.en")}
					<textarea lang="en-CA" rows={6} className={FIELD} value={draftEn} onChange={(e) => setDraftEn(e.target.value)} />
				</label>
				<label className="block font-sans text-sm text-ink">
					{t("reports.edit.fr")}
					<textarea lang="fr-CA" rows={6} className={FIELD} value={draftFr} onChange={(e) => setDraftFr(e.target.value)} />
				</label>
				<p className="font-sans text-sm text-ink-muted">{t("reports.edit.clearsApproval")}</p>
				<div className="flex flex-wrap gap-3">
					<button type="submit" className={PRIMARY} disabled={busy || !draftEn.trim() || !draftFr.trim()}>
						{t("reports.edit.save")}
					</button>
					<button type="button" className={SECONDARY} disabled={busy} onClick={() => setMode("view")}>
						{t("reports.action.cancel")}
					</button>
				</div>
			</form>
		)
	}

	if (mode === "reject") {
		return (
			<form
				aria-label={t("reports.reject.label")}
				className="mt-4 flex flex-col gap-4 rounded border border-rule bg-surface p-4"
				onSubmit={(event) => {
					event.preventDefault()
					void reject()
				}}
			>
				<label className="block font-sans text-sm text-ink">
					{t("reports.reject.note")}
					<textarea rows={3} maxLength={2000} className={FIELD} value={note} onChange={(e) => setNote(e.target.value)} />
				</label>
				<div className="flex flex-wrap gap-3">
					<button type="submit" className={PRIMARY} disabled={busy}>
						{t("reports.reject.confirm")}
					</button>
					<button type="button" className={SECONDARY} disabled={busy} onClick={() => setMode("view")}>
						{t("reports.action.cancel")}
					</button>
				</div>
			</form>
		)
	}

	// A report without consent is never summarized (REQ-DOM-006): there is no
	// pair to edit or approve, only a decision to reject or delete it.
	const actions =
		report.status === "pending_review" && !report.summary
			? ACTIONS.pending_review.filter((action) => action === "reject" || action === "delete")
			: ACTIONS[report.status]

	return (
		<div className="mt-4 flex flex-col gap-2">
			{actions.includes("approve") && (
				<p className="font-sans text-sm text-ink-muted">
					{t(report.consent === "yes" ? "reports.approve.publishes" : "reports.approve.private")}
				</p>
			)}
			<div role="group" aria-label={t("reports.action.label")} className="flex flex-wrap gap-3">
				{actions.includes("edit") && (
					<button type="button" className={SECONDARY} disabled={busy} onClick={openEditor}>
						{t("reports.action.edit")}
					</button>
				)}
				{actions.includes("write") && (
					<button type="button" className={PRIMARY} disabled={busy} onClick={openEditor}>
						{t("reports.action.write")}
					</button>
				)}
				{actions.includes("approve") && (
					<button type="button" className={PRIMARY} disabled={busy} onClick={onApprove}>
						{t("reports.action.approve")}
					</button>
				)}
				{actions.includes("reject") && (
					<button type="button" className={SECONDARY} disabled={busy} onClick={() => setMode("reject")}>
						{t("reports.action.reject")}
					</button>
				)}
				{actions.includes("reopen") && (
					<button type="button" className={PRIMARY} disabled={busy} onClick={onReopen}>
						{t("reports.action.reopen")}
					</button>
				)}
				{actions.includes("unpublish") && (
					<button type="button" className={SECONDARY} disabled={busy} onClick={onUnpublish}>
						{t("reports.action.unpublish")}
					</button>
				)}
				<button type="button" className={SECONDARY} disabled={busy} onClick={onDelete}>
					{t("reports.action.delete")}
				</button>
			</div>
		</div>
	)
}
