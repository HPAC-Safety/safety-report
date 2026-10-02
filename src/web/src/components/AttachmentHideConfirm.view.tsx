import { useLocale } from "../i18n/useLocale"
import type { AttachmentHideConfirmModel, AttachmentHideConfirmProps } from "./AttachmentHideConfirm"

export type AttachmentHideConfirmViewProps = AttachmentHideConfirmProps & AttachmentHideConfirmModel

/** The Hide button, or once it is pressed, the question with its confirm and keep buttons. */
export function AttachmentHideConfirmView({ confirming, onAsk, onConfirm, onKeep }: AttachmentHideConfirmViewProps) {
	const { t } = useLocale()

	if (!confirming) {
		return (
			<button
				type="button"
				className="touch-target inline-flex items-center rounded border border-rule px-2 font-sans text-xs text-ink"
				onClick={onAsk}
			>
				{t("reports.attachment.hide")}
			</button>
		)
	}

	return (
		<div className="flex flex-col gap-1">
			<span className="font-sans text-xs text-ink">{t("media.confirmHide")}</span>
			<div className="flex gap-1">
				<button
					type="button"
					className="touch-target inline-flex items-center rounded bg-brand-700 px-2 font-sans text-xs font-medium text-ink-inverse"
					onClick={onConfirm}
				>
					{t("media.hide")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-2 font-sans text-xs text-ink"
					onClick={onKeep}
				>
					{t("media.keep")}
				</button>
			</div>
		</div>
	)
}
