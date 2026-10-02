import { useState } from "react"
import { COMMENT_MAX_LENGTH } from "../api/publicReports"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { CommentComposerView } from "./CommentComposer.view"

// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
export type SaveComment = (text: string) =>
	Promise<boolean>

export interface CommentComposerProps {
	initial?: string
	submitLabel?: string
	onPost: SaveComment
	onCancel?: () => void
}

/** The view model of the form that writes or edits one comment. */
export function useCommentComposer({ initial = "", onPost, onCancel }: CommentComposerProps) {
	const [text, setText] = useState(initial)
	const [saving, setSaving] = useState(false)
	const length = text.trim().length
	const blank = length === 0
	const tooLong = length > COMMENT_MAX_LENGTH
	useUnsavedChangesGuard(text !== initial)

	async function submit(event: React.FormEvent) {
		event.preventDefault()
		if (blank || tooLong) return
		setSaving(true)
		const saved = await onPost(text)
		setSaving(false)
		if (saved && !onCancel) setText("")
	}

	return {
		text,
		length,
		tooLong,
		maxLength: COMMENT_MAX_LENGTH,
		submitDisabled: blank || tooLong || saving,
		changeText: setText,
		submit: (event: React.FormEvent) => void submit(event),
	}
}

export function CommentComposer(props: CommentComposerProps) {
	return <CommentComposerView {...props} {...useCommentComposer(props)} />
}
