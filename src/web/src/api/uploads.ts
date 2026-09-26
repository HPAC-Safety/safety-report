/*
 * A reporter's attachment, sent the moment it is attached (ADR-0096,
 * ADR-0126). The API judges what the browser declares — type and exact size —
 * and mints an upload ID and a short-lived pre-signed PUT; the browser then
 * sends the file straight to private storage, never through the API. Its name
 * never leaves the browser here — it travels only with the final submission,
 * as a reviewer's download name (ADR-0097).
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
// The API decides by sniffing at claim either way; this only gives it the
// declaration it signs the upload for and compares the sniff against.
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

/** The limits the API enforces by default (MediaPolicyOptions, ADR-0126). */
export const MAX_ATTACHMENTS = 5
const MEGABYTE = 1024 * 1024
export const MAX_BYTES_BY_KIND: Record<AttachmentKind, number> = {
	video: 250 * MEGABYTE,
	image: 25 * MEGABYTE,
	document: 25 * MEGABYTE,
}

/** The per-kind limits in whole megabytes, as the form's copy states them. */
export const SIZE_LIMIT_PARAMS = {
	videoSize: MAX_BYTES_BY_KIND.video / MEGABYTE,
	size: MAX_BYTES_BY_KIND.image / MEGABYTE,
}

/**
 * The type the browser declares, as one canonical string — lower case, with no
 * parameters — because the upload URL is signed for exactly this and storage
 * refuses any other.
 */
export function declaredType(file: File): string {
	const type = file.type.split(";")[0]?.trim().toLowerCase()
	if (type) return type
	const extension = file.name.split(".").pop()?.toLowerCase() ?? ""
	return TYPE_BY_EXTENSION[extension] ?? "application/octet-stream"
}

/** The kind a declared type belongs to, or null when it is not one the form offers. */
export function declaredKind(type: string): AttachmentKind | null {
	if (!Object.values(TYPE_BY_EXTENSION).includes(type) && type !== "text/rtf") return null
	if (type.startsWith("image/")) return "image"
	if (type.startsWith("video/")) return "video"
	return "document"
}

/** True when a file is larger than its declared kind allows, so it is refused without asking the API. */
export function exceedsKindLimit(file: File): boolean {
	const kind = declaredKind(declaredType(file))
	return kind !== null && file.size > MAX_BYTES_BY_KIND[kind]
}

interface MintedUpload extends UploadedAttachment {
	uploadUrl: string
}

/**
 * Sends one file: mints its upload, then PUTs it straight to storage. Aborting
 * `signal` cancels either request; a cancelled PUT leaves nothing, and the
 * minted ID is released all the same.
 */
export async function uploadAttachment(file: File, signal: AbortSignal): Promise<UploadedAttachment> {
	const contentType = declaredType(file)
	const minted = await mintUpload(contentType, file.size, signal)

	let stored: Response
	try {
		// No Authorization header: the signature in the URL is the only
		// credential storage takes, and it names no member (ADR-0126).
		stored = await fetch(minted.uploadUrl, {
			method: "PUT",
			headers: { "Content-Type": contentType },
			body: file,
			signal,
		})
	} catch (error) {
		void deleteUpload(minted.uploadId)
		if (signal.aborted) throw error
		throw new UploadNetworkError()
	}

	if (!stored.ok) {
		void deleteUpload(minted.uploadId)
		throw new UploadRejectedError("unknown")
	}

	return { uploadId: minted.uploadId, kind: minted.kind }
}

async function mintUpload(contentType: string, byteSize: number, signal: AbortSignal): Promise<MintedUpload> {
	let response: Response
	try {
		response = await fetch("/api/v1/uploads/", {
			method: "POST",
			headers: { ...authorization(), "Content-Type": "application/json" },
			body: JSON.stringify({ contentType, byteSize }),
			signal,
		})
	} catch (error) {
		if (signal.aborted) throw error
		throw new UploadNetworkError()
	}

	if (response.status === 201) {
		return (await response.json()) as MintedUpload
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
