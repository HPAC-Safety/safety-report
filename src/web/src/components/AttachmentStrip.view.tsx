import { useLocale } from "../i18n/useLocale"
import { AttachmentLightbox } from "./AttachmentLightbox"
import { AttachmentThumbnail } from "./AttachmentThumbnail"
import type { AttachmentStripModel, AttachmentStripProps } from "./AttachmentStrip"

export type AttachmentStripViewProps = AttachmentStripProps & AttachmentStripModel

/** The attachment strip's markup: a heading, an error, one thumbnail per item and the lightbox when one is open. */
export function AttachmentStripView({
	staff,
	items,
	error,
	lightboxId,
	lightboxItems,
	getLink,
	invalidateLink,
	remove,
	closeLightbox,
	rows,
}: AttachmentStripViewProps) {
	const { t } = useLocale()

	if (items.length === 0) {
		return null
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
				{rows.map(({ item, label, onActivate, onGone, onHide, onShow }) => (
					<li key={item.id} data-media={item.kind} className="flex-none snap-start">
						<AttachmentThumbnail
							item={item}
							label={label}
							staff={staff}
							getLink={getLink}
							invalidateLink={invalidateLink}
							onActivate={onActivate}
							onGone={onGone}
							onHide={onHide}
							onShow={onShow}
						/>
					</li>
				))}
			</ul>

			{lightboxId && (
				<AttachmentLightbox
					items={lightboxItems}
					openId={lightboxId}
					getLink={getLink}
					invalidateLink={invalidateLink}
					onClose={closeLightbox}
					onGone={remove}
				/>
			)}
		</section>
	)
}
