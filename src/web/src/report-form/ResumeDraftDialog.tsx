import { useEffect, useRef } from "react"

import type { Locale } from "../i18n/locales"
import type { PublicQuestionView } from "../api/publicQuestions"
import type { DraftAnswer } from "./draft"
import { collectsNoAnswer, questionLabel } from "./steps"

/*
 * Asks a returning reporter whether to continue the report this browser saved
 * (issue #344, `features/report-submission/README.md`'s "Returning to a saved
 * report"). Everything it shows comes from local storage — the dialog makes no
 * request of its own.
 */

export interface SavedAnswerRow {
	revisionId: string
	label: string
	value: string
}

/**
 * The saved answers that belong to a question on the current form, in form
 * order. An answer to a revision the form no longer shows has nothing to be
 * restored into, so it is not listed. Attachments are never saved, so never
 * appear.
 */
export function savedAnswerRows(
	questions: PublicQuestionView[],
	answers: Record<string, DraftAnswer>,
	locale: Locale,
	t: (key: string) => string,
): SavedAnswerRow[] {
	const rows: SavedAnswerRow[] = []
	for (const question of questions.flatMap((entry) => [entry, ...entry.children])) {
		if (collectsNoAnswer(question) || question.type === "file_upload") continue
		const answer = answers[question.revisionId]
		if (!answer) continue
		rows.push({ revisionId: question.revisionId, label: questionLabel(question, locale), value: displayValue(question, answer, t) })
	}
	return rows
}

function displayValue(question: PublicQuestionView, answer: DraftAnswer, t: (key: string) => string): string {
	if (answer.kind === "options") return answer.values.join(", ")
	if (question.type === "yes_no" || question.type === "checkbox") {
		if (answer.value === "yes") return t("report.booleanYes")
		if (answer.value === "no") return t("report.booleanNo")
	}
	return answer.value
}

export function ResumeDraftDialog({
	rows,
	onContinue,
	onStartOver,
	t,
}: {
	rows: SavedAnswerRow[]
	onContinue: () => void
	onStartOver: () => void
	t: (key: string) => string
}) {
	const dialog = useRef<HTMLDialogElement>(null)
	const continueButton = useRef<HTMLButtonElement>(null)

	// showModal() focuses the first control, which is the destructive one;
	// start on the choice that keeps the reporter's answers instead.
	useEffect(() => {
		const element = dialog.current
		if (element && !element.open) element.showModal()
		continueButton.current?.focus()
	}, [])

	return (
		<dialog
			ref={dialog}
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
					ref={continueButton}
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
