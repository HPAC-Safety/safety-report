import { useLocale } from "../i18n/useLocale"
import type { PrivateNoteItemProps, PrivateNoteRevision } from "./PrivateNoteItem"
import { PrivateNoteComposer } from "./PrivateNoteComposer"
import type { SaveNote } from "./PrivateNoteComposer"
import { PrivateNoteReference } from "./PrivateNoteReference"

const SECONDARY = "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
const PRIMARY =
	"touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse disabled:opacity-60"

export type PrivateNoteItemViewProps = PrivateNoteItemProps & {
	editing: boolean
	confirming: boolean
	history: PrivateNoteRevision[] | null
	historyFailed: boolean
	editAttachment: string | null
	startEditing: () => void
	stopEditing: () => void
	askRemove: () => void
	keep: () => void
	confirmRemove: () => void
	toggleHistory: () => void
	saveEdit: SaveNote
}

export function PrivateNoteItemView({
	reportId,
	note,
	attachments,
	format,
	editing,
	confirming,
	history,
	historyFailed,
	editAttachment,
	startEditing,
	stopEditing,
	askRemove,
	keep,
	confirmRemove,
	toggleHistory,
	saveEdit,
}: PrivateNoteItemViewProps) {
	const { t } = useLocale()

	return (
		<li data-private-note-id={note.id} className="rounded border border-rule bg-surface p-4">
			<p className="flex flex-wrap items-baseline gap-x-3 gap-y-1 font-sans text-sm text-ink-muted">
				<span className="font-medium text-ink" data-private-note-writer>
					{note.isMine ? t("privateNotes.author.you") : note.writtenBy}
				</span>
				<span>{format(note.writtenAt)}</span>
				{note.edited && <span data-private-note-edited>{t("privateNotes.edited")}</span>}
			</p>

			{editing ? (
				<PrivateNoteComposer
					initial={note.text}
					initialAttachment={editAttachment}
					attachments={attachments}
					label={t("privateNotes.editLabel")}
					submitLabel={t("privateNotes.save")}
					onCancel={stopEditing}
					onSave={saveEdit}
				/>
			) : (
				<>
					<p data-private-note-text className="mt-2 whitespace-pre-line break-words font-sans text-ink">
						{note.text}
					</p>
					<PrivateNoteReference reportId={reportId} attachment={note.attachment} />
				</>
			)}

			{!editing && (
				<div className="mt-3 flex flex-wrap items-center gap-3">
					{confirming ? (
						<>
							<span className="font-sans text-sm text-ink">{t("privateNotes.confirmRemove")}</span>
							<button type="button" className={PRIMARY} onClick={confirmRemove}>
								{t("privateNotes.remove")}
							</button>
							<button type="button" className={SECONDARY} onClick={keep}>
								{t("privateNotes.keep")}
							</button>
						</>
					) : (
						<>
							<button type="button" className={SECONDARY} onClick={startEditing}>
								{t("privateNotes.edit")}
							</button>
							{note.edited && (
								<button type="button" className={SECONDARY} aria-expanded={history !== null} onClick={toggleHistory}>
									{history ? t("privateNotes.hideHistory") : t("privateNotes.history")}
								</button>
							)}
							<button type="button" className={SECONDARY} onClick={askRemove}>
								{t("privateNotes.remove")}
							</button>
						</>
					)}
				</div>
			)}

			{historyFailed && <p className="mt-3 font-sans text-sm text-ink-muted">{t("privateNotes.error.history")}</p>}

			{history && (
				<ol aria-label={t("privateNotes.historyLabel")} className="mt-4 flex flex-col gap-3 border-l-2 border-rule pl-4">
					{history.map((revision) => (
						<li key={revision.number} data-private-note-revision={revision.number}>
							<p className="font-sans text-xs text-ink-muted">
								{t("privateNotes.revision", {
									number: String(revision.number),
									writer: revision.isMine ? t("privateNotes.author.you") : revision.writtenBy,
									at: format(revision.writtenAt),
								})}
							</p>
							<p className="mt-1 whitespace-pre-line break-words font-sans text-sm text-ink">{revision.text}</p>
							<PrivateNoteReference reportId={reportId} attachment={revision.attachment} />
						</li>
					))}
				</ol>
			)}
		</li>
	)
}
