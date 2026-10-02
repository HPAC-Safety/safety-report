import type { ReactNode } from "react"
import {
	DndContext,
	type CollisionDetection,
	type DragEndEvent,
	type DndContextProps,
} from "@dnd-kit/core"
import { SortableContext, type SortingStrategy } from "@dnd-kit/sortable"
import { SortableRow } from "./SortableRow"

export interface SortableListViewProps<T> {
	items: T[]
	getId: (item: T) => string
	children: (item: T, index: number) => ReactNode
	label: string
	ids: string[]
	sensors: DndContextProps["sensors"]
	collisionDetection: CollisionDetection
	modifiers: DndContextProps["modifiers"]
	strategy: SortingStrategy
	move: (from: number, to: number) => void
	onDragEnd: (event: DragEndEvent) => void
}

export function SortableListView<T>({
	items,
	getId,
	children,
	label,
	ids,
	sensors,
	collisionDetection,
	modifiers,
	strategy,
	move,
	onDragEnd,
}: SortableListViewProps<T>) {
	return (
		<DndContext
			sensors={sensors}
			collisionDetection={collisionDetection}
			modifiers={modifiers}
			onDragEnd={onDragEnd}
		>
			<SortableContext items={ids} strategy={strategy}>
				<ul aria-label={label} className="flex flex-col gap-3">
					{items.map((item, index) => (
						<SortableRow
							key={getId(item)}
							id={getId(item)}
							position={index}
							count={items.length}
							onMove={move}
						>
							{children(item, index)}
						</SortableRow>
					))}
				</ul>
			</SortableContext>
		</DndContext>
	)
}
