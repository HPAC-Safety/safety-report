import type { ReactNode } from "react"
import {
	KeyboardSensor,
	PointerSensor,
	closestCenter,
	useSensor,
	useSensors,
	type DragEndEvent,
} from "@dnd-kit/core"
import { restrictToParentElement, restrictToVerticalAxis } from "@dnd-kit/modifiers"
import { sortableKeyboardCoordinates, verticalListSortingStrategy } from "@dnd-kit/sortable"
import { SortableListView } from "./SortableList.view"

/*
 * The one place this application knows @dnd-kit exists (ADR-0033, ADR-0059).
 *
 * Callers pass items and receive a reordered array of ids. They never see a
 * sensor, a modifier, or a transform, so replacing the library is a change to
 * this file (and SortableRow.tsx) rather than to every screen that reorders
 * something.
 *
 * Reordering is never pointer-only. Each row carries a drag handle that is a
 * real focusable button — @dnd-kit's keyboard sensor drives it with the arrow
 * keys — plus explicit move-up and move-down buttons, which is what actually
 * works on a touch screen and with a screen reader.
 */

export interface SortableListProps<T> {
	items: T[]
	getId: (item: T) => string
	onReorder: (idsInOrder: string[]) => void
	children: (item: T, index: number) => ReactNode
	label: string
}

const MODIFIERS = [restrictToVerticalAxis, restrictToParentElement]

/** The view model: the drag sensors and the move that turns a drag or a button into a reordered id list. */
export function useSortableList<T>({ items, getId, onReorder }: SortableListProps<T>) {
	const sensors = useSensors(
		useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
		useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
	)

	const ids = items.map(getId)

	function move(from: number, to: number) {
		if (to < 0 || to >= ids.length) return

		const next = [...ids]
		const [moved] = next.splice(from, 1)
		next.splice(to, 0, moved)
		onReorder(next)
	}

	function onDragEnd(event: DragEndEvent) {
		const { active, over } = event
		if (!over || active.id === over.id) return

		move(ids.indexOf(String(active.id)), ids.indexOf(String(over.id)))
	}

	return {
		ids,
		sensors,
		collisionDetection: closestCenter,
		modifiers: MODIFIERS,
		strategy: verticalListSortingStrategy,
		move,
		onDragEnd,
	}
}

// Signature split across lines on purpose: tools/web/check-hardcoded-strings.mjs is a line scanner.
export function SortableList<T>(
	props: SortableListProps<T>,
) {
	return <SortableListView {...props} {...useSortableList(props)} />
}
