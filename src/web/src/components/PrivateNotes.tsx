import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import { ApiError } from "../api/adminQuestions"
import {
	addPrivateNote,
	editPrivateNote,
	listPrivateNotes,
	PRIVATE_NOTE_MAX_LENGTH,
	privateNoteHistory,
	removePrivateNote,
	STALE_PRIVATE_NOTE,
	type PrivateNote,
	type PrivateNoteRevision,
} from "../api/adminReports"

/*
 * Staff-only notes on one report (ADR-0133, REQ-MOD-106). Only a safety
 * officer or administrator reaches this page, and the API refuses everyone
 * else, so these controls are convenience, not the boundary. A note is plain
 * text shown exactly as typed; each edit is a new revision, and its history
 * shows every one. Newest note first.
 */
// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
type Task = () =>
	Promise<unknown>
type Save = (text: string) =>
	Promise<boolean>
type Attempt = () =>
	Promise<boolean>

const SECONDARY = "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
const PRIMARY =
	"touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse disabled:opacity-60"

export function PrivateNotes({ reportId }: { reportId: string }) {
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

	return (
		<section aria-labelledby="private-notes-heading" className="mt-10" data-private-notes>
			<h2 id="private-notes-heading" className="font-display text-2xl font-bold">
				{t("privateNotes.title")}
			</h2>
			<p className="mt-2 font-sans text-sm text-ink-muted">{t("privateNotes.explanation")}</p>

			{error && (
				<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			<Composer onSave={(text) => run(() => addPrivateNote(reportId, text))} />

			{failed ? (
				<p className="mt-6 font-sans text-ink-muted" data-private-notes-failed>
					{t("privateNotes.error.load")}
				</p>
			) : !notes ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateNotes.loading")}</p>
			) : notes.length === 0 ? (
				<p className="mt-6 font-sans text-ink-muted">{t("privateNotes.empty")}</p>
			) : (
				<ol aria-label={t("privateNotes.listLabel")} className="mt-6 flex flex-col gap-4">
					{notes.map((note) => (
						<NoteItem
							key={note.id}
							reportId={reportId}
							note={note}
							format={(value) => at.format(new Date(value))}
							onEdit={(text) => run(() => editPrivateNote(reportId, note, text))}
							onRemove={() => run(() => removePrivateNote(reportId, note.id))}
						/>
					))}
				</ol>
			)}
		</section>
	)
}

function NoteItem({
	reportId,
	note,
	format,
	onEdit,
	onRemove,
}: {
	reportId: string
	note: PrivateNote
	format: (value: string) => string
	onEdit: Save
	onRemove: Attempt
}) {
	const { t } = useLocale()
	const [editing, setEditing] = useState(false)
	const [confirming, setConfirming] = useState(false)
	const [history, setHistory] = useState<PrivateNoteRevision[] | null>(null)
	const [historyFailed, setHistoryFailed] = useState(false)

	async function toggleHistory() {
		if (history) {
			setHistory(null)
			return
		}
		setHistoryFailed(false)
		try {
			setHistory(await privateNoteHistory(reportId, note.id))
		} catch {
			setHistoryFailed(true)
		}
	}

	return (
		<li data-private-note-id={note.id} className="rounded border border-rule bg-surface p-4">
			<p className="flex flex-wrap items-baseline gap-x-3 gap-y-1 font-sans text-sm text-ink-muted">
				<span className="font-medium text-ink" data-private-note-writer>
					{note.isMine ? t("privateNotes.author.you") : note.writtenBy}
				</span>
				<span>{format(note.writtenAt)}</span>
				{note.edited && <span data-private-note-edited>{t("privateNotes.edited")}</span>}
			</p>

			{editing ? (
				<Composer
					initial={note.text}
					label={t("privateNotes.editLabel")}
					submitLabel={t("privateNotes.save")}
					onCancel={() => setEditing(false)}
					onSave={async (text) => {
						const saved = await onEdit(text)
						if (saved) {
							setEditing(false)
							setHistory(null)
						}
						return saved
					}}
				/>
			) : (
				<p data-private-note-text className="mt-2 whitespace-pre-line break-words font-sans text-ink">
					{note.text}
				</p>
			)}

			{!editing && (
				<div className="mt-3 flex flex-wrap items-center gap-3">
					{confirming ? (
						<>
							<span className="font-sans text-sm text-ink">{t("privateNotes.confirmRemove")}</span>
							<button
								type="button"
								className={PRIMARY}
								onClick={() => void onRemove().then(() => setConfirming(false))}
							>
								{t("privateNotes.remove")}
							</button>
							<button type="button" className={SECONDARY} onClick={() => setConfirming(false)}>
								{t("privateNotes.keep")}
							</button>
						</>
					) : (
						<>
							<button type="button" className={SECONDARY} onClick={() => setEditing(true)}>
								{t("privateNotes.edit")}
							</button>
							{note.edited && (
								<button
									type="button"
									className={SECONDARY}
									aria-expanded={history !== null}
									onClick={() => void toggleHistory()}
								>
									{history ? t("privateNotes.hideHistory") : t("privateNotes.history")}
								</button>
							)}
							<button type="button" className={SECONDARY} onClick={() => setConfirming(true)}>
								{t("privateNotes.remove")}
							</button>
						</>
					)}
				</div>
			)}

			{historyFailed && <p className="mt-3 font-sans text-sm text-ink-muted">{t("privateNotes.error.history")}</p>}

			{history && (
				<ol aria-label={t("privateNotes.historyLabel")} className="mt-4 flex flex-col gap-3 border-l-2 border-rule pl-4">
					{history.map((revision) => (
						<li key={revision.number} data-private-note-revision={revision.number}>
							<p className="font-sans text-xs text-ink-muted">
								{t("privateNotes.revision", {
									number: String(revision.number),
									writer: revision.isMine ? t("privateNotes.author.you") : revision.writtenBy,
									at: format(revision.writtenAt),
								})}
							</p>
							<p className="mt-1 whitespace-pre-line break-words font-sans text-sm text-ink">{revision.text}</p>
						</li>
					))}
				</ol>
			)}
		</li>
	)
}

function Composer({
	initial = "",
	label,
	submitLabel,
	onSave,
	onCancel,
}: {
	initial?: string
	label?: string
	submitLabel?: string
	onSave: Save
	onCancel?: () => void
}) {
	const { t } = useLocale()
	const [text, setText] = useState(initial)
	const [saving, setSaving] = useState(false)
	const length = text.trim().length
	const blank = length === 0
	const tooLong = length > PRIVATE_NOTE_MAX_LENGTH

	async function submit(event: React.FormEvent) {
		event.preventDefault()
		if (blank || tooLong) return
		setSaving(true)
		const saved = await onSave(text)
		setSaving(false)
		if (saved && !onCancel) setText("")
	}

	return (
		<form className="mt-4 flex flex-col gap-2" onSubmit={(event) => void submit(event)}>
			<label className="font-sans text-sm font-medium text-ink">
				{label ?? t("privateNotes.newLabel")}
				<textarea
					value={text}
					onChange={(event) => setText(event.target.value)}
					rows={3}
					className="mt-2 block w-full rounded border border-rule bg-surface p-3 font-sans text-base font-normal text-ink"
				/>
			</label>
			<p className={`font-sans text-sm ${tooLong ? "text-brand-700" : "text-ink-muted"}`} aria-live="polite">
				{t("privateNotes.length", { count: String(length), max: String(PRIVATE_NOTE_MAX_LENGTH) })}
			</p>
			<div className="flex flex-wrap gap-3">
				<button type="submit" disabled={blank || tooLong || saving} className={PRIMARY}>
					{submitLabel ?? t("privateNotes.add")}
				</button>
				{onCancel && (
					<button type="button" className={SECONDARY} onClick={onCancel}>
						{t("privateNotes.cancel")}
					</button>
				)}
			</div>
		</form>
	)
}
