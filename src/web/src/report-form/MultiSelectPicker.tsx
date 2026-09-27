import { Fragment, useEffect, useRef, useState, type ReactNode } from "react"
import { Caret, ChoiceSeparator, choiceListClassName, choiceRowClassName } from "./ChoiceList"

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
}

/**
 * A "Pick several" question as a picker dropdown (issue no. 343, REQ-SUB-034):
 * one closed trigger naming what is chosen, opening the type-ahead's list with a
 * checkbox on each row (REQ-QB-211, ADR-0150), which stays open while several
 * are checked. Escape closes it and returns focus to
 * the trigger; pressing outside or tabbing away closes it too.
 */
export function MultiSelectPicker({ fieldId, label, groups, values, placeholder, describedBy, onToggle }: MultiSelectPickerProps) {
	const [open, setOpen] = useState(false)
	const containerRef = useRef<HTMLDivElement>(null)
	const triggerRef = useRef<HTMLButtonElement>(null)
	const labelId = `${fieldId}-label`
	const summaryId = `${fieldId}-summary`
	const panelId = `${fieldId}-options`

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

	return (
		<div
			ref={containerRef}
			onBlur={(event) => {
				// Tabbing out closes the list; a pointer press outside is handled above.
				const next = event.relatedTarget as Node | null
				if (next && !containerRef.current?.contains(next)) setOpen(false)
			}}
		>
			<span id={labelId} className="block font-sans text-sm font-medium text-ink">
				{label}
			</span>
			<div className="relative">
				<button
					ref={triggerRef}
					id={fieldId}
					type="button"
					aria-expanded={open}
					aria-controls={panelId}
					aria-labelledby={`${labelId} ${summaryId}`}
					aria-describedby={describedBy}
					onClick={() => setOpen((value) => !value)}
					className="touch-target mt-1 flex w-full items-center justify-between gap-2 rounded border border-rule bg-surface px-3 py-2 text-left font-sans text-ink"
				>
					<span id={summaryId} className={chosen.length > 0 ? "truncate" : "truncate text-ink-muted"}>
						{chosen.length > 0 ? chosen.join(", ") : placeholder}
					</span>
					<Caret />
				</button>
				{open && (
					<ul id={panelId} role="group" aria-labelledby={labelId} className={choiceListClassName}>
						{groups.map((group, index) => (
							<Fragment key={group[0].key}>
								{index > 0 && <ChoiceSeparator />}
								{group.map((option) => (
									<li key={option.key} role="presentation">
										{/* The type-ahead's row and highlight (ChoiceList), on the row pointed at or holding keyboard focus. */}
										<label
											className={`${choiceRowClassName} gap-2 hover:bg-surface-4 hover:shadow-[inset_4px_0_0_var(--color-focus)] has-[:focus-visible]:bg-surface-4 has-[:focus-visible]:shadow-[inset_4px_0_0_var(--color-focus)]`}
										>
											<input type="checkbox" checked={values.includes(option.key)} onChange={() => onToggle(option.key)} />
											{option.label}
										</label>
									</li>
								))}
							</Fragment>
						))}
					</ul>
				)}
			</div>
		</div>
	)
}
