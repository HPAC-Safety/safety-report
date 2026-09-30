import { useEffect, useRef } from "react"
import { useLocale } from "../i18n/useLocale"

/*
 * Confirms restoring an earlier summary version (REQ-MOD-204). Restoring saves
 * a new version and changes what a published report shows at once, so it is
 * asked first. Nothing is lost either way — the history keeps every version —
 * but focus still starts on the choice that changes nothing, and Escape keeps
 * the current version too.
 */
export function RestoreVersionDialog({
	sequence,
	isLive,
	onConfirm,
	onKeep,
}: {
	sequence: number
	isLive: boolean
	onConfirm: () => void
	onKeep: () => void
}) {
	const { t } = useLocale()
	const dialog = useRef<HTMLDialogElement>(null)
	const keepButton = useRef<HTMLButtonElement>(null)

	useEffect(() => {
		const element = dialog.current
		if (element && !element.open) element.showModal()
		keepButton.current?.focus()
	}, [])

	return (
		<dialog
			ref={dialog}
			aria-labelledby="restore-version-title"
			aria-describedby="restore-version-body"
			onCancel={(event) => {
				event.preventDefault()
				onKeep()
			}}
			className="m-auto w-[calc(100%-2rem)] max-w-measure rounded border border-rule bg-surface p-6 text-ink backdrop:bg-black/40"
		>
			<h2 id="restore-version-title" className="font-display text-xl font-bold text-ink">
				{t("reports.restore.title", { n: String(sequence) })}
			</h2>
			<p id="restore-version-body" className="mt-2 font-sans text-sm text-ink-muted">
				{t(isLive ? "reports.restore.body.live" : "reports.restore.body.draft", { n: String(sequence) })}
			</p>
			<div className="mt-4 flex justify-end gap-3">
				<button
					type="button"
					className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					onClick={onConfirm}
				>
					{t("reports.restore.confirm")}
				</button>
				<button
					ref={keepButton}
					type="button"
					className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
					onClick={onKeep}
				>
					{t("reports.restore.keep")}
				</button>
			</div>
		</dialog>
	)
}
