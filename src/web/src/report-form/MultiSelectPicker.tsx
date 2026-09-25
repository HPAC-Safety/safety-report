import { useEffect, useRef, useState, type ReactNode } from "react"

export interface MultiSelectPickerProps {
	fieldId: string
	/** The question's label content (text plus any required badge). */
	label: ReactNode
	/** The option labels in the reporter's locale, in display order. */
	options: { key: string; label: string }[]
	values: string[]
	placeholder: string
	describedBy: string | undefined
	onToggle: (key: string) => void
}

/**
 * A "Pick several" question as a picker dropdown (issue no. 343, REQ-SUB-034):
 * one closed trigger naming what is chosen, opening a list of checkboxes that
 * stays open while several are checked. Escape closes it and returns focus to
 * the trigger; pressing outside or tabbing away closes it too.
 */
export function MultiSelectPicker({ fieldId, label, options, values, placeholder, describedBy, onToggle }: MultiSelectPickerProps) {
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

	const chosen = options.filter((option) => values.includes(option.key)).map((option) => option.label)

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
					<svg aria-hidden="true" viewBox="0 0 20 20" className="h-4 w-4 shrink-0">
						<path d="M5.5 7.5 10 12l4.5-4.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
					</svg>
				</button>
				{open && (
					<div
						id={panelId}
						role="group"
						aria-labelledby={labelId}
						className="absolute left-0 right-0 top-full z-40 mt-1 flex max-h-72 flex-col gap-1 overflow-y-auto rounded border border-rule bg-surface py-2 shadow-lg"
					>
						{options.map((option) => (
							<label key={option.key} className="touch-target flex items-center gap-2 px-3 font-sans text-ink">
								<input type="checkbox" checked={values.includes(option.key)} onChange={() => onToggle(option.key)} />
								{option.label}
							</label>
						))}
					</div>
				)}
			</div>
		</div>
	)
}
