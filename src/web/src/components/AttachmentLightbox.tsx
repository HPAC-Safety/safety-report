import { useCallback, useEffect, useState, type KeyboardEvent } from "react"
import { useModalDialog } from "../hooks/useModalDialog"
import { useLocale } from "../i18n/useLocale"
import type { StripItem } from "./stripItems"
import { AttachmentLightboxView } from "./AttachmentLightbox.view"

/*
 * Steps through a report's images and videos in attachment order (issue no. 427
 * decision 3). Left/Right and the caret buttons move, wrapping at both ends
 * (decision 5); Escape closes; focus is trapped by the native <dialog> while
 * it is open and returns to the thumbnail that opened it on close
 * (AttachmentStrip). A video plays with native controls and its audio, and
 * stops when the lightbox moves away from it or closes. Labels stay generic
 * ("Photo 1 of 3", "Video 1 of 2") — there is no reviewer-authored text.
 *
 * Link rules (decision 11): a public item loads through the anonymous public
 * link, unaudited; a non-public item goes through the audited staff mint. On
 * error, a fresh link is fetched and a video resumes where it was. A 404
 * removes the item from the strip and the lightbox, moving to the next item.
 */

export interface AttachmentLightboxProps {
	/** Images and videos only, in attachment order — a document is never here. */
	items: StripItem[]
	openId: string
	// A method signature, not an arrow property: tools/check-hardcoded-strings.mjs
	// is a line scanner and reads `=> Promise<string>` as JSX text between a `>`
	// and a `<` — see AuthContext.tsx.
	getLink(item: StripItem): Promise<string>
	invalidateLink: (id: string) => void
	onClose: () => void
	onGone: (id: string) => void
}

/** The view model: which item is open, the wrap-around stepping, the keyboard, and closing the native dialog. */
export function useAttachmentLightbox({ items, openId, onClose, onGone }: AttachmentLightboxProps) {
	const { t } = useLocale()
	const { dialogRef: dialog } = useModalDialog()
	const [index, setIndex] = useState(() => Math.max(0, items.findIndex((item) => item.id === openId)))

	useEffect(() => {
		// The current item may have been removed from the strip (404) since the
		// index was last set; clamp rather than read past the end.
		if (index >= items.length) {
			if (items.length === 0) {
				dialog.current?.close()
				onClose()
			} else {
				setIndex(items.length - 1)
			}
		}
		// eslint-disable-next-line react-hooks/exhaustive-deps -- runs when the item count changes, not on every callback or index change
	}, [items.length])

	const wrap = useCallback(
		(next: number) => {
			if (items.length === 0) return 0
			return (next + items.length) % items.length
		},
		[items.length],
	)

	function previous() {
		setIndex((current) => wrap(current - 1))
	}

	function next() {
		setIndex((current) => wrap(current + 1))
	}

	const item = items[index]

	// Closes the native dialog first — it must actually stop being modal before
	// focus can move to anything outside it (an inert element refuses focus) —
	// then tells the parent to unmount it.
	function close() {
		dialog.current?.close()
		onClose()
	}

	const group = item ? items.filter((candidate) => candidate.kind === item.kind) : []
	const label = !item
		? ""
		: item.kind === "image"
			? t("media.photoLabel", { index: group.indexOf(item) + 1, count: group.length })
			: t("media.videoLabel", { index: group.indexOf(item) + 1, count: group.length })

	return {
		dialog,
		item,
		label,
		previous,
		next,
		close,
		onCancel(event: { preventDefault: () => void }) {
			event.preventDefault()
			close()
		},
		onKeyDown(event: KeyboardEvent<HTMLDialogElement>) {
			if (event.key === "ArrowLeft") {
				event.preventDefault()
				previous()
			} else if (event.key === "ArrowRight") {
				event.preventDefault()
				next()
			}
		},
		onItemGone() {
			if (item) onGone(item.id)
		},
	}
}

export type AttachmentLightboxModel = ReturnType<typeof useAttachmentLightbox>

export function AttachmentLightbox(props: AttachmentLightboxProps) {
	return <AttachmentLightboxView {...props} {...useAttachmentLightbox(props)} />
}
