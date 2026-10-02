import { useEffect, useRef } from "react"
import { ResumeDraftDialogView, type ResumeDraftDialogViewProps } from "./ResumeDraftDialog.view"

export { clearedAnswerCount, savedAnswerRows, type SavedAnswerRow } from "./savedAnswerRows"

/*
 * Asks a returning reporter whether to continue the report this browser saved
 * (issue no. 344, `.spec/features/report-submission/README.md`'s "Returning to a saved
 * report"). Everything it shows comes from local storage — the dialog makes no
 * request of its own.
 */
export function useResumeDraftDialog() {
	const dialogRef = useRef<HTMLDialogElement>(null)
	const continueButtonRef = useRef<HTMLButtonElement>(null)

	// showModal() focuses the first control, which is the destructive one;
	// start on the choice that keeps the reporter's answers instead.
	useEffect(() => {
		const element = dialogRef.current
		if (element && !element.open) element.showModal()
		continueButtonRef.current?.focus()
	}, [])

	return { dialogRef, continueButtonRef }
}

export function ResumeDraftDialog(props: Omit<ResumeDraftDialogViewProps, "dialogRef" | "continueButtonRef">) {
	return <ResumeDraftDialogView {...props} {...useResumeDraftDialog()} />
}
