import { useEffect, useRef } from "react"
import { UnsavedChangesDialogView, type UnsavedChangesDialogViewProps } from "./UnsavedChangesDialog.view"

/*
 * The bilingual confirm dialog `useUnsavedChangesGuard` shows for an in-app
 * route change away from a form with unsaved changes (issue no. 659). A browser
 * unload cannot carry custom text, so only this in-app path gets one. A form
 * may pass its own wording (`copy`); the report form does (issue no. 748). Focus
 * starts on the choice that keeps the form, and Escape keeps it too — the
 * same pattern as `DiscardReportDialog`.
 */
export function useUnsavedChangesDialog() {
	const dialogRef = useRef<HTMLDialogElement>(null)
	const keepButtonRef = useRef<HTMLButtonElement>(null)

	useEffect(() => {
		const element = dialogRef.current
		if (element && !element.open) element.showModal()
		keepButtonRef.current?.focus()
	}, [])

	return { dialogRef, keepButtonRef }
}

export function UnsavedChangesDialog(props: Omit<UnsavedChangesDialogViewProps, "dialogRef" | "keepButtonRef">) {
	return <UnsavedChangesDialogView {...props} {...useUnsavedChangesDialog()} />
}
