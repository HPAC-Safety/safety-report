import { useEffect, useRef, useState } from "react"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { useLocale } from "../i18n/useLocale"
import {
	addPrivateAttachment,
	PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH,
	PRIVATE_ATTACHMENT_MAX_BYTES,
	PrivateUploadError,
	stagePrivateUpload,
} from "../api/adminReports"
import { byteSizeLabel } from "./byteSizeLabel"
import { PrivateAttachmentStagingView } from "./PrivateAttachmentStaging.view"

/** One file staged for this report: uploading, finished, or refused, with its own description. */
export interface StagedRow {
	key: string
	name: string
	size: number
	description: string
	status: "uploading" | "uploaded" | "rejected"
	progress: number
	uploadId?: string
	reason?: "too_large" | "empty" | "storage" | "network" | "claim"
	controller?: AbortController
}

export interface PrivateAttachmentStagingProps {
	reportId: string
	onAdded: () => void
}

let nextStagedKey = 0

/**
 * The view model of the staging area that replaced the single-file Add form
 * (issue 658): a drop zone matching the reporter's — dropped or chosen,
 * several files at once — where each file starts uploading the moment it is
 * staged, with its own progress, its own Cancel (while uploading) or Remove
 * (once settled) control, and its own description box once it has finished.
 * Removing a finished staged row calls no API; its bytes simply expire by the
 * quarantine lifecycle rule (ADR-0126). "Add N attachments" claims every
 * finished row, and stays disabled until every row has settled — finished or
 * failed. Leaving the page while any un-added row still holds a real upload
 * warns — a refused row has nothing to lose — through the shared
 * `useUnsavedChangesGuard` (issue no. 659).
 */
export function usePrivateAttachmentStaging({ reportId, onAdded }: PrivateAttachmentStagingProps) {
	const { t, locale } = useLocale()
	const formatSize = (bytes: number) => byteSizeLabel(bytes, locale)
	const [rows, setRows] = useState<StagedRow[]>([])
	const [adding, setAdding] = useState(false)
	const [problem, setProblem] = useState<string | null>(null)
	const rowsRef = useRef<StagedRow[]>([])
	rowsRef.current = rows

	const hasStaged = rows.length > 0
	// A refused row holds nothing to lose; only a live or finished upload warns.
	const hasUploads = rows.some((row) => row.status !== "rejected")
	const settling = rows.some((row) => row.status === "uploading")
	const finishedCount = rows.filter((row) => row.status === "uploaded").length
	const tooLong = rows.some(
		(row) => row.status === "uploaded" && row.description.trim().length > PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH,
	)

	useUnsavedChangesGuard(hasUploads)

	// Leaving the page abandons anything still uploading.
	useEffect(
		() => () => {
			for (const row of rowsRef.current) row.controller?.abort()
		},
		[],
	)

	function update(key: string, patch: Partial<StagedRow>) {
		setRows((current) => current.map((row) => (row.key === key ? { ...row, ...patch } : row)))
	}

	async function stage(file: File) {
		const key = `private-staged-${nextStagedKey++}`

		// Refused at once, without minting anything: the API would refuse the
		// same declaration.
		if (file.size > PRIVATE_ATTACHMENT_MAX_BYTES) {
			setRows((current) => [
				...current,
				{ key, name: file.name, size: file.size, description: "", status: "rejected", progress: 0, reason: "too_large" },
			])
			return
		}

		const controller = new AbortController()
		setRows((current) => [...current, { key, name: file.name, size: file.size, description: "", status: "uploading", progress: 0, controller }])

		try {
			const staged = await stagePrivateUpload(reportId, file, (fraction) => update(key, { progress: fraction }), controller.signal)
			update(key, { status: "uploaded", uploadId: staged.uploadId, controller: undefined })
		} catch (cause) {
			if (controller.signal.aborted) {
				// Cancel already removed the row (or is about to); nothing to mark.
				setRows((current) => current.filter((row) => row.key !== key))
				return
			}
			const reason = cause instanceof PrivateUploadError ? cause.reason : "network"
			update(key, { status: "rejected", reason: reason as StagedRow["reason"], controller: undefined })
		}
	}

	function cancel(row: StagedRow) {
		row.controller?.abort()
	}

	function remove(row: StagedRow) {
		// Decision: no new API for this. The row disappears; its bytes expire
		// by the existing 15-day quarantine lifecycle rule.
		setRows((current) => current.filter((candidate) => candidate.key !== row.key))
	}

	async function addAll() {
		setProblem(null)
		setAdding(true)
		let failed = false
		for (const row of rowsRef.current.filter((candidate) => candidate.status === "uploaded" && candidate.uploadId)) {
			try {
				await addPrivateAttachment(reportId, row.uploadId!, row.name, row.description)
				setRows((current) => current.filter((candidate) => candidate.key !== row.key))
			} catch {
				failed = true
				update(row.key, { status: "rejected", reason: "claim" })
			}
		}
		setAdding(false)
		if (failed) setProblem(t("privateAttachments.error.upload"))
		onAdded()
	}

	return {
		rows,
		adding,
		problem,
		hasStaged,
		formatSize,
		maxSize: formatSize(PRIVATE_ATTACHMENT_MAX_BYTES),
		descriptionMaxLength: PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH,
		addDisabled: settling || finishedCount === 0 || tooLong || adding,
		addAllLabel: finishedCount === 1 ? t("privateAttachments.addAll.one") : t("privateAttachments.addAll.other", { count: finishedCount }),
		onFiles(files: File[]) {
			for (const file of files) void stage(file)
		},
		onRowAction: (row: StagedRow) => (row.status === "uploading" ? cancel(row) : remove(row)),
		onDescription: (key: string, description: string) => update(key, { description }),
		onAddAll: () => void addAll(),
	}
}

export type PrivateAttachmentStagingModel = ReturnType<typeof usePrivateAttachmentStaging>

export function PrivateAttachmentStaging(props: PrivateAttachmentStagingProps) {
	return <PrivateAttachmentStagingView {...props} {...usePrivateAttachmentStaging(props)} />
}
