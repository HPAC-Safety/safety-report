import { useLocale } from "../i18n/useLocale"
import { AttachmentHideConfirm } from "./AttachmentHideConfirm"
import type { AttachmentThumbnailModel, AttachmentThumbnailProps } from "./AttachmentThumbnail"
import type { StripItem } from "./stripItems"

export type AttachmentThumbnailViewProps = AttachmentThumbnailProps & AttachmentThumbnailModel

/** One strip item: its thumbnail button, its label, and for staff its state or visibility with Hide or Show. */
export function AttachmentThumbnailView({ item, label, staff, onActivate, onHide, onShow, thumbnailUrl, failed }: AttachmentThumbnailViewProps) {
	const { t } = useLocale()

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
			{onHide && <AttachmentHideConfirm onHide={onHide} />}
			{onShow && <ShowButton onShow={onShow} />}
		</div>
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
