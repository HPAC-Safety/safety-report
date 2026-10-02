import { useEffect, useRef } from "react"
import { wordDiff } from "../lib/wordDiff"
import { TranslateConfirmDialogView } from "./TranslateConfirmDialog.view"

/*
 * Asks before a machine translation replaces a summary language, showing the
 * current text and the proposed one with their differences marked (REQ-MOD-072,
 * ADR-0108). Focus starts on keeping the current text, and Escape keeps it too.
 */
export interface TranslateConfirmDialogProps {
	target: "en" | "fr"
	current: string
	proposed: string
	onAccept: () => void
	onKeep: () => void
}

export function useTranslateConfirmDialog({ current, proposed }: TranslateConfirmDialogProps) {
	const dialogRef = useRef<HTMLDialogElement>(null)
	const keepButtonRef = useRef<HTMLButtonElement>(null)
	const parts = wordDiff(current, proposed)
	const currentParts = parts.filter((part) => part.kind !== "added")
	const proposedParts = parts.filter((part) => part.kind !== "removed")

	useEffect(() => {
		const element = dialogRef.current
		if (element && !element.open) element.showModal()
		keepButtonRef.current?.focus()
	}, [])

	return { dialogRef, keepButtonRef, currentParts, proposedParts }
}

export function TranslateConfirmDialog(props: TranslateConfirmDialogProps) {
	return <TranslateConfirmDialogView {...props} {...useTranslateConfirmDialog(props)} />
}
