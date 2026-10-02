import { useState, type ChangeEvent, type KeyboardEvent } from "react"

import { emailSuggestions } from "../lib/emailAddress"
import { EmailFieldView } from "./EmailField.view"

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
 * The view model of an email question (REQ-SUB-085, REQ-SUB-092..095): the
 * email keyboard, and a list of suggested addresses below the field, as a
 * combobox and its listbox. Arrow keys move through the suggestions, Enter or a
 * press chooses one, and Escape closes the list. A suggestion never stops the
 * reporter typing any other address.
 */
export function useEmailField({ fieldId, value, onChange }: EmailFieldProps) {
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

	return {
		listId,
		suggestions,
		showing,
		active,
		onFocus: () => setFocused(true),
		onBlur: () => setFocused(false),
		onKeyDown,
		onInputChange: (event: ChangeEvent<HTMLInputElement>) => type(event.target.value),
		onChoose: choose,
	}
}

export type EmailFieldModel = ReturnType<typeof useEmailField>

/** An email question; the logic is `useEmailField`. */
export function EmailField(props: EmailFieldProps) {
	return <EmailFieldView {...props} {...useEmailField(props)} />
}

/** The suggestion an arrow key moves to, wrapping at either end; from none, Down goes to the first and Up to the last. */
function nextActive(current: number, down: boolean, count: number): number {
	if (current === -1) return down ? 0 : count - 1
	return (current + (down ? 1 : -1) + count) % count
}
