import { Fragment, useEffect, useRef, useState, type KeyboardEvent } from "react"

export interface TypeAheadChoice {
	key: string
	/** The wording the reader sees. */
	label: string
	/** The choice's one language, when a reporter added it in one language only. */
	lang: string | undefined
}

export interface TypeAheadFieldProps {
	fieldId: string
	/** The question's label text, which names the list for assistive technology. */
	label: string
	/** The choices in display order, pinned first, unpinned, pinned last (ADR-0136). */
	groups: TypeAheadChoice[][]
	value: string
	placeholder: string | undefined
	describedBy: string | undefined
	locale: string
	onChange: (value: string) => void
	t: (key: string) => string
}

/** Text folded for matching: accents and case do not count. */
function folded(text: string, locale: string): string {
	return text.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase(locale)
}

/**
 * A type-ahead question as a picker the form draws (REQ-QB-159, ADR-0140): a
 * text field with a caret, and a list directly beneath it that typing narrows.
 * The WAI-ARIA 1.2 combobox pattern with list autocomplete: focus stays in the
 * field, and the arrow keys move the active option. Any text may be typed; a
 * value the list does not offer is a reporter-added one (ADR-0129).
 */
export function TypeAheadField({ fieldId, label, groups, value, placeholder, describedBy, locale, onChange, t }: TypeAheadFieldProps) {
	const [open, setOpen] = useState(false)
	// What narrows the list: the text typed since it opened, or null for every choice.
	const [filter, setFilter] = useState<string | null>(null)
	const [active, setActive] = useState(-1)
	const containerRef = useRef<HTMLDivElement>(null)
	const inputRef = useRef<HTMLInputElement>(null)
	const listId = `${fieldId}-list`
	const optionId = (choice: TypeAheadChoice) => `${fieldId}-option-${choice.key}`

	const needle = filter ? folded(filter, locale) : ""
	const shown = groups
		.map((group) => (needle ? group.filter((choice) => folded(choice.label, locale).includes(needle)) : group))
		.filter((group) => group.length > 0)
	const flat = shown.flat()
	const expanded = open && flat.length > 0
	const activeChoice = expanded && active >= 0 ? flat[active] : undefined
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

	/** Opens the whole list, with the choice the field holds, if any, active. */
	function openAll(first: "none" | "first" | "last" = "none") {
		const all = groups.flat()
		const current = all.findIndex((choice) => choice.label === value)
		setFilter(null)
		setOpen(true)
		setActive(current >= 0 ? current : first === "first" ? 0 : first === "last" ? all.length - 1 : -1)
	}

	function close() {
		setOpen(false)
		setActive(-1)
	}

	function choose(choice: TypeAheadChoice) {
		onChange(choice.label)
		close()
		inputRef.current?.focus()
	}

	function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
		switch (event.key) {
			case "ArrowDown":
				event.preventDefault()
				if (!open) openAll(event.altKey ? "none" : "first")
				else if (!event.altKey) setActive((index) => Math.min(index + 1, flat.length - 1))
				break
			case "ArrowUp":
				event.preventDefault()
				if (!open) openAll("last")
				// With no choice active yet, Up starts from the end of the list.
				else setActive((index) => (index === -1 ? flat.length - 1 : Math.max(index - 1, 0)))
				break
			case "Enter":
				if (activeChoice) {
					event.preventDefault()
					choose(activeChoice)
				}
				break
			case "Escape":
				if (open) {
					event.preventDefault()
					event.stopPropagation()
					close()
				}
				break
			case "Tab":
				close()
				break
		}
	}

	return (
		<div
			ref={containerRef}
			className="relative mt-1"
			onBlur={(event) => {
				// Tabbing out closes the list; a pointer press outside is handled above.
				const next = event.relatedTarget as Node | null
				if (next && !containerRef.current?.contains(next)) close()
			}}
		>
			<input
				ref={inputRef}
				id={fieldId}
				type="text"
				role="combobox"
				autoComplete="off"
				aria-autocomplete="list"
				aria-expanded={expanded}
				aria-controls={listId}
				aria-activedescendant={activeId}
				aria-describedby={describedBy}
				className="w-full rounded border border-rule bg-surface py-2 pl-3 pr-11 font-sans text-ink placeholder:text-ink-muted"
				value={value}
				placeholder={placeholder}
				onClick={() => {
					if (!open) openAll()
				}}
				onChange={(event) => {
					onChange(event.target.value)
					setFilter(event.target.value)
					setOpen(true)
					setActive(-1)
				}}
				onKeyDown={onKeyDown}
			/>
			<button
				type="button"
				tabIndex={-1}
				aria-label={t("report.typeAhead.showChoices")}
				aria-controls={listId}
				aria-expanded={expanded}
				data-caret
				className="absolute inset-y-0 right-0 flex w-11 items-center justify-center text-ink"
				onClick={() => {
					if (open) close()
					else openAll()
					inputRef.current?.focus()
				}}
			>
				<svg aria-hidden="true" viewBox="0 0 20 20" className="h-4 w-4">
					<path d="M5.5 7.5 10 12l4.5-4.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
				</svg>
			</button>
			{/* The listbox is always in the page so aria-controls names it; it is hidden while closed. */}
			<ul
				id={listId}
				role="listbox"
				aria-label={label}
				hidden={!expanded}
				className="absolute left-0 right-0 top-full z-40 mt-1 max-h-72 overflow-y-auto rounded border border-rule bg-surface py-1 shadow-lg"
			>
				{shown.map((group, index) => (
					<Fragment key={group[0].key}>
						{index > 0 && <li role="presentation" aria-hidden="true" className="mx-3 my-1 border-t border-rule" data-separator />}
						{group.map((choice) => (
							<li
								key={choice.key}
								id={optionId(choice)}
								role="option"
								// A reporter-added choice may exist in one language only; it is
								// offered in that language, and says so to assistive technology.
								lang={choice.lang}
								aria-selected={choice === activeChoice}
								className={`touch-target flex cursor-pointer items-center px-3 font-sans text-ink hover:bg-surface-2 ${
									choice === activeChoice ? "bg-surface-3" : ""
								}`}
								// Keep focus in the field while a choice is pressed.
								onMouseDown={(event) => event.preventDefault()}
								onClick={() => choose(choice)}
							>
								{choice.label}
							</li>
						))}
					</Fragment>
				))}
			</ul>
			{open && flat.length === 0 && needle && (
				<p
					role="status"
					className="absolute left-0 right-0 top-full z-40 mt-1 rounded border border-rule bg-surface px-3 py-2 font-sans text-sm text-ink-muted shadow-lg"
				>
					{t("report.typeAhead.noMatches")}
				</p>
			)}
		</div>
	)
}
