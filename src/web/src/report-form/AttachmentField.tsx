import { useEffect, useRef, useState } from "react"

import {
	ACCEPTED_FILE_TYPES,
	MAX_ATTACHMENTS,
	SIZE_LIMIT_PARAMS,
	UploadRejectedError,
	deleteUpload,
	exceedsKindLimit,
	uploadAttachment,
	type UploadRejectionReason,
} from "../api/uploads"
import { AttachmentFieldView } from "./AttachmentField.view"

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

// Re-exported for the stray-drop guard, so both keep the same rule for what
// counts as a file drag rather than each declaring its own.
export { carriesFiles } from "../components/AttachmentDropZone"

/**
 * The view model of a file-upload question's attachments: chosen from a drop
 * zone — dropped on it, or picked through its one large button — and uploaded
 * the moment they are chosen, each with its own indeterminate activity
 * indicator and Cancel control while it uploads, and a Remove control once it
 * has (ADR-0096). Each goes straight to storage through the pre-signed PUT the
 * API mints for it (ADR-0126). Everything about an upload in progress is
 * encapsulated here — the form only learns which files finished and whether
 * anything is still in flight.
 */
export function useAttachmentField({
	fieldId,
	attachments,
	onAttachmentsChange,
	onBusyChange,
	remaining,
	t,
}: AttachmentFieldProps) {
	const [inFlight, setInFlight] = useState<InFlight[]>([])
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

		// Refused at once, without minting anything: the API would refuse the
		// same declaration (ADR-0126).
		if (exceedsKindLimit(file)) {
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

	/** What a row says beneath its size when its upload did not stand: it expired, or the file was refused. */
	function rowMessage(row: Attachment): string {
		return row.status === "expired"
			? t("report.attachments.expired")
			: t(`report.attachments.rejected.${row.reason ?? "unknown"}`, SIZE_LIMIT_PARAMS)
	}

	return {
		inFlight,
		hasRows,
		guidanceId,
		guidance: t("report.attachments.guidance", { count: MAX_ATTACHMENTS, ...SIZE_LIMIT_PARAMS }),
		accept: ACCEPTED_FILE_TYPES,
		rowMessage,
		onFiles: choose,
		onCancel: cancel,
		onRemove: remove,
	}
}

export type AttachmentFieldModel = ReturnType<typeof useAttachmentField>

/** A file-upload question's attachments; the logic is `useAttachmentField`. */
export function AttachmentField(props: AttachmentFieldProps) {
	return <AttachmentFieldView {...props} {...useAttachmentField(props)} />
}
