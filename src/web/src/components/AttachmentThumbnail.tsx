import { useCallback, useEffect, useRef, useState } from "react"
import { AttachmentThumbnailView } from "./AttachmentThumbnail.view"
import { isGone, type StripItem } from "./stripItems"

export interface AttachmentThumbnailProps {
	item: StripItem
	label: string
	staff: boolean
	// A method signature, not an arrow property: tools/web/check-hardcoded-strings.mjs
	// is a line scanner and reads `=> Promise<string>` as JSX text between a `>`
	// and a `<` — see AuthContext.tsx.
	getLink(item: StripItem): Promise<string>
	invalidateLink: (id: string) => void
	onActivate: (trigger: HTMLElement) => void
	onGone: () => void
	onHide: (() => void) | null
	onShow: (() => void) | null
}

/** The view model of one thumbnail: an image's derivative link, with one retry when its bytes fail to load. */
export function useAttachmentThumbnail({ item, getLink, invalidateLink, onGone }: AttachmentThumbnailProps) {
	const [thumbnailUrl, setThumbnailUrl] = useState<string | null>(null)
	const failures = useRef(0)

	// Only an image loads its actual derivative as the thumbnail — a video gets
	// a generic play tile and a document a type icon, neither of which needs a
	// link until activated (decision 3, issue no. 427).
	const refresh = useCallback(() => {
		if (item.kind !== "image" || item.state !== "ready") {
			return
		}

		getLink(item)
			.then((link) => setThumbnailUrl(link))
			.catch((cause: unknown) => {
				if (isGone(cause)) onGone()
			})
		// eslint-disable-next-line react-hooks/exhaustive-deps -- keyed by the item's identity fields, not the item object a parent rebuilds each render
	}, [item.id, item.kind, item.state, getLink])

	useEffect(refresh, [refresh])

	// A thumbnail's own bytes can fail to load even after a good mint — most
	// often an already-expired link (ADR-0117 point 6) — so one retry asks for
	// a fresh one before giving up; a second failure just leaves it blank
	// rather than looping (the lightbox is the resilient path — REQ-MED-033).
	function failed() {
		failures.current += 1
		if (failures.current <= 1) {
			invalidateLink(item.id)
			refresh()
		}
	}

	return { thumbnailUrl, failed }
}

export type AttachmentThumbnailModel = ReturnType<typeof useAttachmentThumbnail>

export function AttachmentThumbnail(props: AttachmentThumbnailProps) {
	return <AttachmentThumbnailView {...props} {...useAttachmentThumbnail(props)} />
}
