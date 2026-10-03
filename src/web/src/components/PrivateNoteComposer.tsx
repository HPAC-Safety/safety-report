import { useState } from "react"
import { PRIVATE_NOTE_MAX_LENGTH, type PrivateAttachment } from "../api/adminReports"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { PrivateNoteComposerView } from "./PrivateNoteComposer.view"

export type { PrivateAttachment } from "../api/adminReports"

// Split across lines on purpose: tools/web/check-hardcoded-strings.ts is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
export type SaveNote = (text: string, attachmentId: string | null) =>
	Promise<boolean>

export interface PrivateNoteComposerProps {
	initial?: string
	initialAttachment?: string | null
	attachments: PrivateAttachment[]
	label?: string
	submitLabel?: string
	onSave: SaveNote
	onCancel?: () => void
}

/** The view model of the form that writes or edits one private note. */
export function usePrivateNoteComposer({
	initial = "",
	initialAttachment = null,
	onSave,
	onCancel,
}: PrivateNoteComposerProps) {
	const [text, setText] = useState(initial)
	const [attachmentId, setAttachmentId] = useState<string | null>(initialAttachment)
	const [saving, setSaving] = useState(false)
	const length = text.trim().length
	const blank = length === 0
	const tooLong = length > PRIVATE_NOTE_MAX_LENGTH
	useUnsavedChangesGuard(text !== initial || attachmentId !== initialAttachment)

	async function submit(event: React.FormEvent) {
		event.preventDefault()
		if (blank || tooLong) return
		setSaving(true)
		const saved = await onSave(text, attachmentId)
		setSaving(false)
		if (saved && !onCancel) {
			setText("")
			setAttachmentId(null)
		}
	}

	return {
		text,
		attachmentId,
		length,
		tooLong,
		maxLength: PRIVATE_NOTE_MAX_LENGTH,
		submitDisabled: blank || tooLong || saving,
		changeText: setText,
		changeAttachment: (value: string) => setAttachmentId(value || null),
		submit: (event: React.FormEvent) => void submit(event),
	}
}

export function PrivateNoteComposer(props: PrivateNoteComposerProps) {
	return <PrivateNoteComposerView {...props} {...usePrivateNoteComposer(props)} />
}
