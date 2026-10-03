import { useCallback, useEffect, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { attachmentLink, attachmentOriginalLink, setAttachmentHidden, type ReportAttachment } from "../api/adminReports"
import { fetchMediaLink, type PublicMedia } from "../api/publicReports"
import { AttachmentStripView } from "./AttachmentStrip.view"
import { isGone, itemsFromPublicMedia, itemsFromStaffAttachments, linkFor, type StripItem } from "./stripItems"

/*
 * A report's attachments as one horizontal, scrollable strip of thumbnails, in
 * place of stacked embeds or admin list rows (issue no. 427). An image thumbnail
 * is its existing derivative scaled with CSS; a video gets a generic play
 * tile; a document gets a type icon and downloads instead of opening — never
 * inline, never in the lightbox. No new Worker derivative.
 *
 * The public sees only what `media` already lists. A signed-in
 * SafetyOfficer/Administrator instead passes `staffAttachments`: every file,
 * marked with its state and public visibility, with Hide/Show. Either way,
 * activating an image or video thumbnail opens the lightbox (AttachmentLightbox).
 */

export interface AttachmentStripProps {
	reportId: string
	/** The public attachment list — used when `staffAttachments` is null. */
	media: PublicMedia[]
	/** Every attachment, staff-marked; present only for a signed-in reviewer (issue no. 427 decision 4). */
	staffAttachments: ReportAttachment[] | null
	/** Called after a successful hide or show, so the caller can refetch. */
	onChanged: () => void
}

/** The view model: the items, the shared link cache, the lightbox, and what activating, hiding and showing do. */
export function useAttachmentStrip({ reportId, media, staffAttachments, onChanged }: AttachmentStripProps) {
	const { t } = useLocale()
	// Loose check: a stub or an older response may omit the field rather than
	// send an explicit null, and that must still read as "not staff".
	const staff = staffAttachments != null
	const [items, setItems] = useState<StripItem[]>(() =>
		staffAttachments ? itemsFromStaffAttachments(staffAttachments) : itemsFromPublicMedia(media),
	)
	const [lightboxId, setLightboxId] = useState<string | null>(null)
	const [error, setError] = useState<string | null>(null)
	// The thumbnail that opened the lightbox, so closing it can return focus
	// there (decision 5, issue no. 427).
	const returnFocusTo = useRef<HTMLElement | null>(null)
	// One link per id, shared between a thumbnail and the lightbox, so opening
	// the lightbox on an already-loaded thumbnail mints nothing new — a
	// non-public item's audited mint is one row, not two (decision 11).
	// Re-minted only once it is within 5s of expiring, or after an error
	// invalidates it.
	const linkCache = useRef<Map<string, { url: string; expiresAt: string }>>(new Map())

	const getLink = useCallback(
		async (item: StripItem): Promise<string> => {
			const cached = linkCache.current.get(item.id)
			if (cached && new Date(cached.expiresAt).getTime() - Date.now() > 5000) {
				return cached.url
			}
			const link = await linkFor(reportId, item, staff)
			linkCache.current.set(item.id, link)
			return link.url
		},
		[reportId, staff],
	)

	const invalidateLink = useCallback((id: string) => {
		linkCache.current.delete(id)
	}, [])

	useEffect(() => {
		setItems(staffAttachments ? itemsFromStaffAttachments(staffAttachments) : itemsFromPublicMedia(media))
	}, [media, staffAttachments])

	const remove = useCallback((id: string) => {
		linkCache.current.delete(id)
		setItems((current) => current.filter((item) => item.id !== id))
	}, [])

	// Only a ready image or video is ever stepped through — a processing or
	// failed item is never reachable in the lightbox (decision 12; REQ-MED-013,
	// REQ-MED-053, REQ-MED-060).
	const lightboxItems = items.filter((item) => (item.kind === "image" || item.kind === "video") && item.state === "ready")

	function label(item: StripItem): string {
		const group = items.filter((candidate) => candidate.kind === item.kind)
		const values = { index: group.indexOf(item) + 1, count: group.length }
		if (item.kind === "document") return t("media.documentLabel", { ...values, format: (item.format ?? "").toUpperCase() })
		return item.kind === "image" ? t("media.photoLabel", values) : t("media.videoLabel", values)
	}

	async function hide(id: string): Promise<void> {
		setError(null)
		try {
			await setAttachmentHidden(reportId, id, true)
			onChanged()
		} catch {
			setError(t("media.error.hide"))
		}
	}

	async function show(id: string): Promise<void> {
		setError(null)
		try {
			await setAttachmentHidden(reportId, id, false)
			onChanged()
		} catch {
			setError(t("media.error.hide"))
		}
	}

	async function activate(item: StripItem, trigger: HTMLElement): Promise<void> {
		if (item.kind === "document") {
			await downloadDocument(item)
			return
		}

		if (staff && (item.state === "processing" || item.state === "failed")) {
			await downloadOriginal(item)
			return
		}

		returnFocusTo.current = trigger
		setLightboxId(item.id)
	}

	async function downloadDocument(item: StripItem): Promise<void> {
		try {
			// Staff always use the audited /download, under the sanitized reporter
			// filename — decision 11 spares thumbnail loads an audit row, but it
			// never covered a document download (decision 21, issue no. 427).
			if (staff) {
				const link = await attachmentLink(reportId, {
					id: item.id,
					kind: "document",
					state: item.state,
					visibility: item.visibility ?? "private",
					format: item.format,
				})
				window.location.assign(link.url)
				return
			}

			const link = await fetchMediaLink(reportId, item.id)
			window.location.assign(link.url)
		} catch (cause) {
			if (isGone(cause)) remove(item.id)
			else setError(t("media.error.download"))
		}
	}

	async function downloadOriginal(item: StripItem): Promise<void> {
		try {
			const link = await attachmentOriginalLink(reportId, item.id)
			window.location.assign(link.url)
		} catch (cause) {
			if (isGone(cause)) remove(item.id)
			else setError(t("media.error.download"))
		}
	}

	return {
		staff,
		items,
		error,
		lightboxId,
		lightboxItems,
		getLink,
		invalidateLink,
		remove,
		closeLightbox: () => {
			setLightboxId(null)
			returnFocusTo.current?.focus()
		},
		rows: items.map((item) => ({
			item,
			label: label(item),
			onActivate: (trigger: HTMLElement) => void activate(item, trigger),
			onGone: () => remove(item.id),
			onHide: staff && (item.visibility === "public" || item.visibility === "when_published") ? () => void hide(item.id) : null,
			onShow: staff && item.visibility === "hidden" ? () => void show(item.id) : null,
		})),
	}
}

export type AttachmentStripModel = ReturnType<typeof useAttachmentStrip>

export function AttachmentStrip(props: AttachmentStripProps) {
	return <AttachmentStripView {...props} {...useAttachmentStrip(props)} />
}
