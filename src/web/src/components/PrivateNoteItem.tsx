import { useState } from "react"
import { privateNoteHistory, type PrivateNote, type PrivateNoteRevision } from "../api/adminReports"
import type { PrivateAttachment } from "./PrivateNoteComposer"
import type { SaveNote } from "./PrivateNoteComposer"
import { PrivateNoteItemView } from "./PrivateNoteItem.view"

export type { PrivateNote, PrivateNoteRevision } from "../api/adminReports"

// Split across lines on purpose: tools/web/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
export type RemoveNote = () =>
	Promise<boolean>

export interface PrivateNoteItemProps {
	reportId: string
	note: PrivateNote
	attachments: PrivateAttachment[]
	format: (value: string) => string
	onEdit: SaveNote
	onRemove: RemoveNote
}

/** The view model of one private note: its edit, remove confirmation, and revision history. */
export function usePrivateNoteItem({ reportId, note, onEdit, onRemove }: PrivateNoteItemProps) {
	const [editing, setEditing] = useState(false)
	const [confirming, setConfirming] = useState(false)
	const [history, setHistory] = useState<PrivateNoteRevision[] | null>(null)
	const [historyFailed, setHistoryFailed] = useState(false)

	async function toggleHistory() {
		if (history) {
			setHistory(null)
			return
		}
		setHistoryFailed(false)
		try {
			setHistory(await privateNoteHistory(reportId, note.id))
		} catch {
			setHistoryFailed(true)
		}
	}

	return {
		editing,
		confirming,
		history,
		historyFailed,
		editAttachment: note.attachment && !note.attachment.removed ? note.attachment.id : null,
		startEditing: () => setEditing(true),
		stopEditing: () => setEditing(false),
		askRemove: () => setConfirming(true),
		keep: () => setConfirming(false),
		confirmRemove: () => void onRemove().then(() => setConfirming(false)),
		toggleHistory: () => void toggleHistory(),
		saveEdit: async (text: string, attachmentId: string | null) => {
			const saved = await onEdit(text, attachmentId)
			if (saved) {
				setEditing(false)
				setHistory(null)
			}
			return saved
		},
	}
}

export function PrivateNoteItem(props: PrivateNoteItemProps) {
	return <PrivateNoteItemView {...props} {...usePrivateNoteItem(props)} />
}
