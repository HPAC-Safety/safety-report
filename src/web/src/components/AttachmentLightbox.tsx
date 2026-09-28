import { useCallback, useEffect, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { isGone, type StripItem } from "./AttachmentStrip"

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

// Two failures in a row with no successful load in between means the bytes
// themselves will not play; asking for more links would only loop — the same
// rule ReportMedia used for the old stacked embeds.
const MAX_CONSECUTIVE_FAILURES = 2

export function AttachmentLightbox({
	items,
	openId,
	getLink,
	invalidateLink,
	onClose,
	onGone,
}: {
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
}) {
	const { t } = useLocale()
	const dialog = useRef<HTMLDialogElement>(null)
	const [index, setIndex] = useState(() => Math.max(0, items.findIndex((item) => item.id === openId)))

	useEffect(() => {
		const element = dialog.current
		if (element && !element.open) {
			element.showModal()
		}
	}, [])

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
		// eslint-disable-next-line react-hooks/exhaustive-deps
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

	function onKeyDown(event: React.KeyboardEvent<HTMLDialogElement>) {
		if (event.key === "ArrowLeft") {
			event.preventDefault()
			previous()
		} else if (event.key === "ArrowRight") {
			event.preventDefault()
			next()
		}
	}

	function gone(id: string) {
		onGone(id)
	}

	const item = items[index]

	if (!item) {
		return null
	}

	const group = items.filter((candidate) => candidate.kind === item.kind)
	const label =
		item.kind === "image"
			? t("media.photoLabel", { index: group.indexOf(item) + 1, count: group.length })
			: t("media.videoLabel", { index: group.indexOf(item) + 1, count: group.length })

	// Closes the native dialog first — it must actually stop being modal before
	// focus can move to anything outside it (an inert element refuses focus) —
	// then tells the parent to unmount it.
	function close() {
		dialog.current?.close()
		onClose()
	}

	return (
		<dialog
			ref={dialog}
			aria-label={label}
			onCancel={(event) => {
				event.preventDefault()
				close()
			}}
			onKeyDown={onKeyDown}
			className="m-auto w-[calc(100%-2rem)] max-w-3xl rounded border border-rule bg-surface p-4 text-ink backdrop:bg-black/70"
			data-testid="attachment-lightbox"
		>
			<div className="flex items-center justify-between gap-4">
				<button
					type="button"
					aria-label={t("media.lightbox.previous")}
					className="touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink"
					onClick={previous}
				>
					‹
				</button>
				<LightboxMedia
					key={item.id}
					item={item}
					label={label}
					getLink={getLink}
					invalidateLink={invalidateLink}
					onGone={() => gone(item.id)}
				/>
				<button
					type="button"
					aria-label={t("media.lightbox.next")}
					className="touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink"
					onClick={next}
				>
					›
				</button>
			</div>
			<div className="mt-3 flex items-center justify-between">
				<span className="font-sans text-sm text-ink-muted">{label}</span>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink"
					onClick={close}
				>
					{t("media.lightbox.close")}
				</button>
			</div>
		</dialog>
	)
}

function LightboxMedia({
	item,
	label,
	getLink,
	invalidateLink,
	onGone,
}: {
	item: StripItem
	label: string
	// A method signature, not an arrow property: tools/check-hardcoded-strings.mjs
	// is a line scanner and reads `=> Promise<string>` as JSX text between a `>`
	// and a `<` — see AuthContext.tsx.
	getLink(item: StripItem): Promise<string>
	invalidateLink: (id: string) => void
	onGone: () => void
}) {
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
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [item.id, getLink])

	useEffect(refresh, [refresh])

	// A video stops playing when the lightbox moves away from it or closes —
	// unmounting the <video> element (this component is keyed by item.id, so
	// React unmounts and remounts it on navigation) does that on its own.
	useEffect(() => {
		return () => {
			const element = video.current
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
			}
		}
	}

	function track() {
		const element = video.current
		if (element && !resume.current && element.readyState > 0) {
			position.current = { at: element.currentTime, playing: !element.paused }
		}
	}

	if (!url) {
		return <div className="flex h-64 w-full items-center justify-center" />
	}

	return item.kind === "image" ? (
		<img src={url} alt={label} onError={failed} className="max-h-[70vh] w-full rounded object-contain" />
	) : (
		<video
			ref={video}
			src={url}
			aria-label={label}
			controls
			autoPlay
			playsInline
			preload="metadata"
			onError={failed}
			onLoadedMetadata={loaded}
			onTimeUpdate={track}
			onPlay={track}
			onPause={track}
			className="max-h-[70vh] w-full rounded bg-surface-2"
		/>
	)
}
