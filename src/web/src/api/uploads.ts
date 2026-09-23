/*
 * A reporter's attachment, uploaded the moment it is attached (ADR-0096). The
 * body is the file itself; its name never leaves the browser here — it travels
 * only with the final submission, as a reviewer's download name (ADR-0097).
 */

import { authorization } from "./adminQuestions"

export type AttachmentKind = "image" | "video" | "document"

/** Why the API refused a file, as the stable code it returns. */
export type UploadRejectionReason =
	| "empty"
	| "too_large"
	| "unrecognised_content"
	| "unaccepted_media_type"
	| "declared_type_mismatch"

export interface UploadedAttachment {
	uploadId: string
	kind: AttachmentKind
}

/** The API refused the file itself. */
export class UploadRejectedError extends Error {
	constructor(readonly reason: UploadRejectionReason | "unknown") {
		super(reason)
		this.name = "UploadRejectedError"
	}
}

/** The request never reached a response — offline, or the server was unreachable. */
export class UploadNetworkError extends Error {
	constructor() {
		super("The attachment could not be sent.")
		this.name = "UploadNetworkError"
	}
}

// Some browsers report no type for HEIC, Markdown, or older Office files.
// The API decides by sniffing either way; this only gives it the declaration
// it compares the sniff against.
const TYPE_BY_EXTENSION: Record<string, string> = {
	jpg: "image/jpeg",
	jpeg: "image/jpeg",
	png: "image/png",
	webp: "image/webp",
	heic: "image/heic",
	mp4: "video/mp4",
	mov: "video/quicktime",
	pdf: "application/pdf",
	doc: "application/msword",
	docx: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
	rtf: "application/rtf",
	md: "text/markdown",
	txt: "text/plain",
	odt: "application/vnd.oasis.opendocument.text",
}

/** The `accept` list for the file input — a hint to the picker, never a gate. */
export const ACCEPTED_FILE_TYPES = [
	...new Set(Object.values(TYPE_BY_EXTENSION)),
	...Object.keys(TYPE_BY_EXTENSION).map((extension) => `.${extension}`),
].join(",")

/** The limits the API enforces by default (MediaPolicyOptions). */
export const MAX_ATTACHMENTS = 5
export const MAX_ATTACHMENT_BYTES = 50 * 1024 * 1024

export function declaredType(file: File): string {
	if (file.type) return file.type
	const extension = file.name.split(".").pop()?.toLowerCase() ?? ""
	return TYPE_BY_EXTENSION[extension] ?? "application/octet-stream"
}

/** Uploads one file. Aborting `signal` cancels it; the API keeps nothing of a cancelled upload. */
export async function uploadAttachment(file: File, signal: AbortSignal): Promise<UploadedAttachment> {
	let response: Response
	try {
		response = await fetch("/api/v1/uploads/", {
			method: "POST",
			headers: { ...authorization(), "Content-Type": declaredType(file) },
			body: file,
			signal,
		})
	} catch (error) {
		if (signal.aborted) throw error
		throw new UploadNetworkError()
	}

	if (response.status === 201) {
		return (await response.json()) as UploadedAttachment
	}

	const problem = await response.json().catch(() => null)
	throw new UploadRejectedError((problem?.reason as UploadRejectionReason | undefined) ?? "unknown")
}

/**
 * Erases an unclaimed upload. Best effort: an upload this fails to erase is
 * never claimed, and the storage lifecycle rule expires it.
 */
export async function deleteUpload(uploadId: string): Promise<void> {
	try {
		await fetch(`/api/v1/uploads/${encodeURIComponent(uploadId)}`, {
			method: "DELETE",
			headers: authorization(),
		})
	} catch {
		// Nothing to do: the lifecycle rule is the backstop.
	}
}
