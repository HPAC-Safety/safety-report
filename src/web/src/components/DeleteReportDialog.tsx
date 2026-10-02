import { useModalDialog } from "../hooks/useModalDialog"
import { DeleteReportDialogView, type DeleteReportDialogViewProps } from "./DeleteReportDialog.view"

/*
 * Confirms soft-deleting a report (REQ-MOD-067). There is no restore
 * (REQ-DOM-007), so focus starts on the choice that keeps it, and Escape keeps
 * it too.
 */
export function useDeleteReportDialog() {
	const { dialogRef, focusRef: keepButtonRef } = useModalDialog()

	return { dialogRef, keepButtonRef }
}

export function DeleteReportDialog(props: Omit<DeleteReportDialogViewProps, "dialogRef" | "keepButtonRef">) {
	return <DeleteReportDialogView {...props} {...useDeleteReportDialog()} />
}
