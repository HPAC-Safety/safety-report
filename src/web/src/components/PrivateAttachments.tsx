import { useCallback, useEffect, useRef, useState } from "react"
import { AttachmentDropZone } from "./AttachmentDropZone"
import { useLeaveWarning } from "../hooks/useLeaveWarning"
import { useLocale } from "../i18n/useLocale"
import {
	addPrivateAttachment,
	listPrivateAttachments,
	PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH,
	PRIVATE_ATTACHMENT_MAX_BYTES,
	privateAttachmentLink,
	PrivateUploadError,
	removePrivateAttachment,
	stagePrivateUpload,
	type PrivateAttachment,
} from "../api/adminReports"

/*
 * Staff-only files on one report (ADR-0135, REQ-MOD-115). Only a safety
 * officer or administrator reaches this page, and the API refuses everyone
 * else, so these controls are convenience, not the boundary. A file of any
 * type goes straight from the browser to storage; it is never previewed,
 * only downloaded, unchanged, under its own name. Newest first.
 */

// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
type Attempt = () =>
	Promise<void>

const SECONDARY = "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
const PRIMARY =
	"touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse disabled:opacity-60"

export interface PrivateAttachmentsState {
	attachments: PrivateAttachment[] | null
	failed: boolean
	reload: () => void
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

/** A byte count in the reader's language: "2.4 MB", "2,4 Mo". */
function useFormatSize() {
	const { locale } = useLocale()
	return (bytes: number) => {
		const units = ["byte", "kilobyte", "megabyte", "gigabyte"] as const
		let value = bytes
		let unit = 0
		while (value >= 1024 && unit !== units.length - 1) {
			value /= 1024
			unit += 1
		}
		return new Intl.NumberFormat(locale, {
			style: "unit",
			unit: units[unit],
			unitDisplay: "short",
			maximumFractionDigits: unit === 0 ? 0 : 1,
		}).format(value)
	}
}

export function PrivateAttachments({ reportId, state }: { reportId: string; state: PrivateAttachmentsState }) {
	const { t, locale } = useLocale()
	const [error, setError] = useState<string | null>(null)
	const formatSize = useFormatSize()
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

	return (
		<section aria-labelledby="private-attachments-heading" className="mt-10" data-private-attachments>
			<h2 id="private-attachments-heading" className="font-display text-2xl font-bold">
				{t("privateAttachments.title")}
			</h2>
			<p className="mt-2 font-sans text-sm text-ink-muted">{t("privateAttachments.explanation")}</p>

			{error && (
				<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			<PrivateAttachmentStaging reportId={reportId} onAdded={reload} />

			{failed ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateAttachments.error.load")}</p>
			) : !attachments ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateAttachments.loading")}</p>
			) : attachments.length === 0 ? (
				<p className="mt-6 font-sans text-ink-muted" data-private-attachments-empty>
					{t("privateAttachments.empty")}
				</p>
			) : (
				<ol aria-label={t("privateAttachments.listLabel")} className="mt-6 flex flex-col gap-4">
					{attachments.map((attachment) => (
						<AttachmentItem
							key={attachment.id}
							attachment={attachment}
							size={formatSize(attachment.byteSize)}
							addedAt={at.format(new Date(attachment.addedAt))}
							onDownload={() => void download(attachment)}
							onRemove={() => remove(attachment)}
						/>
					))}
				</ol>
			)}
		</section>
	)
}

function AttachmentItem({
	attachment,
	size,
	addedAt,
	onDownload,
	onRemove,
}: {
	attachment: PrivateAttachment
	size: string
	addedAt: string
	onDownload: () => void
	onRemove: Attempt
}) {
	const { t } = useLocale()
	const [confirming, setConfirming] = useState(false)

	return (
		<li data-private-attachment-id={attachment.id} className="rounded border border-rule bg-surface p-4">
			<p className="flex flex-wrap items-baseline gap-x-3 gap-y-1 font-sans">
				<span className="break-all font-medium text-ink" data-private-attachment-name>
					{attachment.fileName}
				</span>
				<span className="text-sm text-ink-muted" data-private-attachment-size>
					{size}
				</span>
			</p>
			{attachment.description && (
				<p data-private-attachment-description className="mt-2 whitespace-pre-line break-words font-sans text-ink">
					{attachment.description}
				</p>
			)}
			<p className="mt-2 font-sans text-sm text-ink-muted" data-private-attachment-added>
				{t("privateAttachments.added", {
					adder: attachment.isMine ? t("privateAttachments.author.you") : attachment.addedBy,
					at: addedAt,
				})}
			</p>

			<div className="mt-3 flex flex-wrap items-center gap-3">
				{confirming ? (
					<>
						<span className="font-sans text-sm text-ink">{t("privateAttachments.confirmRemove")}</span>
						<button type="button" className={PRIMARY} onClick={() => void onRemove().then(() => setConfirming(false))}>
							{t("privateAttachments.remove")}
						</button>
						<button type="button" className={SECONDARY} onClick={() => setConfirming(false)}>
							{t("privateAttachments.keep")}
						</button>
					</>
				) : (
					<>
						<button type="button" className={SECONDARY} onClick={onDownload}>
							{t("privateAttachments.download")}
						</button>
						<button type="button" className={SECONDARY} onClick={() => setConfirming(true)}>
							{t("privateAttachments.remove")}
						</button>
					</>
				)}
			</div>
		</li>
	)
}

/** One file staged for this report: uploading, finished, or refused, with its own description. */
interface StagedRow {
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

let nextStagedKey = 0

/**
 * The staging area that replaced the single-file Add form (issue #658): a
 * drop zone matching the reporter's — dropped or chosen, several files at
 * once — where each file starts uploading the moment it is staged, with its
 * own progress, its own Cancel (while uploading) or Remove (once settled)
 * control, and its own description box once it has finished. Removing a
 * finished staged row calls no API; its bytes simply expire by the
 * quarantine lifecycle rule (ADR-0126). "Add N attachments" claims every
 * finished row, and stays disabled until every row has settled — finished or
 * failed. Leaving the page with staged, un-added rows warns.
 */
function PrivateAttachmentStaging({ reportId, onAdded }: { reportId: string; onAdded: () => void }) {
	const { t } = useLocale()
	const formatSize = useFormatSize()
	const [rows, setRows] = useState<StagedRow[]>([])
	const [adding, setAdding] = useState(false)
	const [problem, setProblem] = useState<string | null>(null)
	const rowsRef = useRef<StagedRow[]>([])
	rowsRef.current = rows

	const hasStaged = rows.length > 0
	const settling = rows.some((row) => row.status === "uploading")
	const finishedCount = rows.filter((row) => row.status === "uploaded").length
	const tooLong = rows.some(
		(row) => row.status === "uploaded" && row.description.trim().length > PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH,
	)

	useLeaveWarning(hasStaged, t("privateAttachments.leaveWarning"))

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
				{ key, name: file.name, size: file.size, description: "", status: "rejected", progress: 0, reason: file.size === 0 ? "empty" : "too_large" },
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

	function choose(files: File[]) {
		for (const file of files) void stage(file)
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

	const addAllLabel = finishedCount === 1 ? t("privateAttachments.addAll.one") : t("privateAttachments.addAll.other", { count: finishedCount })

	return (
		<div className="mt-4">
			<label htmlFor="private-attachment-input" className="font-sans text-sm font-medium text-ink">
				{t("privateAttachments.fileLabel")}
			</label>
			<AttachmentDropZone
				fieldId="private-attachment-input"
				describedBy={undefined}
				guidanceId="private-attachment-guidance"
				guidance={t("privateAttachments.limit", { max: formatSize(PRIVATE_ATTACHMENT_MAX_BYTES) })}
				promptText={t("privateAttachments.dropPrompt")}
				onFiles={choose}
			/>

			{hasStaged && (
				<ul className="mt-3 divide-y divide-rule rounded border border-rule" aria-label={t("privateAttachments.stagedListLabel")}>
					{rows.map((row) => (
						<li key={row.key} className="flex flex-col gap-2 px-3 py-3">
							<div className="flex items-center justify-between gap-3">
								<div className="min-w-0">
									<p className="truncate font-sans text-sm text-ink">{row.name}</p>
									<p className="font-sans text-xs text-ink-muted">{formatSize(row.size)}</p>
								</div>
								<button
									type="button"
									className="touch-target shrink-0 rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
									aria-label={row.status === "uploading" ? t("privateAttachments.cancelNamed", { name: row.name }) : t("privateAttachments.removeStagedNamed", { name: row.name })}
									onClick={() => (row.status === "uploading" ? cancel(row) : remove(row))}
								>
									{row.status === "uploading" ? t("privateAttachments.cancelLabel") : t("privateAttachments.remove")}
								</button>
							</div>

							{row.status === "uploading" && (
								<div className="flex flex-wrap items-center gap-3" aria-busy="true">
									<progress
										max={1}
										value={row.progress}
										aria-label={t("privateAttachments.uploadingNamed", { name: row.name })}
										className="h-2 w-full max-w-xs"
									/>
									<span className="font-sans text-xs text-ink-muted" aria-live="polite">
										{t("privateAttachments.progress", { percent: String(Math.round(row.progress * 100)) })}
									</span>
								</div>
							)}

							{row.status === "rejected" && (
								<p role="alert" className="font-sans text-xs text-brand-700">
									{row.reason === "too_large" || row.reason === "empty"
										? t(`privateAttachments.error.${row.reason}`, { max: formatSize(PRIVATE_ATTACHMENT_MAX_BYTES) })
										: t("privateAttachments.error.upload")}
								</p>
							)}

							{row.status === "uploaded" && (
								<div>
									<label className="font-sans text-sm text-ink">
										{t("privateAttachments.descriptionLabel")}
										<textarea
											value={row.description}
											rows={2}
											onChange={(event) => update(row.key, { description: event.target.value })}
											className="mt-1 block w-full rounded border border-rule bg-surface p-2 font-sans text-sm font-normal text-ink"
										/>
									</label>
									<p
										className={`mt-1 font-sans text-xs ${
											row.description.trim().length > PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH ? "text-brand-700" : "text-ink-muted"
										}`}
										aria-live="polite"
									>
										{t("privateAttachments.descriptionLength", {
											count: String(row.description.trim().length),
											max: String(PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH),
										})}
									</p>
								</div>
							)}
						</li>
					))}
				</ul>
			)}

			{problem && (
				<p role="alert" className="mt-2 font-sans text-sm text-brand-700">
					{problem}
				</p>
			)}

			{hasStaged && (
				<div className="mt-3">
					<button type="button" disabled={settling || finishedCount === 0 || tooLong || adding} onClick={() => void addAll()} className={PRIMARY}>
						{addAllLabel}
					</button>
				</div>
			)}
		</div>
	)
}
