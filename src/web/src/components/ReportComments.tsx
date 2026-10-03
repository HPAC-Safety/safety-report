import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import { deleteComment, editComment, hideComment, listComments, postComment, type PublicComment } from "../api/publicReports"
import { ReportCommentsView } from "./ReportComments.view"

export type { PublicComment } from "../api/publicReports"

/*
 * The comments on one published report (ADR-0114, REQ-COM-016..020).
 *
 * Anyone reads them, each labelled "Member", or "You" on the reader's own.
 * Each shows in the reader's language: as written, or the Worker's machine
 * translation with a way back to the original, or the original marked as
 * awaiting translation. A signed-in member writes, and edits or deletes their
 * own; a reviewer hides any. The API authorizes every one of those, so these
 * controls are convenience, not the boundary.
 */
// Split across lines on purpose: tools/web/check-hardcoded-strings.ts is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
type Task = () =>
	Promise<unknown>

export interface ReportCommentsProps {
	reportId: string
}

export function useReportComments({ reportId }: ReportCommentsProps) {
	const { t, locale } = useLocale()
	const { isSignedIn, role } = useAuth()
	const [comments, setComments] = useState<PublicComment[] | null>(null)
	const [failed, setFailed] = useState(false)
	const [error, setError] = useState<string | null>(null)

	const load = useCallback(() => {
		listComments(reportId)
			.then((loaded) => {
				setComments(loaded)
				setFailed(false)
			})
			.catch(() => setFailed(true))
	}, [reportId])

	// Reload when the reader signs in or out, so "You" follows the session.
	useEffect(load, [load, isSignedIn])

	const isReviewer = role === "safety_officer" || role === "administrator"

	async function run(action: Task) {
		setError(null)
		try {
			await action()
			load()
			return true
		} catch {
			setError(t("comments.error.save"))
			return false
		}
	}

	return {
		comments,
		failed,
		error,
		isSignedIn,
		canHide: isReviewer,
		post: (text: string) => run(() => postComment(reportId, text, locale)),
		edit: (comment: PublicComment, text: string) => run(() => editComment(reportId, comment.id, text, locale)),
		remove: (comment: PublicComment) => run(() => deleteComment(reportId, comment.id)),
		hide: (comment: PublicComment) => run(() => hideComment(comment.id)),
	}
}

export function ReportComments(props: ReportCommentsProps) {
	return <ReportCommentsView {...props} {...useReportComments(props)} />
}
