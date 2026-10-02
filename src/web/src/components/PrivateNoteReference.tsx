import { useState } from "react"
import type { PrivateNoteAttachment } from "../api/adminReports"
import { downloadPrivateAttachment } from "./PrivateAttachments"
import { PrivateNoteReferenceView } from "./PrivateNoteReference.view"

export type { PrivateNoteAttachment } from "../api/adminReports"

export interface PrivateNoteReferenceProps {
	reportId: string
	attachment: PrivateNoteAttachment | null
}

/** The view model of the private attachment a note or revision refers to. */
export function usePrivateNoteReference({ reportId }: PrivateNoteReferenceProps) {
	const [failed, setFailed] = useState(false)

	function download(attachmentId: string) {
		setFailed(false)
		downloadPrivateAttachment(reportId, attachmentId).catch(() => setFailed(true))
	}

	return { failed, download }
}

export function PrivateNoteReference(props: PrivateNoteReferenceProps) {
	return <PrivateNoteReferenceView {...props} {...usePrivateNoteReference(props)} />
}
