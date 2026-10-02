import { ApiError } from "../api/adminQuestions"
import { attachmentLink, type AttachmentVisibility, type ReportAttachment } from "../api/adminReports"
import { fetchMediaLink, PublicReportNotFound, type PublicMedia } from "../api/publicReports"

/** One strip item, normalized from either the public or the staff shape. */
export interface StripItem {
	id: string
	kind: "image" | "video" | "document"
	format: string | null
	state: "ready" | "processing" | "failed"
	/** Null for a plain public viewer — the strip shows no visibility marker then. */
	visibility: AttachmentVisibility | null
}

/** True for anything that means "this item is no longer there" (issue no. 427). */
export function isGone(cause: unknown): boolean {
	return cause instanceof PublicReportNotFound || (cause instanceof ApiError && cause.status === 404)
}

export function itemsFromPublicMedia(media: PublicMedia[]): StripItem[] {
	return media.map((item) => ({ id: item.id, kind: item.kind, format: item.format, state: "ready", visibility: null }))
}

export function itemsFromStaffAttachments(attachments: ReportAttachment[]): StripItem[] {
	return attachments.map((item) => ({
		id: item.id,
		kind: item.kind,
		format: item.format,
		state: item.state,
		visibility: item.visibility,
	}))
}

/** The one link a thumbnail or the lightbox needs for an image or video (issue no. 427 decision 11). */
export async function linkFor(
	reportId: string,
	item: StripItem,
	staff: boolean,
): Promise<{ url: string; expiresAt: string }> {
	if (!staff || item.visibility === "public") {
		return await fetchMediaLink(reportId, item.id)
	}

	return await attachmentLink(reportId, {
		id: item.id,
		kind: item.kind,
		state: item.state,
		visibility: item.visibility ?? "private",
		format: item.format,
	})
}
