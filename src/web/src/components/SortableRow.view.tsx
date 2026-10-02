import { useLocale } from "../i18n/useLocale"
import type { SortableRowModel, SortableRowProps } from "./SortableRow"

const controlClassName =
	"touch-target inline-flex items-center justify-center rounded border border-rule bg-surface-2 px-2 font-sans text-sm text-ink hover:bg-surface-3 disabled:opacity-40"

export type SortableRowViewProps = Omit<SortableRowProps, "id"> & SortableRowModel

export function SortableRowView({
	position,
	count,
	onMove,
	children,
	attributes,
	listeners,
	setNodeRef,
	style,
	isDragging,
}: SortableRowViewProps) {
	const { t } = useLocale()

	return (
		<li
			ref={setNodeRef}
			style={style}
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
