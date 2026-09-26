/*
 * The one order every list of a question's choices is shown in (ADR-0136):
 * the choices pinned first, then the unpinned ones, then those pinned last,
 * each group alphabetical in the reader's language. Accents and case are
 * ignored, so "Émeu" sorts with the E's, and numbers compare as numbers. Ties
 * fall to the choice's ID, so the order never depends on what the server sent.
 *
 * The server sends each question's choices grouped by pin but not sorted:
 * only the browser knows which language its reader reads.
 */

/** Where a choice is listed relative to the alphabetical rest. */
export type ChoicePin = "none" | "first" | "last"

const GROUP: Record<ChoicePin, number> = { first: 0, none: 1, last: 2 }

function groupOf(pin: string | null | undefined): number {
	return GROUP[(pin ?? "none") as ChoicePin] ?? GROUP.none
}

/**
 * The choices in the order a reader in `locale` sees them, grouped by pin.
 * `label` is the wording that reader sees for each choice.
 */
export function choiceGroups<T extends { id: string; pin?: string | null }>(
	choices: readonly T[],
	locale: string,
	label: (choice: T) => string,
): T[][] {
	const collator = new Intl.Collator(locale, { sensitivity: "base", numeric: true })
	const sorted = [...choices].sort(
		(left, right) =>
			groupOf(left.pin) - groupOf(right.pin) ||
			collator.compare(label(left), label(right)) ||
			(left.id < right.id ? -1 : left.id > right.id ? 1 : 0),
	)

	const groups: T[][] = []
	for (const choice of sorted) {
		const last = groups[groups.length - 1]
		if (last && groupOf(last[0].pin) === groupOf(choice.pin)) last.push(choice)
		else groups.push([choice])
	}
	return groups
}

/** The same order as {@link choiceGroups}, flattened, for a control that cannot draw a separator. */
export function sortChoices<T extends { id: string; pin?: string | null }>(
	choices: readonly T[],
	locale: string,
	label: (choice: T) => string,
): T[] {
	return choiceGroups(choices, locale, label).flat()
}
