import { useLocale } from "../i18n/useLocale"
import type { PrivateNoteComposerProps } from "./PrivateNoteComposer"

const SECONDARY = "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
const PRIMARY =
	"touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-medium text-ink-inverse disabled:opacity-60"

export type PrivateNoteComposerViewProps = PrivateNoteComposerProps & {
	text: string
	attachmentId: string | null
	length: number
	tooLong: boolean
	maxLength: number
	submitDisabled: boolean
	changeText: (value: string) => void
	changeAttachment: (value: string) => void
	submit: (event: React.FormEvent) => void
}

export function PrivateNoteComposerView({
	attachments,
	label,
	submitLabel,
	onCancel,
	text,
	attachmentId,
	length,
	tooLong,
	maxLength,
	submitDisabled,
	changeText,
	changeAttachment,
	submit,
}: PrivateNoteComposerViewProps) {
	const { t } = useLocale()

	return (
		<form className="mt-4 flex flex-col gap-2" onSubmit={submit}>
			<label className="font-sans text-sm font-medium text-ink">
				{label ?? t("privateNotes.newLabel")}
				<textarea
					value={text}
					onChange={(event) => changeText(event.target.value)}
					rows={3}
					className="mt-2 block w-full rounded border border-rule bg-surface p-3 font-sans text-base font-normal text-ink"
				/>
			</label>
			<p className={`font-sans text-sm ${tooLong ? "text-brand-700" : "text-ink-muted"}`} aria-live="polite">
				{t("privateNotes.length", { count: String(length), max: String(maxLength) })}
			</p>
			{attachments.length > 0 && (
				<label className="font-sans text-sm font-medium text-ink">
					{t("privateNotes.attachmentLabel")}
					<select
						value={attachmentId ?? ""}
						onChange={(event) => changeAttachment(event.target.value)}
						className="touch-target mt-2 block w-full rounded border border-rule bg-surface px-3 font-sans text-base font-normal text-ink"
					>
						<option value="">{t("privateNotes.noAttachment")}</option>
						{attachments.map((attachment) => (
							<option key={attachment.id} value={attachment.id}>
								{attachment.fileName}
							</option>
						))}
					</select>
				</label>
			)}
			<div className="flex flex-wrap gap-3">
				<button type="submit" disabled={submitDisabled} className={PRIMARY}>
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
