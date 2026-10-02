import type { RefObject } from "react"
import { useLocale } from "../i18n/useLocale"
import type { DiffPart } from "../lib/wordDiff"

export interface TranslateConfirmDialogViewProps {
	target: "en" | "fr"
	onAccept: () => void
	onKeep: () => void
	currentParts: DiffPart[]
	proposedParts: DiffPart[]
	dialogRef: RefObject<HTMLDialogElement>
	keepButtonRef: RefObject<HTMLButtonElement>
}

export function TranslateConfirmDialogView({
	target,
	onAccept,
	onKeep,
	currentParts,
	proposedParts,
	dialogRef,
	keepButtonRef,
}: TranslateConfirmDialogViewProps) {
	const { t } = useLocale()

	return (
		<dialog
			ref={dialogRef}
			aria-labelledby="translate-confirm-title"
			onCancel={(event) => {
				event.preventDefault()
				onKeep()
			}}
			className="m-auto w-[calc(100%-2rem)] max-w-3xl rounded border border-rule bg-surface p-6 text-ink backdrop:bg-black/40"
		>
			<h2 id="translate-confirm-title" className="font-display text-xl font-bold text-ink">
				{t(`reports.translate.confirm.title.${target}`)}
			</h2>
			<p className="mt-2 font-sans text-sm text-ink-muted">{t("reports.translate.confirm.body")}</p>

			<div className="mt-4 grid gap-4 md:grid-cols-2">
				<section aria-labelledby="translate-current" className="rounded border border-rule bg-surface-2 p-3">
					<h3 id="translate-current" className="font-sans text-xs uppercase tracking-wide text-ink-muted">
						{t("reports.translate.confirm.current")}
					</h3>
					<p className="mt-2 whitespace-pre-line font-sans text-sm text-ink" data-diff="current">
						{currentParts.map((part, index) =>
							part.kind === "removed" ? (
								<del key={index} className="bg-surface-4 text-ink line-through decoration-2">
									{part.text}
								</del>
							) : (
								<span key={index}>{part.text}</span>
							),
						)}
					</p>
				</section>
				<section aria-labelledby="translate-proposed" className="rounded border border-rule bg-surface-2 p-3">
					<h3 id="translate-proposed" className="font-sans text-xs uppercase tracking-wide text-ink-muted">
						{t("reports.translate.confirm.proposed")}
					</h3>
					<p className="mt-2 whitespace-pre-line font-sans text-sm text-ink" data-diff="proposed">
						{proposedParts.map((part, index) =>
							part.kind === "added" ? (
								<ins key={index} className="bg-surface-4 font-semibold text-ink underline decoration-2">
									{part.text}
								</ins>
							) : (
								<span key={index}>{part.text}</span>
							),
						)}
					</p>
				</section>
			</div>

			<div className="mt-4 flex justify-end gap-3">
				<button
					type="button"
					className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					onClick={onAccept}
				>
					{t("reports.translate.confirm.accept")}
				</button>
				<button
					ref={keepButtonRef}
					type="button"
					className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
					onClick={onKeep}
				>
					{t("reports.translate.confirm.keep")}
				</button>
			</div>
		</dialog>
	)
}
