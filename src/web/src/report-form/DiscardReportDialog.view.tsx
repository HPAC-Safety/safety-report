import type { RefObject } from "react"

export interface DiscardReportDialogViewProps {
	onConfirm: () => void
	onKeep: () => void
	t: (key: string) => string
	dialogRef: RefObject<HTMLDialogElement>
	keepButtonRef: RefObject<HTMLButtonElement>
}

export function DiscardReportDialogView({ onConfirm, onKeep, t, dialogRef, keepButtonRef }: DiscardReportDialogViewProps) {
	return (
		<dialog
			ref={dialogRef}
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
					ref={keepButtonRef}
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
