import { useRef, useState, type ChangeEvent, type DragEvent, type ReactNode } from "react"
import { AttachmentDropZoneView } from "./AttachmentDropZone.view"

/** True when a drag carries files, as opposed to selected text or a link. */
export function carriesFiles(event: { dataTransfer: DataTransfer | null }): boolean {
	return Array.from(event.dataTransfer?.types ?? []).includes("Files")
}

export interface AttachmentDropZoneProps {
	fieldId: string
	describedBy: string | undefined
	guidanceId: string
	guidance: ReactNode
	promptText: string
	accept?: string
	multiple?: boolean
	onFiles: (files: File[]) => void
	testId?: string
}

/**
 * The view model: the steady drag highlight (dragenter/dragleave fire for every
 * child the pointer crosses, so a depth counter is what keeps it steady rather
 * than flicker), the drop, and the click-to-choose fallback through the hidden
 * input.
 */
export function useAttachmentDropZone({ describedBy, guidanceId, onFiles }: AttachmentDropZoneProps) {
	const [dragging, setDragging] = useState(false)
	const inputRef = useRef<HTMLInputElement>(null)
	// dragenter/dragleave fire for every child the pointer crosses; counting
	// them keeps the highlight steady until the drag really leaves the zone.
	const dragDepth = useRef(0)

	function endDrag() {
		dragDepth.current = 0
		setDragging(false)
	}

	return {
		dragging,
		inputRef,
		buttonDescribedBy: [guidanceId, describedBy].filter(Boolean).join(" "),
		onDragEnter(event: DragEvent<HTMLDivElement>) {
			if (!carriesFiles(event)) return
			event.preventDefault()
			dragDepth.current += 1
			setDragging(true)
		},
		onDragOver(event: DragEvent<HTMLDivElement>) {
			if (!carriesFiles(event)) return
			event.preventDefault()
			event.dataTransfer.dropEffect = "copy"
		},
		onDragLeave(event: DragEvent<HTMLDivElement>) {
			if (!carriesFiles(event)) return
			dragDepth.current = Math.max(0, dragDepth.current - 1)
			if (dragDepth.current === 0) setDragging(false)
		},
		onDrop(event: DragEvent<HTMLDivElement>) {
			if (!carriesFiles(event)) return
			event.preventDefault()
			endDrag()
			onFiles(Array.from(event.dataTransfer.files))
		},
		onChoose() {
			inputRef.current?.click()
		},
		onInputChange(event: ChangeEvent<HTMLInputElement>) {
			onFiles(Array.from(event.target.files ?? []))
			// Cleared so the same file can be chosen again after a removal.
			event.target.value = ""
		},
	}
}

export type AttachmentDropZoneModel = ReturnType<typeof useAttachmentDropZone>

/**
 * The dashed, clickable drop zone shared by the reporter's attachment field
 * and the report page's private-attachment staging area (issue 658): a
 * cloud-upload icon, a "Drag files here, or choose files" prompt, a steady
 * drag highlight, and a click-to-choose fallback that is also
 * keyboard-operable (Enter or Space, being a native `<button>`). Only the zone
 * and its hidden input live here; each caller owns what happens to the files
 * and what list of rows renders below.
 */
export function AttachmentDropZone(props: AttachmentDropZoneProps) {
	return <AttachmentDropZoneView {...props} {...useAttachmentDropZone(props)} />
}
