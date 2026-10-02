import type { AttachmentDropZoneModel, AttachmentDropZoneProps } from "./AttachmentDropZone"

export type AttachmentDropZoneViewProps = AttachmentDropZoneProps & AttachmentDropZoneModel

/** The drop zone's markup: a dashed region holding the prompt button, the guidance and the hidden file input. */
export function AttachmentDropZoneView({
	fieldId,
	describedBy,
	guidanceId,
	guidance,
	promptText,
	accept,
	multiple = true,
	testId = "attachment-drop-zone",
	dragging,
	inputRef,
	buttonDescribedBy,
	onDragEnter,
	onDragOver,
	onDragLeave,
	onDrop,
	onChoose,
	onInputChange,
}: AttachmentDropZoneViewProps) {
	return (
		<div
			className={`mt-2 flex flex-col items-center gap-2 rounded border-2 border-dashed px-4 py-6 text-center transition-colors motion-reduce:transition-none ${
				dragging ? "border-ink-muted bg-surface-3" : "border-rule bg-surface"
			}`}
			data-testid={testId}
			onDragEnter={onDragEnter}
			onDragOver={onDragOver}
			onDragLeave={onDragLeave}
			onDrop={onDrop}
		>
			<button
				type="button"
				className="flex flex-col items-center gap-2 rounded px-4 py-2 font-sans text-ink hover:bg-surface-2"
				aria-describedby={buttonDescribedBy}
				onClick={onChoose}
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
				onChange={onInputChange}
			/>
		</div>
	)
}
