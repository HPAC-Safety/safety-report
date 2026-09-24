import { useEffect, useRef } from "react"
import { useLocale } from "../i18n/useLocale"
import { wordDiff } from "../lib/wordDiff"

/*
 * Asks before a machine translation replaces a summary language, showing the
 * current text and the proposed one with their differences marked (REQ-MOD-072,
 * ADR-0106). Focus starts on keeping the current text, and Escape keeps it too.
 */
export function TranslateConfirmDialog({
	target,
	current,
	proposed,
	onAccept,
	onKeep,
}: {
	target: "en" | "fr"
	current: string
	proposed: string
	onAccept: () => void
	onKeep: () => void
}) {
	const { t } = useLocale()
	const dialog = useRef<HTMLDialogElement>(null)
	const keepButton = useRef<HTMLButtonElement>(null)
	const parts = wordDiff(current, proposed)

	useEffect(() => {
		const element = dialog.current
		if (element && !element.open) element.showModal()
		keepButton.current?.focus()
	}, [])

	return (
		<dialog
			ref={dialog}
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
						{parts
							.filter((part) => part.kind !== "added")
							.map((part, index) =>
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
						{parts
							.filter((part) => part.kind !== "removed")
							.map((part, index) =>
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
					ref={keepButton}
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
