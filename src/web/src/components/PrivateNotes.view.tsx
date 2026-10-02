import { useLocale } from "../i18n/useLocale"
import type { PrivateAttachment, PrivateNote, PrivateNotesProps } from "./PrivateNotes"
import { PrivateNoteComposer } from "./PrivateNoteComposer"
import type { SaveNote } from "./PrivateNoteComposer"
import { PrivateNoteItem } from "./PrivateNoteItem"

export type PrivateNotesViewProps = PrivateNotesProps & {
	attachments: PrivateAttachment[]
	notes: PrivateNote[] | null
	failed: boolean
	error: string | null
	format: (value: string) => string
	add: SaveNote
	edit: (note: PrivateNote, text: string, attachmentId: string | null) =>
		Promise<boolean>
	remove: (note: PrivateNote) =>
		Promise<boolean>
}

export function PrivateNotesView({ reportId, attachments, notes, failed, error, format, add, edit, remove }: PrivateNotesViewProps) {
	const { t } = useLocale()

	return (
		<section aria-labelledby="private-notes-heading" className="mt-10" data-private-notes>
			<h2 id="private-notes-heading" className="font-display text-2xl font-bold">
				{t("privateNotes.title")}
			</h2>
			<p className="mt-2 font-sans text-sm text-ink-muted">{t("privateNotes.explanation")}</p>

			{error && (
				<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			<PrivateNoteComposer attachments={attachments} onSave={add} />

			{failed ? (
				<p className="mt-6 font-sans text-ink-muted" data-private-notes-failed>
					{t("privateNotes.error.load")}
				</p>
			) : !notes ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateNotes.loading")}</p>
			) : notes.length === 0 ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateNotes.empty")}</p>
			) : (
				<ol aria-label={t("privateNotes.listLabel")} className="mt-6 flex flex-col gap-4">
					{notes.map((note) => (
						<PrivateNoteItem
							key={note.id}
							reportId={reportId}
							note={note}
							attachments={attachments}
							format={format}
							onEdit={(text, attachmentId) => edit(note, text, attachmentId)}
							onRemove={() => remove(note)}
						/>
					))}
				</ol>
			)}
		</section>
	)
}
