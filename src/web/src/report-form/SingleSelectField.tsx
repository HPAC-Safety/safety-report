import { useEffect, useRef, useState, type KeyboardEvent, type MouseEvent } from "react"
import { Caret, ChoiceOptions, choiceListClassName, type ListChoice } from "./ChoiceList"

export interface SingleSelectFieldProps {
	fieldId: string
	/** The question's label text, which names the list for assistive technology. */
	label: string
	/** The choices in display order, pinned first, unpinned, pinned last (ADR-0136). */
	groups: ListChoice[][]
	/** The chosen choice's ID, or undefined while the question is unanswered. */
	selectedKey: string | undefined
	/** The first row, which the field shows while unanswered and which clears the answer when chosen. */
	placeholder: string
	describedBy: string | undefined
	locale: string
	/** The chosen choice's ID, or undefined when the reporter clears the answer. */
	onChange: (choiceKey: string | undefined) => void
	/** True while the field cannot be answered yet: its parent question is unanswered (ADR-0146). */
	disabled?: boolean
}

/** Text folded for matching: accents and case do not count. */
function folded(text: string, locale: string): string {
	return text.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase(locale)
}

/** How long typed characters keep adding to one search before a new one starts. */
const TYPE_TO_SELECT_MS = 500

/**
 * A single-select question as a picker the form draws (REQ-QB-208, ADR-0150):
 * a field with a caret, and the type-ahead's list directly beneath it. The
 * WAI-ARIA 1.2 select-only combobox pattern: focus stays on the field, the
 * arrow keys move the highlighted choice, and nothing can be typed; a
 * character only jumps to the next choice starting with it.
 */
export function SingleSelectField({
	fieldId,
	label,
	groups,
	selectedKey,
	placeholder,
	describedBy,
	locale,
	onChange,
	disabled = false,
}: SingleSelectFieldProps) {
	const [open, setOpen] = useState(false)
	const [active, setActive] = useState(0)
	const containerRef = useRef<HTMLDivElement>(null)
	const fieldRef = useRef<HTMLButtonElement>(null)
	const typed = useRef({ text: "", at: 0 })
	const listId = `${fieldId}-list`
	const optionId = (choice: ListChoice) => (choice.key ? `${fieldId}-option-${choice.key}` : `${fieldId}-option-none`)

	// The placeholder row comes first, and choosing it clears the answer.
	const none: ListChoice = { key: "", label: placeholder }
	const all = [none, ...groups.flat()]
	const chosen = selectedKey ? all.find((choice) => choice.key === selectedKey) : undefined
	const chosenIndex = chosen ? all.indexOf(chosen) : 0
	const activeChoice = open ? all[Math.min(active, all.length - 1)] : undefined
	const activeId = activeChoice ? optionId(activeChoice) : undefined

	useEffect(() => {
		if (!open) return

		function onPointerDown(event: PointerEvent) {
			if (!containerRef.current?.contains(event.target as Node)) setOpen(false)
		}

		document.addEventListener("pointerdown", onPointerDown)
		return () => document.removeEventListener("pointerdown", onPointerDown)
	}, [open])

	useEffect(() => {
		if (activeId) document.getElementById(activeId)?.scrollIntoView({ block: "nearest" })
	}, [activeId])

	// A disabled field cannot hold its list open.
	useEffect(() => {
		if (disabled) setOpen(false)
	}, [disabled])

	function openAt(index: number) {
		setActive(index)
		setOpen(true)
	}

	function choose(choice: ListChoice) {
		typed.current = { text: "", at: 0 }
		onChange(choice.key || undefined)
		setOpen(false)
		fieldRef.current?.focus()
	}

	/** The next choice after the highlighted one whose wording starts with what was typed; the placeholder is not a choice. */
	function typeToSelect(character: string) {
		const now = Date.now()
		const text = now - typed.current.at > TYPE_TO_SELECT_MS ? character : typed.current.text + character
		typed.current = { text, at: now }
		const needle = folded(text, locale)
		const from = open ? active : chosenIndex
		// A repeated single character cycles through the choices starting with it.
		const start = text.length === 1 ? from + 1 : from
		for (let step = 0; step < all.length; step++) {
			const index = (start + step) % all.length
			if (index > 0 && folded(all[index].label, locale).startsWith(needle)) {
				openAt(index)
				return
			}
		}
	}

	function onKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
		// While a search is being typed, Space adds to it rather than choosing.
		const searching = Date.now() - typed.current.at <= TYPE_TO_SELECT_MS && typed.current.text !== ""
		if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey && (event.key !== " " || searching)) {
			event.preventDefault()
			typeToSelect(event.key)
			return
		}

		// Enter and Space are the button's own: they arrive as a click, handled below.
		if (!open) {
			if (event.key === "ArrowDown" || event.key === "ArrowUp") {
				event.preventDefault()
				openAt(chosenIndex)
			} else if (event.key === "Home" || event.key === "End") {
				event.preventDefault()
				openAt(event.key === "Home" ? 0 : all.length - 1)
			}
			return
		}

		switch (event.key) {
			case "ArrowDown":
				event.preventDefault()
				if (!event.altKey) setActive((index) => Math.min(index + 1, all.length - 1))
				break
			case "ArrowUp":
				event.preventDefault()
				if (event.altKey && activeChoice) choose(activeChoice)
				else setActive((index) => Math.max(index - 1, 0))
				break
			case "Home":
				event.preventDefault()
				setActive(0)
				break
			case "End":
				event.preventDefault()
				setActive(all.length - 1)
				break
			case "PageDown":
				event.preventDefault()
				setActive((index) => Math.min(index + 10, all.length - 1))
				break
			case "PageUp":
				event.preventDefault()
				setActive((index) => Math.max(index - 10, 0))
				break
			case "Escape":
				event.preventDefault()
				event.stopPropagation()
				setOpen(false)
				break
			case "Tab":
				setOpen(false)
				break
		}
	}

	function onClick(event: MouseEvent<HTMLButtonElement>) {
		if (!open) {
			openAt(chosenIndex)
			return
		}
		// Enter or Space (a click with no pointer) takes the highlighted choice;
		// a pointer press on the field closes the list without changing it.
		if (event.detail === 0 && activeChoice) choose(activeChoice)
		else setOpen(false)
	}

	return (
		<div
			ref={containerRef}
			className="relative mt-1"
			onBlur={(event) => {
				// Tabbing out closes the list; a pointer press outside is handled above.
				const next = event.relatedTarget as Node | null
				if (next && !containerRef.current?.contains(next)) setOpen(false)
			}}
		>
			<button
				ref={fieldRef}
				id={fieldId}
				type="button"
				role="combobox"
				aria-haspopup="listbox"
				aria-expanded={open}
				aria-controls={listId}
				aria-activedescendant={activeId}
				aria-describedby={describedBy}
				disabled={disabled}
				className="relative block w-full rounded border border-rule bg-surface py-2 pl-3 pr-11 text-left font-sans text-ink disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-ink-muted"
				onClick={onClick}
				onKeyDown={onKeyDown}
			>
				<span className={chosen ? "block truncate" : "block truncate text-ink-muted"} lang={chosen?.lang}>
					{chosen ? chosen.label : placeholder}
				</span>
				<span className={`absolute inset-y-0 right-0 flex w-11 items-center justify-center ${disabled ? "opacity-40" : ""}`}>
					<Caret />
				</span>
			</button>
			{/* The listbox is always in the page so aria-controls names it; it is hidden while closed. */}
			<ul id={listId} role="listbox" aria-label={label} hidden={!open} className={choiceListClassName}>
				<ChoiceOptions
					groups={groups}
					leading={none}
					optionId={optionId}
					activeKey={activeChoice?.key}
					// The select-only pattern marks the chosen choice; the highlighted one is aria-activedescendant.
					isSelected={(choice) => choice.key === (chosen?.key ?? "")}
					onPoint={(choice) => setActive(all.findIndex((entry) => entry.key === choice.key))}
					onPick={choose}
				/>
			</ul>
		</div>
	)
}
