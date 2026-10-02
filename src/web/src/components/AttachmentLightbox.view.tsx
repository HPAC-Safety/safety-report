import { useLocale } from "../i18n/useLocale"
import { AttachmentLightboxMedia } from "./AttachmentLightboxMedia"
import type { AttachmentLightboxModel, AttachmentLightboxProps } from "./AttachmentLightbox"

export type AttachmentLightboxViewProps = AttachmentLightboxProps & AttachmentLightboxModel

/** The lightbox's markup: a native modal dialog around the open item, with previous, next and close controls. */
export function AttachmentLightboxView({
	getLink,
	invalidateLink,
	dialog,
	item,
	label,
	previous,
	next,
	close,
	onCancel,
	onKeyDown,
	onItemGone,
}: AttachmentLightboxViewProps) {
	const { t } = useLocale()

	if (!item) {
		return null
	}

	return (
		<dialog
			ref={dialog}
			aria-label={label}
			onCancel={onCancel}
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
				<AttachmentLightboxMedia
					key={item.id}
					item={item}
					label={label}
					getLink={getLink}
					invalidateLink={invalidateLink}
					onGone={onItemGone}
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
