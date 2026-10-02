import { useEffect, useRef, useState, type FocusEvent, type ReactNode } from "react"
import { MultiSelectPickerView } from "./MultiSelectPicker.view"

export interface MultiSelectPickerProps {
	fieldId: string
	/** The question's label content (text plus any required badge). */
	label: ReactNode
	/**
	 * The option labels in the reporter's locale, in display order: pinned
	 * first, unpinned, pinned last, with a divider drawn between groups (ADR-0136).
	 */
	groups: { key: string; label: string }[][]
	values: string[]
	placeholder: string
	describedBy: string | undefined
	onToggle: (key: string) => void
	/** Options shown but not toggled: a review page keeps a value's last parent choice ticked (ADR-0151). */
	locked?: string[]
	/** Why a locked option cannot be toggled, read by a screen reader on that option. */
	lockedReason?: string
	/** Marks the trigger invalid, as a choice row offered under nothing is (ADR-0151). */
	invalid?: boolean
}

/**
 * The view model of a "Pick several" question as a picker dropdown (issue no.
 * 343, REQ-SUB-034): one closed trigger naming what is chosen, opening the
 * type-ahead's list with a checkbox on each row (REQ-QB-211, ADR-0150), which
 * stays open while several are checked. Escape closes it and returns focus to
 * the trigger; pressing outside or tabbing away closes it too.
 */
export function useMultiSelectPicker({
	fieldId,
	groups,
	values,
	locked = [],
	invalid = false,
}: MultiSelectPickerProps) {
	const [open, setOpen] = useState(false)
	const containerRef = useRef<HTMLDivElement>(null)
	const triggerRef = useRef<HTMLButtonElement>(null)
	const labelId = `${fieldId}-label`
	const summaryId = `${fieldId}-summary`
	const panelId = `${fieldId}-options`
	const lockedReasonId = `${fieldId}-locked-reason`

	useEffect(() => {
		if (!open) return

		function onKeyDown(event: KeyboardEvent) {
			if (event.key === "Escape") {
				setOpen(false)
				triggerRef.current?.focus()
			}
		}

		function onPointerDown(event: PointerEvent) {
			if (!containerRef.current?.contains(event.target as Node)) {
				setOpen(false)
			}
		}

		document.addEventListener("keydown", onKeyDown)
		document.addEventListener("pointerdown", onPointerDown)
		return () => {
			document.removeEventListener("keydown", onKeyDown)
			document.removeEventListener("pointerdown", onPointerDown)
		}
	}, [open])

	const chosen = groups
		.flat()
		.filter((option) => values.includes(option.key))
		.map((option) => option.label)

	function onBlur(event: FocusEvent<HTMLDivElement>) {
		// Tabbing out closes the list; a pointer press outside is handled above.
		const next = event.relatedTarget as Node | null
		if (next && !containerRef.current?.contains(next)) setOpen(false)
	}

	return {
		open,
		locked,
		invalid,
		containerRef,
		triggerRef,
		labelId,
		summaryId,
		panelId,
		lockedReasonId,
		chosen,
		onBlur,
		onToggleOpen: () => setOpen((value) => !value),
	}
}

export type MultiSelectPickerModel = ReturnType<typeof useMultiSelectPicker>

/** A "Pick several" question as a picker dropdown; the logic is `useMultiSelectPicker`. */
export function MultiSelectPicker(props: MultiSelectPickerProps) {
	return <MultiSelectPickerView {...props} {...useMultiSelectPicker(props)} />
}
