import { useEffect, useRef } from "react"

import type { Locale } from "../i18n/locales"
import { formatAnswer } from "../lib/formatAnswer"
import { DEFAULT_PHONE_COUNTRY, callingCodeOf } from "../lib/phoneNumber"
import type { PublicQuestionView } from "../api/publicQuestions"
import type { DraftAnswer, DraftAttachment } from "./draft"
import { collectsNoAnswer, optionFor, optionLabel, questionLabel } from "./steps"

/*
 * Asks a returning reporter whether to continue the report this browser saved
 * (issue no. 344, `features/report-submission/README.md`'s "Returning to a saved
 * report"). Everything it shows comes from local storage — the dialog makes no
 * request of its own.
 */

export interface SavedAnswerRow {
	revisionId: string
	/** An answer restores into `answers`; attachments restore as the question's attached files. */
	kind: "answer" | "attachments"
	label: string
	value: string
}

/**
 * The saved answers and attached files that belong to a question on the
 * current form, in form order. One saved for a revision the form no longer
 * shows has nothing to be restored into, so it is not listed. A file-upload
 * question lists its saved files by name (ADR-0100).
 */
export function savedAnswerRows(
	questions: PublicQuestionView[],
	answers: Record<string, DraftAnswer>,
	attachments: Record<string, DraftAttachment[]>,
	locale: Locale,
	t: (key: string) => string,
): SavedAnswerRow[] {
	const rows: SavedAnswerRow[] = []
	for (const question of questions.flatMap((entry) => [entry, ...entry.children])) {
		if (collectsNoAnswer(question)) continue
		const label = questionLabel(question, locale)
		if (question.type === "file_upload") {
			const files = attachments[question.revisionId] ?? []
			if (files.length > 0) rows.push({ revisionId: question.revisionId, kind: "attachments", label, value: files.map((file) => file.name).join(", ") })
			continue
		}
		const answer = answers[question.revisionId]
		// A phone answer can hold only a chosen country, with no number yet.
		if (!answer || (answer.kind === "value" && answer.value.length === 0)) continue
		rows.push({ revisionId: question.revisionId, kind: "answer", label, value: displayValue(question, answer, locale, t) })
	}
	return rows
}

function displayValue(question: PublicQuestionView, answer: DraftAnswer, locale: Locale, t: (key: string) => string): string {
	const labelOf = (stored: string) => {
		const option = optionFor(question, stored)
		return option ? optionLabel(option, locale) : stored
	}
	if (answer.kind === "options") return answer.values.map(labelOf).join(", ")
	if (question.type === "single_select") return labelOf(answer.value)
	if (question.type === "phone") return `+${callingCodeOf(answer.country ?? DEFAULT_PHONE_COUNTRY)} ${answer.value}`
	return formatAnswer(question.type, answer.value, locale, t)
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
