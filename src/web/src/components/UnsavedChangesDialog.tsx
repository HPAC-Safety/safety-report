import { useEffect, useRef } from "react"

/*
 * The bilingual confirm dialog `useUnsavedChangesGuard` shows for an in-app
 * route change away from a form with unsaved changes (issue #659). A browser
 * unload cannot carry custom text, so only this in-app path gets one. Focus
 * starts on the choice that keeps the form, and Escape keeps it too — the
 * same pattern as `DiscardReportDialog`.
 */
export function UnsavedChangesDialog({
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
			aria-labelledby="unsaved-changes-title"
			aria-describedby="unsaved-changes-body"
			onCancel={(event) => {
				event.preventDefault()
				onKeep()
			}}
			className="m-auto w-[calc(100%-2rem)] max-w-measure rounded border border-rule bg-surface p-6 text-ink backdrop:bg-black/40"
		>
			<h2 id="unsaved-changes-title" className="font-display text-xl font-bold text-ink">
				{t("unsavedChanges.title")}
			</h2>
			<p id="unsaved-changes-body" className="mt-2 font-sans text-sm text-ink-muted">
				{t("unsavedChanges.body")}
			</p>
			<div className="mt-4 flex justify-end gap-3">
				<button
					type="button"
					className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					onClick={onConfirm}
				>
					{t("unsavedChanges.leave")}
				</button>
				<button
					ref={keepButton}
					type="button"
					className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
					onClick={onKeep}
				>
					{t("unsavedChanges.stay")}
				</button>
			</div>
		</dialog>
	)
}
