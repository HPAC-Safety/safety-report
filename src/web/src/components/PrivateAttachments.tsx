import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	listPrivateAttachments,
	privateAttachmentLink,
	removePrivateAttachment,
	type PrivateAttachment,
} from "../api/adminReports"
import { byteSizeLabel } from "./byteSizeLabel"
import { PrivateAttachmentsView } from "./PrivateAttachments.view"

/*
 * Staff-only files on one report (ADR-0135, REQ-MOD-115). Only a safety
 * officer or administrator reaches this page, and the API refuses everyone
 * else, so these controls are convenience, not the boundary. A file of any
 * type goes straight from the browser to storage; it is never previewed,
 * only downloaded, unchanged, under its own name. Newest first.
 */

export interface PrivateAttachmentsState {
	attachments: PrivateAttachment[] | null
	failed: boolean
	reload: () => void
}

export interface PrivateAttachmentsProps {
	reportId: string
	state: PrivateAttachmentsState
}

/** Loads a report's private attachments, shared by this section and the private notes that refer to them. */
export function usePrivateAttachments(reportId: string): PrivateAttachmentsState {
	const [attachments, setAttachments] = useState<PrivateAttachment[] | null>(null)
	const [failed, setFailed] = useState(false)

	const reload = useCallback(() => {
		listPrivateAttachments(reportId)
			.then((loaded) => {
				// Anything but a list is a failed read, never an empty one.
				if (!Array.isArray(loaded)) throw new Error("not a list")
				setAttachments(loaded)
				setFailed(false)
			})
			.catch(() => setFailed(true))
	}, [reportId])

	useEffect(reload, [reload])

	return { attachments, failed, reload }
}

/** Starts a forced download of one private attachment through its short-lived link. */
export async function downloadPrivateAttachment(reportId: string, attachmentId: string) {
	const link = await privateAttachmentLink(reportId, attachmentId)
	const anchor = document.createElement("a")
	anchor.href = link.url
	anchor.download = link.fileName
	anchor.rel = "noopener"
	document.body.append(anchor)
	anchor.click()
	anchor.remove()
}

/**
 * The view model of the section: the download and remove actions with their
 * error, and each attachment's size and date in the reader's language. The
 * attachments themselves come from `usePrivateAttachments`, which the report
 * page calls so the private notes can share them.
 */
export function usePrivateAttachmentsSection({ reportId, state }: PrivateAttachmentsProps) {
	const { t, locale } = useLocale()
	const [error, setError] = useState<string | null>(null)
	const at = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })
	const { attachments, failed, reload } = state

	async function download(attachment: PrivateAttachment) {
		setError(null)
		try {
			await downloadPrivateAttachment(reportId, attachment.id)
		} catch {
			setError(t("privateAttachments.error.download"))
		}
	}

	async function remove(attachment: PrivateAttachment) {
		setError(null)
		try {
			await removePrivateAttachment(reportId, attachment.id)
		} catch {
			setError(t("privateAttachments.error.remove"))
		}
		reload()
	}

	return {
		error,
		attachments,
		failed,
		reload,
		rows: (attachments ?? []).map((attachment) => ({
			attachment,
			size: byteSizeLabel(attachment.byteSize, locale),
			addedAt: at.format(new Date(attachment.addedAt)),
			onDownload: () => void download(attachment),
			onRemove: () => remove(attachment),
		})),
	}
}

export type PrivateAttachmentsModel = ReturnType<typeof usePrivateAttachmentsSection>

export function PrivateAttachments(props: PrivateAttachmentsProps) {
	return <PrivateAttachmentsView {...props} {...usePrivateAttachmentsSection(props)} />
}
