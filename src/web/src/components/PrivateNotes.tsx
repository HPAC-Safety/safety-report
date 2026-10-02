import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import {
	addPrivateNote,
	editPrivateNote,
	listPrivateNotes,
	removePrivateNote,
	STALE_PRIVATE_NOTE,
	type PrivateAttachment,
	type PrivateNote,
} from "../api/adminReports"
import { PrivateNotesView } from "./PrivateNotes.view"

export type { PrivateAttachment, PrivateNote } from "../api/adminReports"

/*
 * Staff-only notes on one report (ADR-0133, REQ-MOD-106). Only a safety
 * officer or administrator reaches this page, and the API refuses everyone
 * else, so these controls are convenience, not the boundary. A note is plain
 * text shown exactly as typed; each edit is a new revision, and its history
 * shows every one. Newest note first.
 */
// Split across lines on purpose: tools/web/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
type Task = () =>
	Promise<unknown>

export interface PrivateNotesProps {
	reportId: string
	attachments?: PrivateAttachment[]
}

/*
 * A note may refer to one of the report's private attachments (ADR-0135). The
 * reference belongs to the revision: an edit may keep, change, or drop it.
 */
export function usePrivateNotes({ reportId, attachments = [] }: PrivateNotesProps) {
	const { t, locale } = useLocale()
	const [notes, setNotes] = useState<PrivateNote[] | null>(null)
	const [failed, setFailed] = useState(false)
	const [error, setError] = useState<string | null>(null)

	const load = useCallback(() => {
		listPrivateNotes(reportId)
			.then((loaded) => {
				// Anything but a list is a failed read, never an empty one.
				if (!Array.isArray(loaded)) throw new Error("not a list")
				setNotes(loaded)
				setFailed(false)
			})
			.catch(() => setFailed(true))
	}, [reportId])

	useEffect(load, [load])

	async function run(action: Task) {
		setError(null)
		try {
			await action()
			load()
			return true
		} catch (cause) {
			setError(
				cause instanceof ApiError && cause.type === STALE_PRIVATE_NOTE
					? t("privateNotes.error.stale")
					: t("privateNotes.error.save"),
			)
			load()
			return false
		}
	}

	const at = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })

	return {
		attachments,
		notes,
		failed,
		error,
		format: (value: string) => at.format(new Date(value)),
		add: (text: string, attachmentId: string | null) => run(() => addPrivateNote(reportId, text, attachmentId)),
		edit: (note: PrivateNote, text: string, attachmentId: string | null) =>
			run(() => editPrivateNote(reportId, note, text, attachmentId)),
		remove: (note: PrivateNote) => run(() => removePrivateNote(reportId, note.id)),
	}
}

export function PrivateNotes(props: PrivateNotesProps) {
	return <PrivateNotesView {...props} {...usePrivateNotes(props)} />
}
