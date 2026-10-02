import type { RefObject } from "react"
import type { UnsavedChangesCopy } from "../hooks/useUnsavedChangesGuard"

export interface UnsavedChangesDialogViewProps {
	onConfirm: () => void
	onKeep: () => void
	t: (key: string) => string
	copy?: UnsavedChangesCopy
	dialogRef: RefObject<HTMLDialogElement>
	keepButtonRef: RefObject<HTMLButtonElement>
}

export function UnsavedChangesDialogView({ onConfirm, onKeep, t, copy, dialogRef, keepButtonRef }: UnsavedChangesDialogViewProps) {
	return (
		<dialog
			ref={dialogRef}
			aria-labelledby="unsaved-changes-title"
			aria-describedby="unsaved-changes-body"
			onCancel={(event) => {
				event.preventDefault()
				onKeep()
			}}
			className="m-auto w-[calc(100%-2rem)] max-w-measure rounded border border-rule bg-surface p-6 text-ink backdrop:bg-black/40"
		>
			<h2 id="unsaved-changes-title" className="font-display text-xl font-bold text-ink">
				{copy?.title ?? t("unsavedChanges.title")}
			</h2>
			<p id="unsaved-changes-body" className="mt-2 font-sans text-sm text-ink-muted">
				{copy?.body ?? t("unsavedChanges.body")}
			</p>
			<div className="mt-4 flex justify-end gap-3">
				<button
					type="button"
					className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					onClick={onConfirm}
				>
					{copy?.leave ?? t("unsavedChanges.leave")}
				</button>
				<button
					ref={keepButtonRef}
					type="button"
					className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
					onClick={onKeep}
				>
					{copy?.stay ?? t("unsavedChanges.stay")}
				</button>
			</div>
		</dialog>
	)
}
