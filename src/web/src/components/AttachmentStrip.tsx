import { useCallback, useEffect, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	attachmentLink,
	attachmentOriginalLink,
	setAttachmentHidden,
	type AttachmentVisibility,
	type ReportAttachment,
} from "../api/adminReports"
import { fetchMediaLink, PublicReportNotFound, type PublicMedia } from "../api/publicReports"
import { AttachmentLightbox } from "./AttachmentLightbox"

/*
 * A report's attachments as one horizontal, scrollable strip of thumbnails, in
 * place of stacked embeds or admin list rows (issue #427). An image thumbnail
 * is its existing derivative scaled with CSS; a video gets a generic play
 * tile; a document gets a type icon and downloads instead of opening — never
 * inline, never in the lightbox. No new Worker derivative.
 *
 * The public sees only what `media` already lists. A signed-in
 * SafetyOfficer/Administrator instead passes `staffAttachments`: every file,
 * marked with its state and public visibility, with Hide/Show. Either way,
 * activating an image or video thumbnail opens the lightbox (AttachmentLightbox).
 */

/** One strip item, normalized from either the public or the staff shape. */
export interface StripItem {
	id: string
	kind: "image" | "video" | "document"
	format: string | null
	state: "ready" | "processing" | "failed"
	/** Null for a plain public viewer — the strip shows no visibility marker then. */
	visibility: AttachmentVisibility | null
}

function itemsFromPublicMedia(media: PublicMedia[]): StripItem[] {
	return media.map((item) => ({ id: item.id, kind: item.kind, format: item.format, state: "ready", visibility: null }))
}

function itemsFromStaffAttachments(attachments: ReportAttachment[]): StripItem[] {
	return attachments.map((item) => ({
		id: item.id,
		kind: item.kind,
		format: item.format,
		state: item.state,
		visibility: item.visibility,
	}))
}

export function AttachmentStrip({
	reportId,
	media,
	staffAttachments,
	onChanged,
}: {
	reportId: string
	/** The public attachment list — used when `staffAttachments` is null. */
	media: PublicMedia[]
	/** Every attachment, staff-marked; present only for a signed-in reviewer (issue #427 decision 4). */
	staffAttachments: ReportAttachment[] | null
	/** Called after a successful hide or show, so the caller can refetch. */
	onChanged: () => void
}) {
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
	// there (decision 5, issue #427).
	const returnFocusTo = useRef<HTMLElement | null>(null)

	useEffect(() => {
		setItems(staffAttachments ? itemsFromStaffAttachments(staffAttachments) : itemsFromPublicMedia(media))
	}, [media, staffAttachments])

	const remove = useCallback((id: string) => setItems((current) => current.filter((item) => item.id !== id)), [])

	if (items.length === 0) {
		return null
	}

	const lightboxItems = items.filter((item) => item.kind === "image" || item.kind === "video")

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
			if (staff && item.visibility !== "public") {
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
			if (cause instanceof PublicReportNotFound) remove(item.id)
			else setError(t("media.error.download"))
		}
	}

	async function downloadOriginal(item: StripItem): Promise<void> {
		try {
			const link = await attachmentOriginalLink(reportId, item.id)
			window.location.assign(link.url)
		} catch {
			setError(t("media.error.download"))
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

			<ul className="mt-4 flex snap-x snap-mandatory gap-3 overflow-x-auto pb-2" data-testid="attachment-strip">
				{items.map((item) => (
					<li key={item.id} data-media={item.kind} className="flex-none snap-start">
						<Thumbnail
							reportId={reportId}
							item={item}
							label={label(item)}
							staff={staff}
							onActivate={(trigger) => void activate(item, trigger)}
							onGone={() => remove(item.id)}
							onHide={staff && (item.visibility === "public" || item.visibility === "when_published") ? () => hide(item.id) : null}
							onShow={staff && item.visibility === "hidden" ? () => show(item.id) : null}
						/>
					</li>
				))}
			</ul>

			{lightboxId && (
				<AttachmentLightbox
					reportId={reportId}
					items={lightboxItems}
					openId={lightboxId}
					staff={staff}
					onClose={() => {
						setLightboxId(null)
						returnFocusTo.current?.focus()
					}}
					onGone={remove}
				/>
			)}
		</section>
	)
}

function VisibilityLabel({ item }: { item: StripItem }) {
	const { t } = useLocale()

	if (item.state === "processing") {
		return (
			<span className="font-sans text-xs text-ink-muted" data-testid="attachment-state">
				{t("reports.attachment.state.processing")}
			</span>
		)
	}

	if (item.state === "failed") {
		return (
			<span className="font-sans text-xs text-ink-muted" data-testid="attachment-state">
				{t("reports.attachment.state.failed")}
			</span>
		)
	}

	if (item.visibility === null) {
		return null
	}

	return (
		<span className="font-sans text-xs text-ink-muted" data-visibility={item.visibility} data-testid="attachment-visibility">
			{t(`reports.attachment.visibility.${item.visibility}`)}
		</span>
	)
}

function Thumbnail({
	reportId,
	item,
	label,
	staff,
	onActivate,
	onGone,
	onHide,
	onShow,
}: {
	reportId: string
	item: StripItem
	label: string
	staff: boolean
	onActivate: (trigger: HTMLElement) => void
	onGone: () => void
	onHide: (() => void) | null
	onShow: (() => void) | null
}) {
	const { t } = useLocale()
	const [thumbnailUrl, setThumbnailUrl] = useState<string | null>(null)
	const failures = useRef(0)

	// Only an image loads its actual derivative as the thumbnail — a video gets
	// a generic play tile and a document a type icon, neither of which needs a
	// link until activated (decision 3, issue #427).
	const refresh = useCallback(() => {
		if (item.kind !== "image" || item.state !== "ready") {
			return
		}

		linkFor(reportId, item, staff)
			.then((link) => setThumbnailUrl(link))
			.catch((cause: unknown) => {
				if (cause instanceof PublicReportNotFound) onGone()
			})
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [reportId, item.id, item.kind, item.state, staff])

	useEffect(refresh, [refresh])

	// A thumbnail's own bytes can fail to load even after a good mint — most
	// often an already-expired link (ADR-0117 point 6) — so one retry asks for
	// a fresh one before giving up; a second failure just leaves it blank
	// rather than looping (the lightbox is the resilient path — REQ-MED-033).
	function failed() {
		failures.current += 1
		if (failures.current <= 1) {
			refresh()
		}
	}

	return (
		<div className="flex w-32 flex-col gap-1">
			<button
				type="button"
				onClick={(event) => onActivate(event.currentTarget)}
				aria-label={item.kind === "document" ? t("media.downloadLabel", { label }) : label}
				className="touch-target flex h-24 w-32 items-center justify-center overflow-hidden rounded border border-rule bg-surface-2"
			>
				{item.kind === "image" && thumbnailUrl ? (
					<img src={thumbnailUrl} alt="" aria-hidden="true" onError={failed} className="h-full w-full object-cover" />
				) : item.kind === "video" ? (
					<PlayTile />
				) : (
					<DocumentTile format={item.format} />
				)}
			</button>
			<span className="truncate font-sans text-xs text-ink-muted" title={label}>
				{label}
			</span>
			{staff && <VisibilityLabel item={item} />}
			{onHide && <HideConfirm onHide={onHide} />}
			{onShow && <ShowButton onShow={onShow} />}
		</div>
	)
}

/**
 * A two-step confirm before hiding, on both pages (REQ-MED-035, REQ-MED-036):
 * "Hide from the public" opens "Hide this from everyone?" with confirm/keep.
 */
function HideConfirm({ onHide }: { onHide: () => void }) {
	const { t } = useLocale()
	const [confirming, setConfirming] = useState(false)

	if (!confirming) {
		return (
			<button
				type="button"
				className="touch-target inline-flex items-center rounded border border-rule px-2 font-sans text-xs text-ink"
				onClick={() => setConfirming(true)}
			>
				{t("reports.attachment.hide")}
			</button>
		)
	}

	return (
		<div className="flex flex-col gap-1">
			<span className="font-sans text-xs text-ink">{t("media.confirmHide")}</span>
			<div className="flex gap-1">
				<button
					type="button"
					className="touch-target inline-flex items-center rounded bg-brand-700 px-2 font-sans text-xs font-medium text-ink-inverse"
					onClick={() => {
						setConfirming(false)
						onHide()
					}}
				>
					{t("media.hide")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-2 font-sans text-xs text-ink"
					onClick={() => setConfirming(false)}
				>
					{t("media.keep")}
				</button>
			</div>
		</div>
	)
}

function ShowButton({ onShow }: { onShow: () => void }) {
	const { t } = useLocale()
	return (
		<button
			type="button"
			className="touch-target inline-flex items-center rounded border border-rule px-2 font-sans text-xs text-ink"
			onClick={onShow}
		>
			{t("reports.attachment.show")}
		</button>
	)
}

function PlayTile() {
	return (
		<svg aria-hidden="true" viewBox="0 0 24 24" className="h-8 w-8 text-ink-muted" fill="currentColor">
			<path d="M8 5v14l11-7z" />
		</svg>
	)
}

function DocumentTile({ format }: { format: string | null }) {
	return (
		<span className="flex flex-col items-center gap-1">
			<svg
				aria-hidden="true"
				viewBox="0 0 24 24"
				className="h-8 w-8 text-ink-muted"
				fill="none"
				stroke="currentColor"
				strokeWidth="1.5"
				strokeLinecap="round"
				strokeLinejoin="round"
			>
				<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
				<path d="M14 3v5h5" />
			</svg>
			<span className="font-sans text-[10px] uppercase text-ink-muted">{format}</span>
		</span>
	)
}

/** The one link a thumbnail or the lightbox needs for an image or video (issue #427 decision 11). */
export async function linkFor(reportId: string, item: StripItem, staff: boolean): Promise<string> {
	if (!staff || item.visibility === "public") {
		const link = await fetchMediaLink(reportId, item.id)
		return link.url
	}

	const link = await attachmentLink(reportId, {
		id: item.id,
		kind: item.kind,
		state: item.state,
		visibility: item.visibility ?? "private",
		format: item.format,
	})
	return link.url
}
