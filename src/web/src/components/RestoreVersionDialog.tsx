import { useModalDialog } from "../hooks/useModalDialog"
import { RestoreVersionDialogView, type RestoreVersionDialogViewProps } from "./RestoreVersionDialog.view"

/*
 * Confirms restoring an earlier summary version (REQ-MOD-204). Restoring saves
 * a new version and changes what a published report shows at once, so it is
 * asked first. Nothing is lost either way — the history keeps every version —
 * but focus still starts on the choice that changes nothing, and Escape keeps
 * the current version too.
 */
export function useRestoreVersionDialog() {
	const { dialogRef, focusRef: keepButtonRef } = useModalDialog()

	return { dialogRef, keepButtonRef }
}

export function RestoreVersionDialog(props: Omit<RestoreVersionDialogViewProps, "dialogRef" | "keepButtonRef">) {
	return <RestoreVersionDialogView {...props} {...useRestoreVersionDialog()} />
}
