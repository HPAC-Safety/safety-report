import { useCallback, useEffect, useState } from "react"
import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import {
	COMMENT_MAX_LENGTH,
	deleteComment,
	editComment,
	hideComment,
	listComments,
	postComment,
	type PublicComment,
} from "../api/publicReports"

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
// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
type Task = () =>
	Promise<unknown>
type Attempt = () =>
	Promise<boolean>
type Save = (text: string) =>
	Promise<boolean>

export function ReportComments({ reportId }: { reportId: string }) {
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

	return (
		<section aria-labelledby="comments-heading" className="mt-12 border-t border-rule pt-8">
			<h2 id="comments-heading" className="font-display text-2xl font-bold">
				{t("comments.title")}
			</h2>

			{error && (
				<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			{failed ? (
				<p role="alert" className="mt-4 font-sans text-ink-muted">
					{t("comments.error.load")}
				</p>
			) : !comments ? (
				<p className="mt-4 font-sans text-ink-muted">{t("comments.loading")}</p>
			) : comments.length === 0 ? (
				<p className="mt-4 font-sans text-ink-muted">{t("comments.empty")}</p>
			) : (
				<ol aria-label={t("comments.listLabel")} className="mt-6 flex flex-col gap-4">
					{comments.map((comment) => (
						<CommentItem
							key={comment.id}
							comment={comment}
							locale={locale}
							canHide={isReviewer}
							onEdit={(text) => run(() => editComment(reportId, comment.id, text, locale))}
							onDelete={() => run(() => deleteComment(reportId, comment.id))}
							onHide={() => run(() => hideComment(comment.id))}
						/>
					))}
				</ol>
			)}

			{isSignedIn ? (
				<Composer onPost={(text) => run(() => postComment(reportId, text, locale))} />
			) : (
				<p className="mt-8 font-sans text-ink">
					<Link
						to={`/login?returnTo=${encodeURIComponent(`/reports/${reportId}`)}`}
						className="font-medium text-brand-700 underline"
					>
						{t("comments.signIn")}
					</Link>
				</p>
			)}
		</section>
	)
}

function CommentItem({
	comment,
	locale,
	canHide,
	onEdit,
	onDelete,
	onHide,
}: {
	comment: PublicComment
	locale: string
	canHide: boolean
	onEdit: Save
	onDelete: Attempt
	onHide: Attempt
}) {
	const { t } = useLocale()
	const [editing, setEditing] = useState(false)
	const [confirming, setConfirming] = useState<"delete" | "hide" | null>(null)
	const at = new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" })

	const written = comment.locale === locale
	const translated = !written && comment.translatedText !== null
	const shownText = translated ? comment.translatedText! : comment.text
	const shownLocale = translated ? locale : comment.locale

	return (
		<li
			data-comment-id={comment.id}
			data-comment-author={comment.isMine ? "you" : "member"}
			className="rounded border border-rule bg-surface p-4"
		>
			<p className="flex flex-wrap items-baseline gap-x-3 gap-y-1 font-sans text-sm text-ink-muted">
				<span className="font-medium text-ink">{comment.isMine ? t("comments.author.you") : t("comments.author.member")}</span>
				<span>{at.format(new Date(comment.createdAt))}</span>
				{comment.edited && <span data-comment-edited>{t("comments.edited")}</span>}
				{translated && !editing && <TranslatedIcon label={t("comments.translated")} />}
			</p>

			{editing ? (
				<Composer
					initial={comment.text}
					submitLabel={t("comments.save")}
					onCancel={() => setEditing(false)}
					onPost={async (text) => {
						const saved = await onEdit(text)
						if (saved) setEditing(false)
						return saved
					}}
				/>
			) : (
				<>
					<p lang={shownLocale} data-comment-text className="mt-2 whitespace-pre-line font-sans text-ink">
						{shownText}
					</p>

					{!written && !translated && (
						<p data-comment-awaiting className="mt-2 font-sans text-sm text-ink-muted">
							{t("comments.awaitingTranslation")}
						</p>
					)}
				</>
			)}

			{!editing && (comment.isMine || canHide) && (
				<div className="mt-3 flex flex-wrap items-center gap-3">
					{confirming ? (
						<>
							<span className="font-sans text-sm text-ink">
								{confirming === "delete" ? t("comments.confirmDelete") : t("comments.confirmHide")}
							</span>
							<button
								type="button"
								className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse"
								onClick={() => void (confirming === "delete" ? onDelete() : onHide()).then(() => setConfirming(null))}
							>
								{confirming === "delete" ? t("comments.delete") : t("comments.hide")}
							</button>
							<button
								type="button"
								className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink"
								onClick={() => setConfirming(null)}
							>
								{t("comments.keep")}
							</button>
						</>
					) : (
						<>
							{comment.isMine && (
								<>
									<button type="button" className={SECONDARY} onClick={() => setEditing(true)}>
										{t("comments.edit")}
									</button>
									<button type="button" className={SECONDARY} onClick={() => setConfirming("delete")}>
										{t("comments.delete")}
									</button>
								</>
							)}
							{canHide && (
								<button type="button" className={SECONDARY} onClick={() => setConfirming("hide")}>
									{t("comments.hide")}
								</button>
							)}
						</>
					)}
				</div>
			)}
		</li>
	)
}

/**
 * A small, muted mark that the text shown is a machine translation. It is
 * deliberately quiet; its name reaches screen readers, and the tooltip explains
 * it to anyone who hovers (REQ-COM-018).
 */
function TranslatedIcon({ label }: { label: string }) {
	return (
		<span data-comment-translated title={label} className="inline-flex items-center self-center text-ink-muted opacity-70">
			<svg
				aria-hidden="true"
				viewBox="0 0 24 24"
				className="h-3.5 w-3.5"
				fill="none"
				stroke="currentColor"
				strokeWidth="2"
				strokeLinecap="round"
				strokeLinejoin="round"
			>
				<path d="M4 5h8M8 3v2M10 5c0 4-3 7-6 8M6 9c1 2 3 4 6 5" />
				<path d="M13 21l4-9 4 9M14.5 18h5" />
			</svg>
			<span className="sr-only">{label}</span>
		</span>
	)
}

const SECONDARY = "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"

function Composer({
	initial = "",
	submitLabel,
	onPost,
	onCancel,
}: {
	initial?: string
	submitLabel?: string
	onPost: Save
	onCancel?: () => void
}) {
	const { t } = useLocale()
	const [text, setText] = useState(initial)
	const [saving, setSaving] = useState(false)
	const blank = text.trim().length === 0
	const tooLong = text.trim().length > COMMENT_MAX_LENGTH

	async function submit(event: React.FormEvent) {
		event.preventDefault()
		if (blank || tooLong) return
		setSaving(true)
		const saved = await onPost(text)
		setSaving(false)
		if (saved && !onCancel) setText("")
	}

	return (
		<form className="mt-6 flex flex-col gap-2" onSubmit={(event) => void submit(event)}>
			<label className="font-sans text-sm font-medium text-ink">
				{onCancel ? t("comments.editLabel") : t("comments.newLabel")}
				<textarea
					value={text}
					onChange={(event) => setText(event.target.value)}
					rows={4}
					aria-describedby={onCancel ? undefined : "comment-reminder"}
					className="mt-2 block w-full rounded border border-rule bg-surface p-3 font-sans text-base font-normal text-ink"
				/>
			</label>
			{!onCancel && (
				<p id="comment-reminder" className="font-sans text-sm text-ink-muted">
					{t("comments.reminder")}
				</p>
			)}
			<p className={`font-sans text-sm ${tooLong ? "text-brand-700" : "text-ink-muted"}`} aria-live="polite">
				{t("comments.length", { count: String(text.trim().length), max: String(COMMENT_MAX_LENGTH) })}
			</p>
			<div className="flex flex-wrap gap-3">
				<button
					type="submit"
					disabled={blank || tooLong || saving}
					className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse disabled:opacity-60"
				>
					{submitLabel ?? t("comments.post")}
				</button>
				{onCancel && (
					<button type="button" className={SECONDARY} onClick={onCancel}>
						{t("comments.cancel")}
					</button>
				)}
			</div>
		</form>
	)
}
