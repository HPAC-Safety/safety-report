import type { RefObject } from "react"
import type { SavedAnswerRow } from "./savedAnswerRows"

export interface ResumeDraftDialogViewProps {
	rows: SavedAnswerRow[]
	onContinue: () => void
	onStartOver: () => void
	t: (key: string) => string
	dialogRef: RefObject<HTMLDialogElement>
	continueButtonRef: RefObject<HTMLButtonElement>
}

export function ResumeDraftDialogView({ rows, onContinue, onStartOver, t, dialogRef, continueButtonRef }: ResumeDraftDialogViewProps) {
	return (
		<dialog
			ref={dialogRef}
			aria-labelledby="resume-draft-title"
			aria-describedby="resume-draft-body"
			// A decision is required: Escape would otherwise close the dialog
			// with neither the saved report restored nor removed.
			onCancel={(event) => event.preventDefault()}
			className="m-auto w-[calc(100%-2rem)] max-w-measure rounded border border-rule bg-surface p-6 text-ink backdrop:bg-black/40"
		>
			<h2 id="resume-draft-title" className="font-display text-xl font-bold text-ink">
				{t("report.resume.title")}
			</h2>
			<p id="resume-draft-body" className="mt-2 font-sans text-sm text-ink-muted">
				{t("report.resume.body")}
			</p>

			<div className="mt-4 flex justify-end gap-3">
				<button
					type="button"
					className="touch-target rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2"
					onClick={onStartOver}
				>
					{t("report.resume.no")}
				</button>
				<button
					ref={continueButtonRef}
					type="button"
					className="touch-target rounded bg-brand-700 px-5 font-sans text-sm font-semibold text-ink-inverse"
					onClick={onContinue}
				>
					{t("report.resume.yes")}
				</button>
			</div>

			<div className="mt-6 max-h-80 overflow-y-auto">
				<table className="w-full border-collapse font-sans text-sm">
					<caption className="sr-only">{t("report.resume.tableCaption")}</caption>
					<thead>
						<tr className="border-b border-rule text-left text-ink-muted">
							<th scope="col" className="py-2 pr-4 font-medium">{t("report.resume.questionHeader")}</th>
							<th scope="col" className="py-2 font-medium">{t("report.resume.answerHeader")}</th>
						</tr>
					</thead>
					<tbody>
						{rows.map((row) => (
							<tr key={row.revisionId} className="border-b border-rule align-top">
								<th scope="row" className="py-2 pr-4 text-left font-normal text-ink">{row.label}</th>
								<td className="whitespace-pre-wrap break-words py-2 text-ink">{row.value}</td>
							</tr>
						))}
					</tbody>
				</table>
			</div>
		</dialog>
	)
}
