import { useLocale } from "../i18n/useLocale"
import type { Language, ReviewActionsProps } from "./ReviewActions"
import type { ReviewAction } from "./reportReviewActions"
import { TranslateConfirmDialog } from "./TranslateConfirmDialog"

const PRIMARY =
	"touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-50"
const SECONDARY =
	"touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink hover:bg-surface-2 disabled:opacity-50"
const FIELD = "mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"

export type ReviewActionsViewProps = ReviewActionsProps & {
	mode: "view" | "edit" | "unpublish"
	draft: Record<Language, string>
	proposal: { target: Language; text: string } | null
	translating: boolean
	translateError: string | null
	note: string
	changedEn: boolean
	changedFr: boolean
	saveDisabled: boolean
	actions: ReviewAction[]
	openEditor: () => void
	openUnpublish: () => void
	cancel: () => void
	type: (language: Language, text: string) => void
	changeNote: (note: string) => void
	translateFrom: (language: Language) => void
	accept: (target: Language, text: string) => void
	keepCurrent: () => void
	submitEdit: () => void
	submitUnpublish: () => void
}

export function ReviewActionsView({
	report,
	busy,
	onPublish,
	onDelete,
	mode,
	draft,
	proposal,
	translating,
	translateError,
	note,
	changedEn,
	changedFr,
	saveDisabled,
	actions,
	openEditor,
	openUnpublish,
	cancel,
	type,
	changeNote,
	translateFrom,
	accept,
	keepCurrent,
	submitEdit,
	submitUnpublish,
}: ReviewActionsViewProps) {
	const { t } = useLocale()

	if (mode === "edit") {
		return (
			<form
				aria-label={t("reports.edit.label")}
				className="mt-4 flex flex-col gap-4 rounded border border-rule bg-surface p-4"
				onSubmit={(event) => {
					event.preventDefault()
					submitEdit()
				}}
			>
				<label className="block font-sans text-sm text-ink">
					{t("reports.edit.en")}
					<textarea lang="en-CA" rows={6} className={FIELD} value={draft.en} onChange={(e) => type("en", e.target.value)} />
				</label>
				<label className="block font-sans text-sm text-ink">
					{t("reports.edit.fr")}
					<textarea lang="fr-CA" rows={6} className={FIELD} value={draft.fr} onChange={(e) => type("fr", e.target.value)} />
				</label>
				{(changedEn || changedFr) && (
					<div role="group" aria-label={t("reports.translate.label")} className="flex flex-wrap gap-3">
						{changedEn && (
							<button type="button" className={SECONDARY} disabled={busy || translating} onClick={() => translateFrom("en")}>
								{t("reports.translate.toFrench")}
							</button>
						)}
						{changedFr && (
							<button type="button" className={SECONDARY} disabled={busy || translating} onClick={() => translateFrom("fr")}>
								{t("reports.translate.toEnglish")}
							</button>
						)}
					</div>
				)}
				{translateError && (
					<p role="alert" className="rounded border border-brand-700 bg-surface-2 p-3 font-sans text-sm text-ink">
						{translateError}
					</p>
				)}
				{proposal && (
					<TranslateConfirmDialog
						target={proposal.target}
						current={draft[proposal.target]}
						proposed={proposal.text}
						onAccept={() => accept(proposal.target, proposal.text)}
						onKeep={keepCurrent}
					/>
				)}
				<p className="font-sans text-sm text-ink-muted">
					{t(report.status === "published" ? "reports.edit.publishesAtOnce" : "reports.edit.savesDraft")}
				</p>
				<div className="flex flex-wrap gap-3">
					<button type="submit" className={PRIMARY} disabled={saveDisabled}>
						{t("reports.edit.save")}
					</button>
					<button type="button" className={SECONDARY} disabled={busy} onClick={cancel}>
						{t("reports.action.cancel")}
					</button>
				</div>
			</form>
		)
	}

	if (mode === "unpublish") {
		return (
			<form
				aria-label={t("reports.unpublish.label")}
				className="mt-4 flex flex-col gap-4 rounded border border-rule bg-surface p-4"
				onSubmit={(event) => {
					event.preventDefault()
					submitUnpublish()
				}}
			>
				<label className="block font-sans text-sm text-ink">
					{t("reports.unpublish.note")}
					<textarea rows={3} maxLength={2000} className={FIELD} value={note} onChange={(e) => changeNote(e.target.value)} />
				</label>
				<div className="flex flex-wrap gap-3">
					<button type="submit" className={PRIMARY} disabled={busy}>
						{t("reports.unpublish.confirm")}
					</button>
					<button type="button" className={SECONDARY} disabled={busy} onClick={cancel}>
						{t("reports.action.cancel")}
					</button>
				</div>
			</form>
		)
	}

	return (
		<div className="mt-4 flex flex-col gap-2">
			{actions.includes("publish") && <p className="font-sans text-sm text-ink-muted">{t("reports.publish.hint")}</p>}
			{report.consent !== true && <p className="font-sans text-sm text-ink-muted">{t("reports.private.hint")}</p>}
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
				{actions.includes("publish") && (
					<button type="button" className={PRIMARY} disabled={busy} onClick={onPublish}>
						{t("reports.action.publish")}
					</button>
				)}
				{actions.includes("unpublish") && (
					<button type="button" className={SECONDARY} disabled={busy} onClick={openUnpublish}>
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
