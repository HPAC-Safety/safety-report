import { useModalDialog } from "../hooks/useModalDialog"
import { DiscardReportDialogView, type DiscardReportDialogViewProps } from "./DiscardReportDialog.view"

/*
 * Confirms discarding the report in progress (issue no. 373,
 * `.spec/features/report-submission/README.md`'s "Discarding a report"). Focus
 * starts on the choice that keeps the report, and Escape keeps it too.
 */
export function useDiscardReportDialog() {
	const { dialogRef, focusRef: keepButtonRef } = useModalDialog()

	return { dialogRef, keepButtonRef }
}

export function DiscardReportDialog(props: Omit<DiscardReportDialogViewProps, "dialogRef" | "keepButtonRef">) {
	return <DiscardReportDialogView {...props} {...useDiscardReportDialog()} />
}
