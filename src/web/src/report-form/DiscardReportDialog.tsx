import { useEffect, useRef } from "react"

/*
 * Confirms discarding the report in progress (issue no. 373,
 * `features/report-submission/README.md`'s "Discarding a report"). Focus
 * starts on the choice that keeps the report, and Escape keeps it too.
 */
export function DiscardReportDialog({
	onConfirm,
	onKeep,
	t,
}: {
	onConfirm: () => void
	onKeep: () => void
	t: (key: string) => string
}) {
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
			aria-labelledby="discard-report-title"
			aria-describedby="discard-report-body"
			onCancel={(event) => {
				event.preventDefault()
				onKeep()
			}}
			className="m-auto w-[calc(100%-2rem)] max-w-measure rounded border border-rule bg-surface p-6 text-ink backdrop:bg-black/40"
		>
			<h2 id="discard-report-title" className="font-display text-xl font-bold text-ink">
				{t("report.discard.title")}
			</h2>
			<p id="discard-report-body" className="mt-2 font-sans text-sm text-ink-muted">
				{t("report.discard.body")}
			</p>

			<div className="mt-4 flex justify-end gap-3">
				<button
					type="button"
					className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					onClick={onConfirm}
				>
					{t("report.discard.confirm")}
				</button>
				<button
					ref={keepButton}
					type="button"
					className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
					onClick={onKeep}
				>
					{t("report.discard.keep")}
				</button>
			</div>
		</dialog>
	)
}
