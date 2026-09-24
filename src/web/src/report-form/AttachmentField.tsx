import { useEffect, useRef, useState } from "react"

import {
	ACCEPTED_FILE_TYPES,
	MAX_ATTACHMENTS,
	MAX_ATTACHMENT_BYTES,
	UploadRejectedError,
	deleteUpload,
	uploadAttachment,
	type UploadRejectionReason,
} from "../api/uploads"

/**
 * One file the reporter attached, as the form keeps it once its upload has
 * finished. A file still uploading is not one of these: that state, its
 * activity indicator, and its Cancel control live only inside AttachmentField.
 */
export interface Attachment {
	/** A browser-local key for the row; never sent anywhere. */
	key: string
	name: string
	size: number
	status: "uploaded" | "rejected" | "expired"
	/** Set once the API has accepted the file. */
	uploadId?: string
	reason?: UploadRejectionReason | "unknown" | "network" | "limit"
}

interface InFlight {
	key: string
	name: string
	size: number
	controller: AbortController
}

export interface AttachmentFieldProps {
	fieldId: string
	describedBy: string | undefined
	/** Finished rows for this question, held by the form so they survive paging. */
	attachments: Attachment[]
	onAttachmentsChange: (update: (current: Attachment[]) => Attachment[]) => void
	/** True while any file in this field is still uploading. */
	onBusyChange: (busy: boolean) => void
	/** How many more files the whole report may still take. */
	remaining: number
	t: (key: string, params?: Record<string, string | number>) => string
}

let nextKey = 0

/** True when a drag carries files, as opposed to selected text or a link. */
export function carriesFiles(event: { dataTransfer: DataTransfer | null }): boolean {
	return Array.from(event.dataTransfer?.types ?? []).includes("Files")
}

/**
 * A file-upload question's attachments: chosen from a drop zone — dropped on
 * it, or picked through its one large button — and uploaded the moment they
 * are chosen,
 * each with its own indeterminate activity indicator and Cancel control while it
 * uploads, and a Remove control once it has (ADR-0096). Everything about an
 * upload in progress is encapsulated here — the form only learns which files
 * finished and whether anything is still in flight.
 */
export function AttachmentField({
	fieldId,
	describedBy,
	attachments,
	onAttachmentsChange,
	onBusyChange,
	remaining,
	t,
}: AttachmentFieldProps) {
	const [inFlight, setInFlight] = useState<InFlight[]>([])
	const [dragging, setDragging] = useState(false)
	const inputRef = useRef<HTMLInputElement>(null)
	// dragenter/dragleave fire for every child the pointer crosses; counting
	// them keeps the highlight steady until the drag really leaves the zone.
	const dragDepth = useRef(0)
	const inFlightRef = useRef<InFlight[]>([])
	inFlightRef.current = inFlight
	// A ref, so a parent passing a fresh callback each render never re-runs the
	// unmount cleanup below and aborts uploads that are still wanted.
	const onBusyChangeRef = useRef(onBusyChange)
	onBusyChangeRef.current = onBusyChange

	useEffect(() => {
		onBusyChangeRef.current(inFlight.length > 0)
	}, [inFlight.length])

	// Leaving the page abandons anything still uploading; the API keeps nothing
	// of a cancelled upload.
	useEffect(
		() => () => {
			for (const upload of inFlightRef.current) upload.controller.abort()
			onBusyChangeRef.current(false)
		},
		[],
	)

	function settle(key: string, row: Attachment | null) {
		setInFlight((current) => current.filter((upload) => upload.key !== key))
		if (row) onAttachmentsChange((current) => [...current, row])
	}

	async function start(file: File) {
		const key = `attachment-${nextKey++}`
		const base = { key, name: file.name, size: file.size }

		if (file.size > MAX_ATTACHMENT_BYTES) {
			onAttachmentsChange((current) => [...current, { ...base, status: "rejected", reason: "too_large" }])
			return
		}

		const controller = new AbortController()
		setInFlight((current) => [...current, { ...base, controller }])

		try {
			const uploaded = await uploadAttachment(file, controller.signal)
			if (controller.signal.aborted) {
				void deleteUpload(uploaded.uploadId)
				return
			}
			settle(key, { ...base, status: "uploaded", uploadId: uploaded.uploadId })
		} catch (error) {
			if (controller.signal.aborted) return // Cancel already removed the row.
			const reason = error instanceof UploadRejectedError ? error.reason : "network"
			settle(key, { ...base, status: "rejected", reason })
		}
	}

	function choose(files: File[]) {
		let room = remaining - inFlight.length
		for (const file of files) {
			if (room <= 0) {
				onAttachmentsChange((current) => [
					...current,
					{ key: `attachment-${nextKey++}`, name: file.name, size: file.size, status: "rejected", reason: "limit" },
				])
				continue
			}
			room -= 1
			void start(file)
		}
	}

	function cancel(key: string) {
		inFlight.find((upload) => upload.key === key)?.controller.abort()
		settle(key, null)
	}

	function remove(row: Attachment) {
		if (row.uploadId && row.status === "uploaded") void deleteUpload(row.uploadId)
		onAttachmentsChange((current) => current.filter((candidate) => candidate.key !== row.key))
	}

	const hasRows = attachments.length > 0 || inFlight.length > 0

	const guidanceId = `${fieldId}-guidance`
	const buttonDescribedBy = [guidanceId, describedBy].filter(Boolean).join(" ")

	function endDrag() {
		dragDepth.current = 0
		setDragging(false)
	}

	return (
		<>
			<div
				className={`mt-2 flex flex-col items-center gap-2 rounded border-2 border-dashed px-4 py-6 text-center transition-colors motion-reduce:transition-none ${
					dragging ? "border-ink-muted bg-surface-3" : "border-rule bg-surface"
				}`}
				data-testid="attachment-drop-zone"
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
					choose(Array.from(event.dataTransfer.files))
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
					<span className="text-sm font-medium">{t("report.attachments.dropPrompt")}</span>
				</button>
				<p id={guidanceId} className="font-sans text-xs text-ink-muted">
					{t("report.attachments.guidance", {
						count: MAX_ATTACHMENTS,
						size: MAX_ATTACHMENT_BYTES / (1024 * 1024),
					})}
				</p>
				{/* Kept in the page and labelled by the question, so assistive
				    technology and tests still find it; the button above is the
				    one control a reporter reaches. */}
				<input
					ref={inputRef}
					id={fieldId}
					type="file"
					multiple
					accept={ACCEPTED_FILE_TYPES}
					className="sr-only"
					tabIndex={-1}
					aria-describedby={describedBy}
					onChange={(event) => {
						choose(Array.from(event.target.files ?? []))
						// Cleared so the same file can be chosen again after a removal.
						event.target.value = ""
					}}
				/>
			</div>

			{hasRows && (
				<ul className="mt-3 divide-y divide-rule rounded border border-rule" aria-label={t("report.attachments.listLabel")}>
					{attachments.map((row) => (
						<li key={row.key} className="flex items-center justify-between gap-3 px-3 py-2">
							<div className="min-w-0">
								<p className="truncate font-sans text-sm text-ink">{row.name}</p>
								<p className="font-sans text-xs text-ink-muted">{formatSize(row.size)}</p>
								{row.status !== "uploaded" && (
									<p role="alert" className="font-sans text-xs text-brand-700">
										{row.status === "expired" ? t("report.attachments.expired") : t(`report.attachments.rejected.${row.reason ?? "unknown"}`)}
									</p>
								)}
							</div>
							<button
								type="button"
								className="touch-target shrink-0 rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
								aria-label={t("report.attachments.removeNamed", { name: row.name })}
								onClick={() => remove(row)}
							>
								{t("report.attachments.remove")}
							</button>
						</li>
					))}
					{inFlight.map((upload) => (
						<li key={upload.key} className="flex items-center justify-between gap-3 px-3 py-2" aria-busy="true">
							<div className="flex min-w-0 items-center gap-3">
								<span
									role="progressbar"
									aria-label={t("report.attachments.uploadingNamed", { name: upload.name })}
									className="inline-block h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-rule border-t-brand-700 motion-reduce:animate-none"
								/>
								<div className="min-w-0">
									<p className="truncate font-sans text-sm text-ink">{upload.name}</p>
									<p className="font-sans text-xs text-ink-muted">{t("report.attachments.uploading")}</p>
								</div>
							</div>
							<button
								type="button"
								className="touch-target shrink-0 rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
								aria-label={t("report.attachments.cancelNamed", { name: upload.name })}
								onClick={() => cancel(upload.key)}
							>
								{t("report.attachments.cancel")}
							</button>
						</li>
					))}
				</ul>
			)}
		</>
	)
}

function formatSize(bytes: number): string {
	if (bytes < 1024) return `${bytes} B`
	if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
	return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
