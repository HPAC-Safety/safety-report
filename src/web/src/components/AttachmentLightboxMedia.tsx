import { useCallback, useEffect, useRef, useState } from "react"
import { AttachmentLightboxMediaView } from "./AttachmentLightboxMedia.view"
import { isGone, type StripItem } from "./stripItems"

// Two failures in a row with no successful load in between means the bytes
// themselves will not play; asking for more links would only loop — the same
// rule ReportMedia used for the old stacked embeds.
const MAX_CONSECUTIVE_FAILURES = 2

export interface AttachmentLightboxMediaProps {
	item: StripItem
	label: string
	// A method signature, not an arrow property: tools/check-hardcoded-strings.mjs
	// is a line scanner and reads `=> Promise<string>` as JSX text between a `>`
	// and a `<` — see AuthContext.tsx.
	getLink(item: StripItem): Promise<string>
	invalidateLink: (id: string) => void
	onGone: () => void
}

/** The view model of the open item: its link, the retry-on-error rule, and a video resuming where it was. */
export function useAttachmentLightboxMedia({ item, getLink, invalidateLink, onGone }: AttachmentLightboxMediaProps) {
	const [url, setUrl] = useState<string | null>(null)
	const failures = useRef(0)
	const resume = useRef<{ at: number; playing: boolean } | null>(null)
	const position = useRef({ at: 0, playing: false })
	const video = useRef<HTMLVideoElement>(null)
	const gone = useRef(onGone)

	useEffect(() => {
		gone.current = onGone
	}, [onGone])

	const refresh = useCallback(() => {
		getLink(item)
			.then((link) => setUrl(link))
			.catch((cause: unknown) => {
				if (isGone(cause)) gone.current()
			})
		// eslint-disable-next-line react-hooks/exhaustive-deps -- keyed by the item's id, not the item object a parent rebuilds each render
	}, [item.id, getLink])

	useEffect(refresh, [refresh])

	// A video stops playing when the lightbox moves away from it or closes —
	// unmounting the <video> element (this component is keyed by item.id, so
	// React unmounts and remounts it on navigation) does that on its own.
	useEffect(() => {
		return () => {
			// eslint-disable-next-line react-hooks/exhaustive-deps -- read when the cleanup runs, not when the effect does: the <video> exists only once the link has loaded
			const element = video.current
			// React detaches the ref before it runs this cleanup, so the element is
			// already null whenever the <video> unmounts; kept as a defence should
			// that order ever change.
			/* v8 ignore next 3 */
			if (element) {
				element.pause()
			}
		}
	}, [])

	function failed() {
		failures.current += 1
		if (failures.current > MAX_CONSECUTIVE_FAILURES) {
			gone.current()
			return
		}

		if (item.kind === "video") {
			resume.current = { ...position.current }
		}

		invalidateLink(item.id)
		refresh()
	}

	function loaded() {
		failures.current = 0
		const element = video.current
		const from = resume.current
		if (element && from) {
			resume.current = null
			element.currentTime = from.at
			if (from.playing) {
				void element.play().catch(() => undefined)
			} else {
				// The element autoplays every source it loads, the replacement
				// included; a video the visitor had paused stays paused.
				element.pause()
			}
		}
	}

	function track() {
		const element = video.current
		if (element && !resume.current && element.readyState > 0) {
			position.current = { at: element.currentTime, playing: !element.paused }
		}
	}

	return { url, video, failed, loaded, track }
}

export type AttachmentLightboxMediaModel = ReturnType<typeof useAttachmentLightboxMedia>

export function AttachmentLightboxMedia(props: AttachmentLightboxMediaProps) {
	return <AttachmentLightboxMediaView {...props} {...useAttachmentLightboxMedia(props)} />
}
