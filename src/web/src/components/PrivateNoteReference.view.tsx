import { useLocale } from "../i18n/useLocale"
import type { PrivateNoteReferenceProps } from "./PrivateNoteReference"

export type PrivateNoteReferenceViewProps = PrivateNoteReferenceProps & {
	failed: boolean
	download: (attachmentId: string) => void
}

/** The private attachment a note or revision refers to: a download, or its name marked removed. */
export function PrivateNoteReferenceView({ attachment, failed, download }: PrivateNoteReferenceViewProps) {
	const { t } = useLocale()

	if (!attachment) return null

	return (
		<p className="mt-2 flex flex-wrap items-center gap-2 font-sans text-sm text-ink-muted" data-private-note-attachment>
			<span>{t("privateNotes.refersTo")}</span>
			{attachment.removed ? (
				<span className="text-ink">{t("privateNotes.attachmentRemoved", { name: attachment.fileName })}</span>
			) : (
				<button
					type="button"
					className="touch-target inline-flex items-center font-medium text-brand-700 underline"
					onClick={() => download(attachment.id)}
				>
					{attachment.fileName}
				</button>
			)}
			{failed && <span role="alert">{t("privateAttachments.error.download")}</span>}
		</p>
	)
}
