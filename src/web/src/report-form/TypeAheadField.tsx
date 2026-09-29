import { useEffect, useRef, useState, type KeyboardEvent } from "react"
import { ChoiceOptions, choiceListClassName, choiceRowClassName, type ListChoice } from "./ChoiceList"

/** A type-ahead shows no choices below this many typed characters, trimmed (ADR-0140, ADR-0152). */
export const TYPE_AHEAD_THRESHOLD = 3

/** One wording merged away into a choice (ADR-0129 amendment). */
export interface TypeAheadAlias {
	labelEn: string | null
	labelFr: string | null
}

export interface TypeAheadChoice {
	key: string
	/** The wording the reader sees. */
	label: string
	/** The choice's one language, when a reporter added it in one language only. */
	lang: string | undefined
	/**
	 * Every wording ever merged into this choice — never offered as a choice
	 * of its own — matched while typing in either language, whatever the
	 * form's language (ADR-0129 amendment).
	 */
	aliases?: TypeAheadAlias[]
}

export interface TypeAheadFieldProps {
	fieldId: string
	/** The question's label text, which names the list for assistive technology. */
	label: string
	/** The choices in display order, pinned first, unpinned, pinned last (ADR-0136). */
	groups: TypeAheadChoice[][]
	value: string
	/** The choice the field holds, when the reporter picked it from the list. */
	selectedKey: string | undefined
	placeholder: string | undefined
	describedBy: string | undefined
	locale: string
	/**
	 * The field's text, and the choice it names when the reporter picked one
	 * from the list. Typed text names no choice here (ADR-0129).
	 */
	onChange: (value: string, choiceKey?: string) => void
	t: (key: string, params?: Record<string, string | number>) => string
	/** True while the field cannot be answered yet: its parent question is unanswered (ADR-0146). */
	disabled?: boolean
}

/** Text folded for matching: accents and case do not count. */
function folded(text: string, locale: string): string {
	return text.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase(locale)
}

/**
 * A type-ahead question as a picker the form draws (REQ-QB-159, ADR-0140): a
 * plain text field, with no caret, and a list directly beneath it that typing
 * narrows. The WAI-ARIA 1.2 combobox pattern with list autocomplete: focus
 * stays in the field, and the arrow keys move the active option. Any text may
 * be typed; a value the list does not offer is a reporter-added one
 * (ADR-0129).
 *
 * Below three typed characters, trimmed, the open list shows a hint instead
 * of choices, so the field reads as a place to type rather than a dropdown to
 * pick from (ADR-0152). No option is active there, so Up, Down, and Enter do
 * nothing.
 */
export function TypeAheadField({
	fieldId,
	label,
	groups,
	value,
	selectedKey,
	placeholder,
	describedBy,
	locale,
	onChange,
	t,
	disabled = false,
}: TypeAheadFieldProps) {
	const [open, setOpen] = useState(false)
	// What narrows the list: the text typed since it opened, or null for every choice.
	const [filter, setFilter] = useState<string | null>(null)
	const [active, setActive] = useState(-1)
	const containerRef = useRef<HTMLDivElement>(null)
	const inputRef = useRef<HTMLInputElement>(null)
	const listId = `${fieldId}-list`
	const optionId = (choice: ListChoice) => `${fieldId}-option-${choice.key}`

	/**
	 * The alias wording of `choice` that contains `needle`, if any — checked in
	 * both languages regardless of the form's own language, since a merged
	 * value may have been worded in either one (ADR-0129 amendment).
	 */
	function matchedAlias(choice: TypeAheadChoice, needle: string): string | undefined {
		for (const alias of choice.aliases ?? []) {
			for (const wording of [alias.labelEn, alias.labelFr]) {
				if (wording && folded(wording, locale).includes(needle)) return wording
			}
		}
		return undefined
	}

	/**
	 * `groups`, narrowed to the choices whose own wording, or an alias of
	 * theirs, contains `needle` anywhere; every choice for an empty needle. A
	 * choice offered only through an alias carries a hint naming it, so the
	 * reporter sees why it matched (ADR-0129 amendment).
	 */
	function filterGroups(needle: string): ListChoice[][] {
		if (!needle) return groups

		return groups
			.map((group) =>
				group.flatMap((choice): ListChoice[] => {
					if (folded(choice.label, locale).includes(needle)) return [choice]

					const alias = matchedAlias(choice, needle)
					return alias ? [{ ...choice, hint: t("report.typeAhead.alsoKnownAs", { alias }) }] : []
				}),
			)
			.filter((group) => group.length > 0)
	}

	// Below the threshold, the list shows only the hint: no choices, no active option.
	const belowThreshold = value.trim().length < TYPE_AHEAD_THRESHOLD
	const needle = !belowThreshold && filter ? folded(filter, locale) : ""
	const shown = belowThreshold ? [] : filterGroups(needle)
	const flat = shown.flat()
	const expanded = open && (belowThreshold || flat.length > 0)
	const activeChoice = expanded && !belowThreshold && active >= 0 ? flat[active] : undefined
	const activeId = activeChoice ? optionId(activeChoice) : undefined
	const noMatch = open && !belowThreshold && flat.length === 0 && needle !== ""

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

	/**
	 * Opens the list, filtered by what the field already holds — exactly as
	 * typing it would (ADR-0152) — with the choice the field holds, if any,
	 * active.
	 */
	function openAll(first: "none" | "first" | "last" = "none") {
		const belowAfter = value.trim().length < TYPE_AHEAD_THRESHOLD
		const nextFlat = belowAfter ? [] : filterGroups(folded(value, locale)).flat()
		const current = nextFlat.findIndex((choice) => (selectedKey ? choice.key === selectedKey : choice.label === value))
		setFilter(value)
		setOpen(true)
		setActive(current >= 0 ? current : first === "first" ? 0 : first === "last" ? nextFlat.length - 1 : -1)
	}

	function close() {
		setOpen(false)
		setActive(-1)
	}

	function choose(choice: ListChoice) {
		onChange(choice.label, choice.key)
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
				className="w-full rounded border border-rule bg-surface py-2 px-3 font-sans text-ink placeholder:text-ink-muted disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-ink-muted"
				value={value}
				placeholder={placeholder}
				disabled={disabled}
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
			{/* The listbox is always in the page so aria-controls names it; it is hidden while closed. */}
			<ul id={listId} role="listbox" aria-label={label} hidden={!expanded} className={choiceListClassName}>
				{belowThreshold ? (
					// Presentational, like ChoiceSeparator: not an option, so Up, Down, and Enter pick nothing.
					<li role="presentation" data-hint="" className={`${choiceRowClassName} cursor-default text-ink-muted`}>
						{t("report.typeAhead.typeToSeeChoices")}
					</li>
				) : (
					<ChoiceOptions
						groups={shown}
						optionId={optionId}
						activeKey={activeChoice?.key}
						// The highlighted choice is the selected one: the combobox with list autocomplete.
						isSelected={(choice) => choice.key === activeChoice?.key}
						onPoint={(choice) => setActive(flat.findIndex((entry) => entry.key === choice.key))}
						onPick={choose}
					/>
				)}
			</ul>
			{/*
			 * Always in the page, so a change is announced through this polite live
			 * status the moment it happens. The hint is already visible as the
			 * list's own row, so this copy stays screen-reader only; "no match"
			 * has no visible row of its own, so it also draws the floating box.
			 */}
			<p
				role="status"
				className={
					noMatch
						? "absolute left-0 right-0 top-full z-40 mt-1 rounded border border-rule bg-surface px-3 py-2 font-sans text-sm text-ink-muted shadow-lg"
						: "sr-only"
				}
			>
				{noMatch ? t("report.typeAhead.noMatches") : expanded && belowThreshold ? t("report.typeAhead.typeToSeeChoices") : ""}
			</p>
		</div>
	)
}
