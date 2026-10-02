import { useEffect, useRef } from "react"
import { DiscardReportDialogView, type DiscardReportDialogViewProps } from "./DiscardReportDialog.view"

/*
 * Confirms discarding the report in progress (issue no. 373,
 * `.spec/features/report-submission/README.md`'s "Discarding a report"). Focus
 * starts on the choice that keeps the report, and Escape keeps it too.
 */
export function useDiscardReportDialog() {
	const dialogRef = useRef<HTMLDialogElement>(null)
	const keepButtonRef = useRef<HTMLButtonElement>(null)

	useEffect(() => {
		const element = dialogRef.current
		if (element && !element.open) element.showModal()
		keepButtonRef.current?.focus()
	}, [])

	return { dialogRef, keepButtonRef }
}

export function DiscardReportDialog(props: Omit<DiscardReportDialogViewProps, "dialogRef" | "keepButtonRef">) {
	return <DiscardReportDialogView {...props} {...useDiscardReportDialog()} />
}
