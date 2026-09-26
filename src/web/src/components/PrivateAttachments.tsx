import { useCallback, useEffect, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	listPrivateAttachments,
	PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH,
	PRIVATE_ATTACHMENT_MAX_BYTES,
	privateAttachmentLink,
	PrivateUploadError,
	removePrivateAttachment,
	uploadPrivateAttachment,
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

			<AddForm reportId={reportId} onAdded={reload} />

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

function AddForm({ reportId, onAdded }: { reportId: string; onAdded: () => void }) {
	const { t } = useLocale()
	const formatSize = useFormatSize()
	const [file, setFile] = useState<File | null>(null)
	const [description, setDescription] = useState("")
	const [progress, setProgress] = useState<number | null>(null)
	const [problem, setProblem] = useState<string | null>(null)
	const controller = useRef<AbortController | null>(null)
	const input = useRef<HTMLInputElement>(null)

	const tooLarge = file !== null && file.size > PRIVATE_ATTACHMENT_MAX_BYTES
	const tooLong = description.trim().length > PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH
	const uploading = progress !== null

	useEffect(() => () => controller.current?.abort(), [])

	async function submit(event: React.FormEvent) {
		event.preventDefault()
		if (!file || tooLarge || tooLong || uploading) return

		setProblem(null)
		setProgress(0)
		const abort = new AbortController()
		controller.current = abort

		try {
			await uploadPrivateAttachment(reportId, file, description, setProgress, abort.signal)
			setFile(null)
			setDescription("")
			if (input.current) input.current.value = ""
			onAdded()
		} catch (cause) {
			if (abort.signal.aborted) {
				setProblem(t("privateAttachments.cancelled"))
			} else if (cause instanceof PrivateUploadError && (cause.reason === "too_large" || cause.reason === "empty")) {
				setProblem(t(`privateAttachments.error.${cause.reason}`, { max: formatSize(PRIVATE_ATTACHMENT_MAX_BYTES) }))
			} else {
				setProblem(t("privateAttachments.error.upload"))
			}
		} finally {
			setProgress(null)
			controller.current = null
		}
	}

	return (
		<form className="mt-4 flex flex-col gap-3" onSubmit={(event) => void submit(event)}>
			<label className="font-sans text-sm font-medium text-ink">
				{t("privateAttachments.fileLabel")}
				<input
					ref={input}
					type="file"
					disabled={uploading}
					onChange={(event) => {
						setProblem(null)
						setFile(event.target.files?.[0] ?? null)
					}}
					className="mt-2 block w-full font-sans text-base font-normal text-ink"
				/>
			</label>
			<p className="font-sans text-sm text-ink-muted">
				{t("privateAttachments.limit", { max: formatSize(PRIVATE_ATTACHMENT_MAX_BYTES) })}
			</p>
			{tooLarge && (
				<p className="font-sans text-sm text-brand-700">
					{t("privateAttachments.error.too_large", { max: formatSize(PRIVATE_ATTACHMENT_MAX_BYTES) })}
				</p>
			)}

			<label className="font-sans text-sm font-medium text-ink">
				{t("privateAttachments.descriptionLabel")}
				<textarea
					value={description}
					disabled={uploading}
					onChange={(event) => setDescription(event.target.value)}
					rows={2}
					className="mt-2 block w-full rounded border border-rule bg-surface p-3 font-sans text-base font-normal text-ink"
				/>
			</label>
			<p className={`font-sans text-sm ${tooLong ? "text-brand-700" : "text-ink-muted"}`} aria-live="polite">
				{t("privateAttachments.descriptionLength", {
					count: String(description.trim().length),
					max: String(PRIVATE_ATTACHMENT_DESCRIPTION_MAX_LENGTH),
				})}
			</p>

			{uploading && (
				<div className="flex flex-wrap items-center gap-3" data-private-attachment-progress>
					<progress
						max={1}
						value={progress}
						aria-label={t("privateAttachments.progressLabel")}
						className="h-2 w-full max-w-xs"
					/>
					<span className="font-sans text-sm text-ink-muted" aria-live="polite">
						{t("privateAttachments.progress", { percent: String(Math.round((progress ?? 0) * 100)) })}
					</span>
					<button type="button" className={SECONDARY} onClick={() => controller.current?.abort()}>
						{t("privateAttachments.cancel")}
					</button>
				</div>
			)}

			{problem && (
				<p role="alert" className="font-sans text-sm text-brand-700">
					{problem}
				</p>
			)}

			<div>
				<button type="submit" disabled={!file || tooLarge || tooLong || uploading} className={PRIMARY}>
					{t("privateAttachments.add")}
				</button>
			</div>
		</form>
	)
}
