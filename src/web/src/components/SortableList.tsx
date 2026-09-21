import type { ReactNode } from "react"
import {
	DndContext,
	KeyboardSensor,
	PointerSensor,
	closestCenter,
	useSensor,
	useSensors,
	type DragEndEvent,
} from "@dnd-kit/core"
import { restrictToParentElement, restrictToVerticalAxis } from "@dnd-kit/modifiers"
import {
	SortableContext,
	sortableKeyboardCoordinates,
	useSortable,
	verticalListSortingStrategy,
} from "@dnd-kit/sortable"
import { CSS } from "@dnd-kit/utilities"
import { useLocale } from "../i18n/useLocale"

/*
 * The one place this application knows @dnd-kit exists (ADR-0033, ADR-0059).
 *
 * Callers pass items and receive a reordered array of ids. They never see a
 * sensor, a modifier, or a transform, so replacing the library is a change to
 * this file rather than to every screen that reorders something.
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

export function SortableList<T>({ items, getId, onReorder, children, label }: SortableListProps<T>) {
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

	return (
		<DndContext
			sensors={sensors}
			collisionDetection={closestCenter}
			modifiers={[restrictToVerticalAxis, restrictToParentElement]}
			onDragEnd={onDragEnd}
		>
			<SortableContext items={ids} strategy={verticalListSortingStrategy}>
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

const controlClassName =
	"touch-target inline-flex items-center justify-center rounded border border-rule bg-surface-2 px-2 font-sans text-sm text-ink hover:bg-surface-3 disabled:opacity-40"

function SortableRow({
	id,
	position,
	count,
	onMove,
	children,
}: {
	id: string
	position: number
	count: number
	onMove: (from: number, to: number) => void
	children: ReactNode
}) {
	const { t } = useLocale()
	const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id })

	return (
		<li
			ref={setNodeRef}
			style={{ transform: CSS.Transform.toString(transform), transition }}
			className={`flex items-start gap-3 rounded border border-rule bg-surface p-4 ${isDragging ? "opacity-60" : ""}`}
		>
			<div className="flex flex-col items-center gap-1">
				<button
					type="button"
					className={controlClassName}
					onClick={() => onMove(position, position - 1)}
					disabled={position === 0}
					aria-label={t("questions.moveUp")}
				>
					↑
				</button>
				<button
					type="button"
					className={`${controlClassName} cursor-grab`}
					aria-label={t("questions.dragHandle")}
					{...attributes}
					{...listeners}
				>
					⠿
				</button>
				<button
					type="button"
					className={controlClassName}
					onClick={() => onMove(position, position + 1)}
					disabled={position === count - 1}
					aria-label={t("questions.moveDown")}
				>
					↓
				</button>
			</div>

			<div className="min-w-0 flex-1">{children}</div>
		</li>
	)
}
