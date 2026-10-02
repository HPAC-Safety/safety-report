import { useState } from "react"
import type { PublicComment } from "../api/publicReports"
import type { SaveComment } from "./CommentComposer"
import { CommentItemView } from "./CommentItem.view"

export type { PublicComment } from "../api/publicReports"

// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
export type CommentAttempt = () =>
	Promise<boolean>

export interface CommentItemProps {
	comment: PublicComment
	locale: string
	canHide: boolean
	onEdit: SaveComment
	onDelete: CommentAttempt
	onHide: CommentAttempt
}

/** The view model of one comment: its language, its edit, and its delete or hide confirmation. */
export function useCommentItem({ comment, locale, onEdit, onDelete, onHide }: CommentItemProps) {
	const [editing, setEditing] = useState(false)
	const [confirming, setConfirming] = useState<"delete" | "hide" | null>(null)
	const at = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })

	const written = comment.locale === locale
	const translated = !written && comment.translatedText !== null

	return {
		editing,
		confirming,
		written,
		translated,
		shownText: translated ? comment.translatedText! : comment.text,
		shownLocale: translated ? locale : comment.locale,
		postedAt: at.format(new Date(comment.createdAt)),
		startEditing: () => setEditing(true),
		stopEditing: () => setEditing(false),
		askDelete: () => setConfirming("delete"),
		askHide: () => setConfirming("hide"),
		keep: () => setConfirming(null),
		confirmed: () => void (confirming === "delete" ? onDelete() : onHide()).then(() => setConfirming(null)),
		saveEdit: async (text: string) => {
			const saved = await onEdit(text)
			if (saved) setEditing(false)
			return saved
		},
	}
}

export function CommentItem(props: CommentItemProps) {
	return <CommentItemView {...props} {...useCommentItem(props)} />
}
