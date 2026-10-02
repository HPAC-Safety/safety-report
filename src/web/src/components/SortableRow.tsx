import type { ReactNode } from "react"
import { useSortable } from "@dnd-kit/sortable"
import { CSS } from "@dnd-kit/utilities"
import { SortableRowView } from "./SortableRow.view"

export interface SortableRowProps {
	id: string
	position: number
	count: number
	onMove: (from: number, to: number) => void
	children: ReactNode
}

/** The view model: the sortable hook's refs, handle props and drag transform for one row. */
export function useSortableRow({ id }: SortableRowProps) {
	const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id })

	return {
		attributes,
		listeners,
		setNodeRef,
		style: { transform: CSS.Transform.toString(transform), transition },
		isDragging,
	}
}

export type SortableRowModel = ReturnType<typeof useSortableRow>

/** One row of a SortableList: a drag handle plus explicit move-up and move-down buttons. */
export function SortableRow(props: SortableRowProps) {
	return <SortableRowView {...props} {...useSortableRow(props)} />
}
