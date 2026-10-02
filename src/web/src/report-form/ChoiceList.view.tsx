import { Fragment } from "react"

/*
 * The one list the report form's three choice questions open: the type-ahead
 * (ADR-0140), the single-select, and the multi-select picker (ADR-0150). Each
 * draws its list from these pieces, so the three stay identical.
 */

/** The open list: directly beneath its field, as wide as it, scrolling when long. */
export const choiceListClassName =
	"absolute left-0 right-0 top-full z-40 mt-1 max-h-72 overflow-y-auto rounded border border-rule bg-surface py-1 shadow-lg"

/** One row of the list: a touch target in the form's font. */
export const choiceRowClassName = "touch-target flex cursor-pointer items-center px-3 font-sans text-ink"

/**
 * The highlighted row: a stronger surface and an inset bar in the focus
 * colour, so it reads at 3:1 against the list. The multi-select picker spells
 * the same two classes behind `hover:` and `has-[:focus-visible]:`.
 */
export const choiceRowHighlightClassName = "bg-surface-4 shadow-[inset_4px_0_0_var(--color-focus)]"

/** The field's caret. */
export function Caret() {
	return (
		<svg aria-hidden="true" viewBox="0 0 20 20" className="h-4 w-4 shrink-0">
			<path d="M5.5 7.5 10 12l4.5-4.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
		</svg>
	)
}

/** The rule drawn between two non-empty pin groups (ADR-0136). */
export function ChoiceSeparator() {
	return <li role="presentation" aria-hidden="true" className="mx-3 my-1 border-t border-rule" data-separator />
}

export interface ListChoice {
	key: string
	/** The wording the reader sees. */
	label: string
	/** The choice's one language, when a reporter added it in one language only. */
	lang?: string
	/**
	 * A short line shown after the label, already composed for display — a
	 * type-ahead's "also: <alias>" when the typed text matched a merged-away
	 * wording rather than the choice's own (ADR-0129 amendment).
	 */
	hint?: string
}

export interface ChoiceOptionsProps {
	/** The choices in display order, pinned first, unpinned, pinned last (ADR-0136). */
	groups: ListChoice[][]
	/** A row before the groups, with no separator after it: a single-select's placeholder. */
	leading?: ListChoice
	optionId: (choice: ListChoice) => string
	/** The highlighted choice's key. */
	activeKey: string | undefined
	/** Whether the option is `aria-selected`: the active one in a type-ahead, the chosen one in a single-select. */
	isSelected: (choice: ListChoice) => boolean
	/** Pointing at a choice highlights it, so hover and the keyboard's highlight are always the same one. */
	onPoint: (choice: ListChoice) => void
	onPick: (choice: ListChoice) => void
}

/** The `role="option"` rows of a listbox, with a separator between pin groups. */
export function ChoiceOptions({ groups, leading, optionId, activeKey, isSelected, onPoint, onPick }: ChoiceOptionsProps) {
	const row = (choice: ListChoice, extra: Record<string, string> = {}) => (
		// eslint-disable-next-line jsx-a11y/click-events-have-key-events -- an option of a combobox listbox: the keyboard drives it from the input through aria-activedescendant
		<li
			key={choice.key}
			id={optionId(choice)}
			role="option"
			// A reporter-added choice may exist in one language only; it is
			// offered in that language, and says so to assistive technology.
			lang={choice.lang}
			aria-selected={isSelected(choice)}
			className={`${choiceRowClassName} ${choice.key === activeKey ? choiceRowHighlightClassName : ""}`}
			onMouseMove={() => {
				if (choice.key !== activeKey) onPoint(choice)
			}}
			// Keep focus in the field while a choice is pressed.
			onMouseDown={(event) => event.preventDefault()}
			onClick={() => onPick(choice)}
			{...extra}
		>
			{choice.label}
			{choice.hint && (
				<span data-testid="choice-hint" className="ml-2 font-sans text-xs text-ink-muted">
					{choice.hint}
				</span>
			)}
		</li>
	)

	return (
		<>
			{leading && row(leading, { "data-placeholder": "" })}
			{groups.map((group, index) => (
				<Fragment key={group[0].key}>
					{index > 0 && <ChoiceSeparator />}
					{group.map((choice) => row(choice))}
				</Fragment>
			))}
		</>
	)
}
