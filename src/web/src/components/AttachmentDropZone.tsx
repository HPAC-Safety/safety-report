import { useRef, useState, type ReactNode } from "react"

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
 * The dashed, clickable drop zone shared by the reporter's attachment field
 * and the report page's private-attachment staging area (issue 658): a
 * cloud-upload icon, a "Drag files here, or choose files" prompt, a steady
 * drag highlight (dragenter/dragleave fire for every child the pointer
 * crosses, so a depth counter is what keeps it steady rather than flicker),
 * and a click-to-choose fallback that is also keyboard-operable (Enter or
 * Space, being a native `<button>`). Only the zone and its hidden input live
 * here; each caller owns what happens to the files and what list of rows
 * renders below.
 */
export function AttachmentDropZone({
	fieldId,
	describedBy,
	guidanceId,
	guidance,
	promptText,
	accept,
	multiple = true,
	onFiles,
	testId = "attachment-drop-zone",
}: AttachmentDropZoneProps) {
	const [dragging, setDragging] = useState(false)
	const inputRef = useRef<HTMLInputElement>(null)
	// dragenter/dragleave fire for every child the pointer crosses; counting
	// them keeps the highlight steady until the drag really leaves the zone.
	const dragDepth = useRef(0)

	function endDrag() {
		dragDepth.current = 0
		setDragging(false)
	}

	const buttonDescribedBy = [guidanceId, describedBy].filter(Boolean).join(" ")

	return (
		<div
			className={`mt-2 flex flex-col items-center gap-2 rounded border-2 border-dashed px-4 py-6 text-center transition-colors motion-reduce:transition-none ${
				dragging ? "border-ink-muted bg-surface-3" : "border-rule bg-surface"
			}`}
			data-testid={testId}
			onDragEnter={(event) => {
				if (!carriesFiles(event)) return
				event.preventDefault()
				dragDepth.current += 1
				setDragging(true)
			}}
			onDragOver={(event) => {
				if (!carriesFiles(event)) return
				event.preventDefault()
				event.dataTransfer.dropEffect = "copy"
			}}
			onDragLeave={(event) => {
				if (!carriesFiles(event)) return
				dragDepth.current = Math.max(0, dragDepth.current - 1)
				if (dragDepth.current === 0) setDragging(false)
			}}
			onDrop={(event) => {
				if (!carriesFiles(event)) return
				event.preventDefault()
				endDrag()
				onFiles(Array.from(event.dataTransfer.files))
			}}
		>
			<button
				type="button"
				className="flex flex-col items-center gap-2 rounded px-4 py-2 font-sans text-ink hover:bg-surface-2"
				aria-describedby={buttonDescribedBy}
				onClick={() => inputRef.current?.click()}
			>
				<svg aria-hidden="true" viewBox="0 0 24 24" className="h-14 w-14 text-ink-muted" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
					<path d="M7 18a4.5 4.5 0 0 1-.6-8.96A6 6 0 0 1 18 8.5a4 4 0 0 1 0 8H17" />
					<path d="M12 12v8" />
					<path d="m8.5 15.5 3.5-3.5 3.5 3.5" />
				</svg>
				<span className="text-sm font-medium">{promptText}</span>
			</button>
			<p id={guidanceId} className="font-sans text-xs text-ink-muted">
				{guidance}
			</p>
			{/* Kept in the page and labelled by the caller, so assistive
			    technology and tests still find it; the button above is the
			    one control a person reaches. */}
			<input
				ref={inputRef}
				id={fieldId}
				type="file"
				multiple={multiple}
				accept={accept}
				className="sr-only"
				tabIndex={-1}
				aria-describedby={describedBy}
				onChange={(event) => {
					onFiles(Array.from(event.target.files ?? []))
					// Cleared so the same file can be chosen again after a removal.
					event.target.value = ""
				}}
			/>
		</div>
	)
}
