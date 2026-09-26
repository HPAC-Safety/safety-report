import { useState, type KeyboardEvent } from "react"

import { emailSuggestions } from "../lib/emailAddress"

export interface EmailFieldProps {
	fieldId: string
	className: string
	describedBy: string | undefined
	placeholder: string | undefined
	value: string
	onChange: (value: string) => void
	t: (key: string) => string
}

/**
 * An email question (REQ-SUB-085, REQ-SUB-092..095): the email keyboard, and a
 * list of suggested addresses below the field, as a combobox and its listbox.
 * Arrow keys move through the suggestions, Enter or a press chooses one, and
 * Escape closes the list. A suggestion never stops the reporter typing any
 * other address.
 */
export function EmailField({ fieldId, className, describedBy, placeholder, value, onChange, t }: EmailFieldProps) {
	const [focused, setFocused] = useState(false)
	const [dismissed, setDismissed] = useState(false)
	const [active, setActive] = useState(-1)
	const listId = `${fieldId}-suggestions`
	const suggestions = emailSuggestions(value)
	const showing = focused && !dismissed && suggestions.length > 0

	function type(next: string) {
		setDismissed(false)
		setActive(-1)
		onChange(next)
	}

	function choose(suggestion: string) {
		setActive(-1)
		onChange(suggestion)
	}

	function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
		if (event.key === "ArrowDown" || event.key === "ArrowUp") {
			if (suggestions.length === 0) return
			event.preventDefault()
			setDismissed(false)
			const down = event.key === "ArrowDown"
			setActive((current) => nextActive(current, down, suggestions.length))
			return
		}
		if (event.key === "Enter" && showing && active >= 0) {
			event.preventDefault()
			choose(suggestions[active])
			return
		}
		if (event.key === "Escape" && showing) {
			event.preventDefault()
			setDismissed(true)
			setActive(-1)
		}
	}

	return (
		<div className="relative">
			<input
				id={fieldId}
				type="email"
				inputMode="email"
				autoComplete="email"
				role="combobox"
				aria-autocomplete="list"
				aria-expanded={showing}
				aria-controls={listId}
				aria-activedescendant={showing && active >= 0 ? `${listId}-${active}` : undefined}
				className={className}
				value={value}
				aria-describedby={describedBy}
				placeholder={placeholder}
				onFocus={() => setFocused(true)}
				onBlur={() => setFocused(false)}
				onKeyDown={onKeyDown}
				onChange={(event) => type(event.target.value)}
			/>
			<ul
				id={listId}
				role="listbox"
				aria-label={t("report.email.suggestions")}
				hidden={!showing}
				className="mt-1 rounded border border-rule bg-surface py-1 font-sans text-ink"
			>
				{suggestions.map((suggestion, index) => (
					<li
						key={suggestion}
						id={`${listId}-${index}`}
						role="option"
						aria-selected={index === active}
						className={`touch-target flex cursor-pointer items-center px-3 ${index === active ? "bg-surface-2" : "hover:bg-surface-2"}`}
						// Chosen on press, before the field loses focus and the list closes.
						onMouseDown={(event) => {
							event.preventDefault()
							choose(suggestion)
						}}
					>
						{suggestion}
					</li>
				))}
			</ul>
		</div>
	)
}

/** The suggestion an arrow key moves to, wrapping at either end; from none, Down goes to the first and Up to the last. */
function nextActive(current: number, down: boolean, count: number): number {
	if (current === -1) return down ? 0 : count - 1
	return (current + (down ? 1 : -1) + count) % count
}
