import { useEffect, useRef } from "react"

/**
 * Opens a native `<dialog>` as a modal when it mounts, and starts focus on the
 * control `focusRef` is attached to (ADR-0188). `showModal()` focuses the first
 * control, which for a confirmation is often the destructive one; a dialog
 * that wants focus elsewhere passes the ref of the choice that keeps things
 * as they are. A dialog with no such control leaves `focusRef` unattached and
 * keeps the browser's own choice.
 */
export function useModalDialog<Focus extends HTMLElement = HTMLButtonElement>() {
	const dialogRef = useRef<HTMLDialogElement>(null)
	const focusRef = useRef<Focus>(null)

	useEffect(() => {
		const element = dialogRef.current
		if (element && !element.open) element.showModal()
		focusRef.current?.focus()
	}, [])

	return { dialogRef, focusRef }
}
