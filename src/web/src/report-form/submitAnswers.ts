import type { PublicQuestionView } from "../api/publicQuestions"
import type { SubmitAnswer } from "../api/reportSubmission"
import { DEFAULT_PHONE_COUNTRY, toE164 } from "../lib/phoneNumber"
import type { AttachmentMap } from "./attachmentMap"
import {
	cannotBeAnswered,
	collectsNoAnswer,
	optionFor,
	optionTyped,
	scopedQuestion,
	visibleChildren,
	type AnswerMap,
	type FormStep,
} from "./steps"
import { isYesNoType, yesNoValue } from "./yesNo"

/**
 * An entered text answer as it is sent: an email address trimmed, and a phone
 * number in E.164 for the country it was typed for (ADR-0137); a blank one of
 * either as no answer (REQ-SUB-086). The form has already refused a malformed
 * one.
 */
export function submittedText(question: PublicQuestionView, answer: { value: string; country?: string }): string | null {
	if (question.type !== "email" && question.type !== "phone") return answer.value
	const entered = answer.value.trim()
	if (!entered) return null
	if (question.type === "email") return entered
	return toE164(answer.country ?? DEFAULT_PHONE_COUNTRY, entered) ?? entered
}

/** The answers the final request carries, one per question the reporter was asked, in form order. */
export function buildSubmitAnswers(
	visible: FormStep[],
	answers: AnswerMap,
	attachments: AttachmentMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment: boolean,
): SubmitAnswer[] {
	const submitAnswers: SubmitAnswer[] = []

	for (const step of visible) {
		const questions = step.kind === "group" ? visibleChildren(step.question, answers, questionsById, hasAttachment) : [step.question]

		for (const question of questions) {
			if (collectsNoAnswer(question)) continue
			// A question that cannot be answered yet was never asked (ADR-0146).
			if (cannotBeAnswered(question, answers, questionsById)) continue

			const answer = answers[question.revisionId]

			// A choice answer names its choices by ID (ADR-0128); a type-ahead
			// value no choice carries yet goes as the typed text (ADR-0129).
			if (question.type === "multi_select" || question.type === "single_select" || question.type === "autocomplete") {
				// Only what the parent's answer lets it offer is matched (ADR-0146).
				const offered = scopedQuestion(question, answers, questionsById)
				const stored = answer?.kind === "options" ? answer.values : answer?.kind === "value" ? [answer.value] : []
				// A type-ahead choice picked from its list names itself by ID; only
				// typed text is matched to a choice by its wording.
				const picked =
					question.type === "autocomplete" && answer?.kind === "value" && answer.choice
						? offered.options.find((option) => option.id === answer.choice)
						: undefined
				const named = picked
					? [picked]
					: question.type === "autocomplete"
						? stored.map((typed) => optionTyped(offered, typed))
						: stored.map((value) => optionFor(offered, value))
				const typed = question.type === "autocomplete" && stored.length > 0 && !named[0] ? stored[0].trim() : null
				submitAnswers.push({
					questionRevisionId: question.revisionId,
					value: typed || null,
					choices: typed ? null : named.flatMap((option) => (option ? [option.id] : [])),
					attachments: null,
				})
				continue
			}

			if (question.type === "file_upload") {
				submitAnswers.push({
					questionRevisionId: question.revisionId,
					value: null,
					choices: null,
					attachments: (attachments[question.revisionId] ?? []).flatMap((row) =>
						row.status === "uploaded" && row.uploadId ? [{ uploadId: row.uploadId, fileName: row.name }] : [],
					),
				})
				continue
			}

			const value = answer?.kind === "value" ? submittedText(question, answer) : null

			submitAnswers.push({
				questionRevisionId: question.revisionId,
				// A yes/no is sent as a JSON boolean, whatever the language (ADR-0130).
				value: value !== null && isYesNoType(question.type) ? yesNoValue(value) : value,
				choices: null,
				attachments: null,
			})
		}
	}

	return submitAnswers
}
