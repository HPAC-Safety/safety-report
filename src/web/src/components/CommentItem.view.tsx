import { useLocale } from "../i18n/useLocale"
import type { CommentItemProps } from "./CommentItem"
import { CommentComposer } from "./CommentComposer"
import type { SaveComment } from "./CommentComposer"

const SECONDARY = "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"

export type CommentItemViewProps = CommentItemProps & {
	editing: boolean
	confirming: "delete" | "hide" | null
	written: boolean
	translated: boolean
	shownText: string
	shownLocale: string
	postedAt: string
	startEditing: () => void
	stopEditing: () => void
	askDelete: () => void
	askHide: () => void
	keep: () => void
	confirmed: () => void
	saveEdit: SaveComment
}

export function CommentItemView({
	comment,
	canHide,
	editing,
	confirming,
	written,
	translated,
	shownText,
	shownLocale,
	postedAt,
	startEditing,
	stopEditing,
	askDelete,
	askHide,
	keep,
	confirmed,
	saveEdit,
}: CommentItemViewProps) {
	const { t } = useLocale()

	return (
		<li
			data-comment-id={comment.id}
			data-comment-author={comment.isMine ? "you" : "member"}
			className="rounded border border-rule bg-surface p-4"
		>
			<p className="flex flex-wrap items-baseline gap-x-3 gap-y-1 font-sans text-sm text-ink-muted">
				<span className="font-medium text-ink">{comment.isMine ? t("comments.author.you") : t("comments.author.member")}</span>
				<span>{postedAt}</span>
				{comment.edited && <span data-comment-edited>{t("comments.edited")}</span>}
				{translated && !editing && <TranslatedIcon label={t("comments.translated")} />}
			</p>

			{editing ? (
				<CommentComposer initial={comment.text} submitLabel={t("comments.save")} onCancel={stopEditing} onPost={saveEdit} />
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
								onClick={confirmed}
							>
								{confirming === "delete" ? t("comments.delete") : t("comments.hide")}
							</button>
							<button
								type="button"
								className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink"
								onClick={keep}
							>
								{t("comments.keep")}
							</button>
						</>
					) : (
						<>
							{comment.isMine && (
								<>
									<button type="button" className={SECONDARY} onClick={startEditing}>
										{t("comments.edit")}
									</button>
									<button type="button" className={SECONDARY} onClick={askDelete}>
										{t("comments.delete")}
									</button>
								</>
							)}
							{canHide && (
								<button type="button" className={SECONDARY} onClick={askHide}>
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
