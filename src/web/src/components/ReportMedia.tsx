import { useCallback, useEffect, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import { fetchMediaLink, hideMedia, PublicReportNotFound, type PublicMedia } from "../api/publicReports"

/*
 * A published report's photos and video, embedded in its page (ADR-0117,
 * REQ-MED-032..035).
 *
 * Each file's bytes come from a link that lives at most fifteen minutes, asked
 * for when the file is shown. When an image or video stops loading — most often
 * because its link expired — the page asks for a new one and carries on; a
 * video resumes where it was. If the API answers 404, the file is no longer
 * public, and it is removed from the page. A reviewer may hide any file here;
 * the API authorizes that, so the control is convenience, not the boundary.
 */

// Two failures in a row with no successful load in between means the bytes
// themselves will not play; asking for more links would only loop.
const MAX_CONSECUTIVE_FAILURES = 2

export function ReportMedia({ reportId, media }: { reportId: string; media: PublicMedia[] }) {
	const { t } = useLocale()
	const { role } = useAuth()
	const [shown, setShown] = useState(media)
	const [error, setError] = useState<string | null>(null)

	useEffect(() => setShown(media), [media])

	const remove = useCallback((id: string) => setShown((current) => current.filter((item) => item.id !== id)), [])

	if (shown.length === 0) {
		return null
	}

	const isReviewer = role === "safety_officer" || role === "administrator"
	const images = shown.filter((item) => item.kind === "image")
	const videos = shown.filter((item) => item.kind === "video")

	function label(item: PublicMedia): string {
		const group = item.kind === "image" ? images : videos
		const values = { index: group.indexOf(item) + 1, count: group.length }
		return item.kind === "image" ? t("media.photoLabel", values) : t("media.videoLabel", values)
	}

	async function hide(id: string): Promise<void> {
		setError(null)
		try {
			await hideMedia(reportId, id)
			remove(id)
		} catch {
			setError(t("media.error.hide"))
		}
	}

	return (
		<section aria-labelledby="media-heading" className="mt-10">
			<h2 id="media-heading" className="font-display text-2xl font-bold">
				{t("media.title")}
			</h2>

			{error && (
				<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			<ul className="mt-4 flex flex-col gap-6">
				{shown.map((item) => (
					<li key={item.id} data-media={item.kind}>
						<MediaItem
							reportId={reportId}
							item={item}
							label={label(item)}
							onGone={() => remove(item.id)}
							onHide={isReviewer ? () => hide(item.id) : null}
						/>
					</li>
				))}
			</ul>
		</section>
	)
}

// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
type Hide = () =>
	Promise<void>

function MediaItem({
	reportId,
	item,
	label,
	onGone,
	onHide,
}: {
	reportId: string
	item: PublicMedia
	label: string
	onGone: () => void
	onHide: Hide | null
}) {
	const { t } = useLocale()
	const [url, setUrl] = useState<string | null>(null)
	const [confirming, setConfirming] = useState(false)
	const failures = useRef(0)
	const resume = useRef<{ at: number; playing: boolean } | null>(null)
	// Where the video last was, kept as it plays: by the time a failed request
	// reports an error, the element may already have reset its own position.
	const position = useRef({ at: 0, playing: false })
	const video = useRef<HTMLVideoElement>(null)

	// The parent passes a new callback each render; asking for a link must not
	// follow it, or every render would ask again.
	const gone = useRef(onGone)
	useEffect(() => {
		gone.current = onGone
	}, [onGone])

	const refresh = useCallback(() => {
		fetchMediaLink(reportId, item.id)
			.then((link) => setUrl(link.url))
			.catch((cause: unknown) => {
				if (cause instanceof PublicReportNotFound) {
					gone.current()
				}
			})
	}, [reportId, item.id])

	useEffect(refresh, [refresh])

	function failed() {
		failures.current += 1
		if (failures.current > MAX_CONSECUTIVE_FAILURES) {
			gone.current()
			return
		}

		if (item.kind === "video") {
			resume.current = { ...position.current }
		}

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
		// A reload in progress reads as position zero; keep the real one.
		if (element && !resume.current && element.readyState > 0) {
			position.current = { at: element.currentTime, playing: !element.paused }
		}
	}

	return (
		<figure className="flex flex-col gap-2">
			{url &&
				(item.kind === "image" ? (
					<img src={url} alt={label} onError={failed} onLoad={loaded} className="w-full rounded border border-rule" />
				) : (
					<video
						ref={video}
						src={url}
						aria-label={label}
						controls
						playsInline
						preload="metadata"
						onError={failed}
						onLoadedMetadata={loaded}
						onTimeUpdate={track}
						onPlay={track}
						onPause={track}
						className="w-full rounded border border-rule bg-surface-2"
					/>
				))}
			<figcaption aria-hidden="true" className="font-sans text-sm text-ink-muted">
				{label}
			</figcaption>

			{onHide && (
				<div className="flex flex-wrap items-center gap-3">
					{confirming ? (
						<>
							<span className="font-sans text-sm text-ink">{t("media.confirmHide")}</span>
							<button
								type="button"
								className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse"
								onClick={() => void onHide().then(() => setConfirming(false))}
							>
								{t("media.hide")}
							</button>
							<button
								type="button"
								className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink"
								onClick={() => setConfirming(false)}
							>
								{t("media.keep")}
							</button>
						</>
					) : (
						<button
							type="button"
							className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink"
							onClick={() => setConfirming(true)}
						>
							{t("media.hide")}
						</button>
					)}
				</div>
			)}
		</figure>
	)
}
