import { useLocale } from "../i18n/useLocale"
import type { PrivateAttachmentItemModel, PrivateAttachmentItemProps } from "./PrivateAttachmentItem"
import { PRIMARY, SECONDARY } from "./privateAttachmentStyles"

export type PrivateAttachmentItemViewProps = PrivateAttachmentItemProps & PrivateAttachmentItemModel

/** One private attachment: name, size, description, who added it and when, with Download and a confirmed Remove. */
export function PrivateAttachmentItemView({
	attachment,
	size,
	addedAt,
	onDownload,
	confirming,
	onAskRemove,
	onKeep,
	onConfirmRemove,
}: PrivateAttachmentItemViewProps) {
	const { t } = useLocale()

	return (
		<li data-private-attachment-id={attachment.id} className="rounded border border-rule bg-surface p-4">
			<p className="flex flex-wrap items-baseline gap-x-3 gap-y-1 font-sans">
				<span className="break-all font-medium text-ink" data-private-attachment-name>
					{attachment.fileName}
				</span>
				<span className="text-sm text-ink-muted" data-private-attachment-size>
					{size}
				</span>
			</p>
			{attachment.description && (
				<p data-private-attachment-description className="mt-2 whitespace-pre-line break-words font-sans text-ink">
					{attachment.description}
				</p>
			)}
			<p className="mt-2 font-sans text-sm text-ink-muted" data-private-attachment-added>
				{t("privateAttachments.added", {
					adder: attachment.isMine ? t("privateAttachments.author.you") : attachment.addedBy,
					at: addedAt,
				})}
			</p>

			<div className="mt-3 flex flex-wrap items-center gap-3">
				{confirming ? (
					<>
						<span className="font-sans text-sm text-ink">{t("privateAttachments.confirmRemove")}</span>
						<button type="button" className={PRIMARY} onClick={onConfirmRemove}>
							{t("privateAttachments.remove")}
						</button>
						<button type="button" className={SECONDARY} onClick={onKeep}>
							{t("privateAttachments.keep")}
						</button>
					</>
				) : (
					<>
						<button type="button" className={SECONDARY} onClick={onDownload}>
							{t("privateAttachments.download")}
						</button>
						<button type="button" className={SECONDARY} onClick={onAskRemove}>
							{t("privateAttachments.remove")}
						</button>
					</>
				)}
			</div>
		</li>
	)
}
