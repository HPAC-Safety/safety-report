import type { Locale } from "../i18n/locales"
import { formatAnswer } from "../lib/formatAnswer"
import { DEFAULT_PHONE_COUNTRY, callingCodeOf } from "../lib/phoneNumber"
import type { PublicQuestionView } from "../api/publicQuestions"
import type { DraftAnswer, DraftAttachment } from "./draft"
import { collectsNoAnswer, optionFor, optionGroups, optionLabel, questionLabel } from "./steps"

/*
 * The rows the resume dialog lists, and the count of what a changed form left
 * out, both read from what this browser saved (issue no. 344).
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
	answers: Partial<Record<string, DraftAnswer>>,
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

/**
 * How many saved answers or attached files were left out of `rows` because
 * their question revision is not the current revision of a question on the form
 * (ADR-0185). An empty saved answer holds nothing to clear, so it is not counted.
 */
export function clearedAnswerCount(
	answers: Partial<Record<string, DraftAnswer>>,
	attachments: Record<string, DraftAttachment[]>,
	rows: SavedAnswerRow[],
): number {
	const kept = new Set(rows.map((row) => `${row.kind}:${row.revisionId}`))
	const held = (answer: DraftAnswer) => (answer.kind === "value" ? answer.value.length > 0 : answer.values.length > 0)
	const answersCleared = Object.entries(answers).filter(([revisionId, answer]) => answer !== undefined && held(answer) && !kept.has(`answer:${revisionId}`))
	const filesCleared = Object.entries(attachments).filter(([revisionId, files]) => files.length > 0 && !kept.has(`attachments:${revisionId}`))
	return answersCleared.length + filesCleared.length
}

function displayValue(question: PublicQuestionView, answer: DraftAnswer, locale: Locale, t: (key: string) => string): string {
	const labelOf = (stored: string) => {
		const option = optionFor(question, stored)
		return option ? optionLabel(option, locale) : stored
	}
	if (answer.kind === "options") {
		// Listed as the form lists the choices (ADR-0136); a value no choice carries goes last, as stored.
		const listed = optionGroups(question, locale).flat()
		const rank = (stored: string) => {
			const index = listed.findIndex((option) => option.id === optionFor(question, stored)?.id)
			return index < 0 ? listed.length : index
		}
		return [...answer.values]
			.sort((left, right) => rank(left) - rank(right))
			.map(labelOf)
			.join(", ")
	}
	if (question.type === "single_select") return labelOf(answer.value)
	if (question.type === "phone") return `+${callingCodeOf(answer.country ?? DEFAULT_PHONE_COUNTRY)} ${answer.value}`
	return formatAnswer(question.type, answer.value, locale, t)
}
