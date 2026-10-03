/**
 * The id of the `role="option"` row a click landed in, or undefined when it
 * landed anywhere else in the list's container.
 *
 * A listbox row is never focused: the keyboard drives it from the field through
 * `aria-activedescendant`. So the row has no key handler of its own, and a
 * click on it is taken once, on the container, for whichever row it reached
 * (REQ-QB-267, REQ-QB-268).
 */
export function optionRowId(target: EventTarget): string | undefined {
	if (!(target instanceof Element)) return undefined
	const id = target.closest('[role="option"]')?.id
	return id ? id : undefined
}
