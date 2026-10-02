import { useLocale } from "../i18n/useLocale"
import type { CommentComposerProps } from "./CommentComposer"

const SECONDARY = "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"

export type CommentComposerViewProps = CommentComposerProps & {
	text: string
	length: number
	tooLong: boolean
	maxLength: number
	submitDisabled: boolean
	changeText: (value: string) => void
	submit: (event: React.FormEvent) => void
}

export function CommentComposerView({
	submitLabel,
	onCancel,
	text,
	length,
	tooLong,
	maxLength,
	submitDisabled,
	changeText,
	submit,
}: CommentComposerViewProps) {
	const { t } = useLocale()

	return (
		<form className="mt-6 flex flex-col gap-2" onSubmit={submit}>
			<label className="font-sans text-sm font-medium text-ink">
				{onCancel ? t("comments.editLabel") : t("comments.newLabel")}
				<textarea
					value={text}
					onChange={(event) => changeText(event.target.value)}
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
				{t("comments.length", { count: String(length), max: String(maxLength) })}
			</p>
			<div className="flex flex-wrap gap-3">
				<button
					type="submit"
					disabled={submitDisabled}
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
