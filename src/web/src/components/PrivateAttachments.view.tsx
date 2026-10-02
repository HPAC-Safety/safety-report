import { useLocale } from "../i18n/useLocale"
import { PrivateAttachmentItem } from "./PrivateAttachmentItem"
import { PrivateAttachmentStaging } from "./PrivateAttachmentStaging"
import type { PrivateAttachmentsModel, PrivateAttachmentsProps } from "./PrivateAttachments"

export type PrivateAttachmentsViewProps = PrivateAttachmentsProps & PrivateAttachmentsModel

/** The private-attachments section: its heading, the staging area, and the newest-first list. */
export function PrivateAttachmentsView({ reportId, error, attachments, failed, reload, rows }: PrivateAttachmentsViewProps) {
	const { t } = useLocale()

	return (
		<section aria-labelledby="private-attachments-heading" className="mt-10" data-private-attachments>
			<h2 id="private-attachments-heading" className="font-display text-2xl font-bold">
				{t("privateAttachments.title")}
			</h2>
			<p className="mt-2 font-sans text-sm text-ink-muted">{t("privateAttachments.explanation")}</p>

			{error && (
				<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			<PrivateAttachmentStaging reportId={reportId} onAdded={reload} />

			{failed ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateAttachments.error.load")}</p>
			) : !attachments ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateAttachments.loading")}</p>
			) : attachments.length === 0 ? (
				<p className="mt-6 font-sans text-ink-muted" data-private-attachments-empty>
					{t("privateAttachments.empty")}
				</p>
			) : (
				<ol aria-label={t("privateAttachments.listLabel")} className="mt-6 flex flex-col gap-4">
					{rows.map(({ attachment, size, addedAt, onDownload, onRemove }) => (
						<PrivateAttachmentItem
							key={attachment.id}
							attachment={attachment}
							size={size}
							addedAt={addedAt}
							onDownload={onDownload}
							onRemove={onRemove}
						/>
					))}
				</ol>
			)}
		</section>
	)
}
