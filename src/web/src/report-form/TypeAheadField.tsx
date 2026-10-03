import { useEffect, useRef, useState, type ChangeEvent, type FocusEvent, type KeyboardEvent, type MouseEvent } from "react"
import type { ListChoice } from "./ChoiceList"
import { optionRowId } from "./optionRowId"
import { TypeAheadFieldView } from "./TypeAheadField.view"

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
 * The view model of a type-ahead question as a picker the form draws
 * (REQ-QB-159, ADR-0140): a plain text field, with no caret, and a list
 * directly beneath it that typing narrows. The WAI-ARIA 1.2 combobox pattern
 * with list autocomplete: focus stays in the field, and the arrow keys move the
 * active option. Any text may be typed; a value the list does not offer is a
 * reporter-added one (ADR-0129).
 *
 * Below three typed characters, trimmed, the open list shows a hint instead
 * of choices, so the field reads as a place to type rather than a dropdown to
 * pick from (ADR-0152). No option is active there, so Up, Down, and Enter do
 * nothing.
 */
export function useTypeAheadField({
	fieldId,
	groups,
	value,
	selectedKey,
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

	function onBlur(event: FocusEvent<HTMLDivElement>) {
		// Tabbing out closes the list; a pointer press outside is handled above.
		const next = event.relatedTarget as Node | null
		if (next && !containerRef.current?.contains(next)) close()
	}

	function onClick() {
		if (!open) openAll()
	}

	function onInputChange(event: ChangeEvent<HTMLInputElement>) {
		onChange(event.target.value)
		setFilter(event.target.value)
		setOpen(true)
		setActive(-1)
	}

	return {
		disabled,
		containerRef,
		inputRef,
		listId,
		optionId,
		expanded,
		belowThreshold,
		noMatch,
		shown,
		activeId,
		activeKey: activeChoice?.key,
		onBlur,
		onClick,
		onInputChange,
		onKeyDown,
		// The highlighted choice is the selected one: the combobox with list autocomplete.
		isSelected: (choice: ListChoice) => choice.key === activeChoice?.key,
		onPoint: (choice: ListChoice) => setActive(flat.findIndex((entry) => entry.key === choice.key)),
		onListClick: (event: MouseEvent<HTMLElement>) => {
			// A click on a row takes it; a click anywhere else in the container is not the list's.
			const row = flat.find((choice) => optionId(choice) === optionRowId(event.target))
			if (row) choose(row)
		},
	}
}

export type TypeAheadFieldModel = ReturnType<typeof useTypeAheadField>

/** A type-ahead question as a picker the form draws; the logic is `useTypeAheadField`. */
export function TypeAheadField(props: TypeAheadFieldProps) {
	return <TypeAheadFieldView {...props} {...useTypeAheadField(props)} />
}
