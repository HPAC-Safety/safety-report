import { useEffect, useRef } from "react"
import { useLocale } from "../i18n/useLocale"

/*
 * Confirms soft-deleting a report (REQ-MOD-067). There is no restore
 * (REQ-DOM-007), so focus starts on the choice that keeps it, and Escape keeps
 * it too.
 */
export function DeleteReportDialog({ onConfirm, onKeep }: { onConfirm: () => void; onKeep: () => void }) {
	const { t } = useLocale()
	const dialog = useRef<HTMLDialogElement>(null)
	const keepButton = useRef<HTMLButtonElement>(null)

	useEffect(() => {
		const element = dialog.current
		if (element && !element.open) element.showModal()
		keepButton.current?.focus()
	}, [])

	return (
		<dialog
			ref={dialog}
			aria-labelledby="delete-report-title"
			aria-describedby="delete-report-body"
			onCancel={(event) => {
				event.preventDefault()
				onKeep()
			}}
			className="m-auto w-[calc(100%-2rem)] max-w-measure rounded border border-rule bg-surface p-6 text-ink backdrop:bg-black/40"
		>
			<h2 id="delete-report-title" className="font-display text-xl font-bold text-ink">
				{t("reports.delete.title")}
			</h2>
			<p id="delete-report-body" className="mt-2 font-sans text-sm text-ink-muted">
				{t("reports.delete.body")}
			</p>
			<div className="mt-4 flex justify-end gap-3">
				<button
					type="button"
					className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					onClick={onConfirm}
				>
					{t("reports.delete.confirm")}
				</button>
				<button
					ref={keepButton}
					type="button"
					className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
					onClick={onKeep}
				>
					{t("reports.delete.keep")}
				</button>
			</div>
		</dialog>
	)
}
