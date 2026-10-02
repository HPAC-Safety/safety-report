import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import type { PublicComment, ReportCommentsProps } from "./ReportComments"
import { CommentComposer } from "./CommentComposer"
import { CommentItem } from "./CommentItem"

// Split across lines on purpose: tools/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
export type ReportCommentsViewProps = ReportCommentsProps & {
	comments: PublicComment[] | null
	failed: boolean
	error: string | null
	isSignedIn: boolean
	canHide: boolean
	post: (text: string) =>
		Promise<boolean>
	edit: (comment: PublicComment, text: string) =>
		Promise<boolean>
	remove: (comment: PublicComment) =>
		Promise<boolean>
	hide: (comment: PublicComment) =>
		Promise<boolean>
}

export function ReportCommentsView({
	reportId,
	comments,
	failed,
	error,
	isSignedIn,
	canHide,
	post,
	edit,
	remove,
	hide,
}: ReportCommentsViewProps) {
	const { t, locale } = useLocale()

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
							canHide={canHide}
							onEdit={(text) => edit(comment, text)}
							onDelete={() => remove(comment)}
							onHide={() => hide(comment)}
						/>
					))}
				</ol>
			)}

			{isSignedIn ? (
				<CommentComposer onPost={post} />
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
