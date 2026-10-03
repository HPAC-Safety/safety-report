import type { UploadRejectionReason } from "../api/uploads"
import type { Attachment } from "./AttachmentField"
import type { DraftAttachment } from "./draft"

export type AttachmentMap = Record<string, Attachment[]>
export type DraftAttachmentMap = Record<string, DraftAttachment[]>

/**
 * Whether any upload has finished, which is when the form asks media consent:
 * it covers photos, video, and documents alike (ADR-0117, ADR-0119).
 */
export function hasFileAttached(attachments: AttachmentMap): boolean {
	return Object.values(attachments)
		.flat()
		.some((row) => row.status === "uploaded")
}

/** The finished uploads the draft keeps, per question; a refused or expired row is not worth restoring. */
export function savedAttachments(attachments: AttachmentMap): DraftAttachmentMap {
	const saved: DraftAttachmentMap = {}
	for (const [revisionId, rows] of Object.entries(attachments)) {
		const uploaded: DraftAttachmentMap[string] = []
		for (const row of rows) {
			if (row.status === "uploaded" && row.uploadId) uploaded.push({ uploadId: row.uploadId, name: row.name, size: row.size })
		}
		if (uploaded.length > 0) saved[revisionId] = uploaded
	}
	return saved
}

/**
 * Marks only the uploads a submission turned back: an expired one to attach
 * again, a refused one with the reason sniffing gave. Every other row stays
 * exactly as it was (REQ-SUB-051, REQ-SUB-076).
 */
export function markRejectedUploads(
	attachments: AttachmentMap,
	expired: ReadonlySet<string>,
	refused: ReadonlyMap<string, UploadRejectionReason>,
): AttachmentMap {
	return Object.fromEntries(
		Object.entries(attachments).map(([revisionId, rows]) => [
			revisionId,
			rows.map((row) => {
				if (!row.uploadId) return row
				if (expired.has(row.uploadId)) return { ...row, status: "expired" as const }
				const reason = refused.get(row.uploadId)
				return reason ? { ...row, status: "rejected" as const, reason } : row
			}),
		]),
	)
}
