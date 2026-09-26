/*
 * The paging engine: pure functions turning the server's flat/nested question
 * list into an ordered sequence of pages, and deciding which are currently
 * visible. No React, no I/O — easy to reason about and to unit-test through
 * the component that uses it.
 */

import type { Locale } from "../i18n/locales"
import type { PublicOptionView, PublicQuestionView } from "../api/publicQuestions"
import { localToday, parseIsoDate } from "../lib/calendarDate"
import { isValidEmail } from "../lib/emailAddress"
import { DEFAULT_PHONE_COUNTRY, isValidPhone } from "../lib/phoneNumber"
import { choiceGroups } from "../lib/sortChoices"
import type { DraftAnswer } from "./draft"

export type AnswerMap = Record<string, DraftAnswer>

export type FormStep =
	| { kind: "intro"; question: PublicQuestionView }
	| { kind: "question"; question: PublicQuestionView }
	| { kind: "group"; question: PublicQuestionView }

/** A question collects no answer, and is skipped entirely when building an answer entry (ADR-0076). */
export function collectsNoAnswer(question: PublicQuestionView): boolean {
	return question.type === "statement" || question.type === "group"
}

export function questionLabel(question: PublicQuestionView, locale: Locale): string {
	return locale === "fr-CA" ? question.labelFr : question.labelEn
}

export function questionHelp(question: PublicQuestionView, locale: Locale): string | null {
	return locale === "fr-CA" ? question.helpTextFr : question.helpTextEn
}

export function questionPlaceholder(question: PublicQuestionView, locale: Locale): string | null {
	return locale === "fr-CA" ? question.placeholderFr : question.placeholderEn
}

export function optionLabel(option: PublicOptionView, locale: Locale): string {
	return locale === "fr-CA" ? option.labelFr : option.labelEn
}

/**
 * A question's choices as a reader in `locale` sees them: pinned first,
 * unpinned, pinned last, each group alphabetical in their language (ADR-0136).
 */
export function optionGroups(question: PublicQuestionView, locale: Locale): PublicOptionView[][] {
	return choiceGroups(question.options, locale, (option) => optionLabel(option, locale))
}

/**
 * The choice a stored select answer names: by its ID, which is what the form
 * keeps and submits (ADR-0128) — or, in a draft saved before answers named
 * choices, by its label in either language.
 */
export function optionFor(question: PublicQuestionView, stored: string): PublicOptionView | undefined {
	return (
		question.options.find((option) => option.id === stored) ??
		question.options.find((option) => option.labelEn === stored || option.labelFr === stored)
	)
}

/**
 * The choice a type-ahead's typed text names, ignoring case, in either
 * language — or none, when the reporter typed a value the question does not
 * offer yet (ADR-0129).
 */
export function optionTyped(question: PublicQuestionView, typed: string): PublicOptionView | undefined {
	const wanted = typed.trim().toLocaleLowerCase()
	return question.options.find(
		(option) => option.labelEn.toLocaleLowerCase() === wanted || option.labelFr.toLocaleLowerCase() === wanted,
	)
}

/**
 * The choice a picker or type-ahead's answer names: a single-select's by its ID,
 * a type-ahead's picked choice, or the one its typed words read as. Undefined
 * for typed words naming no choice — a value the question does not offer yet.
 */
export function answeredChoiceId(question: PublicQuestionView, answer: DraftAnswer | undefined): string | undefined {
	if (answer?.kind !== "value" || !answer.value.trim()) return undefined
	if (question.type !== "autocomplete") return optionFor(question, answer.value)?.id
	if (answer.choice && question.options.some((option) => option.id === answer.choice)) return answer.choice
	return optionTyped(question, answer.value)?.id
}

/**
 * Which of a question's choices its parent's answer lets the form offer
 * (ADR-0146):
 * - `all`: its choices depend on no question on the form;
 * - `waiting`: the parent is unanswered, so the question is disabled;
 * - `under`: only the choices under the parent's answer — none when the
 *   parent was answered with a value it does not offer yet.
 */
export type ChoiceScope =
	| { kind: "all" }
	| { kind: "waiting"; parent: PublicQuestionView }
	| { kind: "under"; parent: PublicQuestionView; options: PublicOptionView[] }

export function choiceScope(
	question: PublicQuestionView,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
): ChoiceScope {
	// A parent the form does not ask filters nothing, as a condition whose
	// parent is missing hides nothing.
	const parent = question.choicesDependOnQuestionId ? questionsById.get(question.choicesDependOnQuestionId) : undefined
	if (!parent) return { kind: "all" }

	const answer = answers[parent.revisionId]
	if (answer?.kind !== "value" || !answer.value.trim()) return { kind: "waiting", parent }

	const chosen = answeredChoiceId(parent, answer)
	return { kind: "under", parent, options: chosen ? question.options.filter((option) => option.parentChoiceId === chosen) : [] }
}

/** The question as the form offers it now: with only the choices its parent's answer allows. */
export function scopedQuestion(
	question: PublicQuestionView,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
): PublicQuestionView {
	const scope = choiceScope(question, answers, questionsById)
	if (scope.kind === "all") return question
	return { ...question, options: scope.kind === "under" ? scope.options : [] }
}

/**
 * Whether a question cannot be answered yet, and so never holds the reporter
 * back, even when required: its parent is unanswered, or it is a picker with
 * nothing under the parent's answer. A type-ahead always takes a typed value.
 */
export function cannotBeAnswered(
	question: PublicQuestionView,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
): boolean {
	const scope = choiceScope(question, answers, questionsById)
	if (scope.kind === "waiting") return true
	return scope.kind === "under" && question.type !== "autocomplete" && scope.options.length === 0
}

/**
 * The answers with every child answer its parent's answer no longer allows
 * removed: a choice not offered under it, typed words reading as such a
 * choice, or anything at all while the parent is unanswered. Words typed that
 * name no choice stay (ADR-0146).
 */
export function consistentAnswers(answers: AnswerMap, questionsById: Map<string, PublicQuestionView>): AnswerMap {
	let consistent = answers

	for (const question of questionsById.values()) {
		const answer = consistent[question.revisionId]
		if (answer?.kind !== "value") continue

		const scope = choiceScope(question, consistent, questionsById)
		if (scope.kind === "all") continue

		const offered = scope.kind === "under" ? scope.options : []
		const offers = (id: string | undefined) => id !== undefined && offered.some((option) => option.id === id)
		const keep =
			scope.kind === "under" &&
			(question.type !== "autocomplete"
				? offers(optionFor(question, answer.value)?.id)
				: answer.choice
					? offers(answer.choice)
					: offers(optionTyped({ ...question, options: offered }, answer.value)?.id) || !optionTyped(question, answer.value))

		if (!keep) {
			consistent = { ...consistent }
			delete consistent[question.revisionId]
		}
	}

	return consistent
}

/**
 * Every question and group child, flattened, keyed by its (question, not
 * revision) ID — what `dependsOnQuestionId` names.
 */
export function indexQuestionsById(topLevel: PublicQuestionView[]): Map<string, PublicQuestionView> {
	const byId = new Map<string, PublicQuestionView>()
	for (const question of topLevel) {
		byId.set(question.id, question)
		for (const child of question.children) {
			byId.set(child.id, child)
		}
	}
	return byId
}

/**
 * The top-level list, one entry per page: the leading live `statement`
 * becomes the introduction (Next only, no answer); every other `group`
 * becomes one page with its children; everything else is one question per
 * page. The server already excludes a group's children from the top level, so
 * no question is ever both a page of its own and somebody's child.
 */
export function buildSteps(topLevel: PublicQuestionView[]): FormStep[] {
	return topLevel.map((question, index) => {
		if (index === 0 && question.type === "statement") {
			return { kind: "intro", question }
		}
		if (question.type === "group") {
			return { kind: "group", question }
		}
		return { kind: "question", question }
	})
}

/**
 * Media consent is asked by a built-in rule rather than an authored
 * dependency (ADR-0117): only when publication consent is yes and a file is
 * attached, since it covers documents too (ADR-0119). `hasAttachment` says
 * whether one is.
 */
function isMediaConsentAsked(
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment: boolean,
): boolean {
	if (!hasAttachment) return false
	const publication = [...questionsById.values()].find((candidate) => candidate.role === "consent_publish")
	const answer = publication ? answers[publication.revisionId] : undefined
	return answer?.kind === "value" && answer.value === "yes"
}

/**
 * Whether `question`'s conditional parent (a yes/no question, or a
 * single-select question naming a required option) currently answers true.
 * A question with no `dependsOnQuestionId` is always visible, except media
 * consent, which follows its own rule.
 */
export function isConditionMet(
	question: PublicQuestionView,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment = false,
): boolean {
	if (question.role === "consent_media") return isMediaConsentAsked(answers, questionsById, hasAttachment)
	if (!question.dependsOnQuestionId) return true

	const parent = questionsById.get(question.dependsOnQuestionId)
	if (!parent) return true // Defensive: an unresolvable parent never hides a question outright.

	const parentAnswer = answers[parent.revisionId]
	if (!parentAnswer || parentAnswer.kind !== "value") return false

	if (!question.dependsOnChoiceId) {
		// Boolean parent (ADR-0060): the canonical token, never the localized word.
		return parentAnswer.value === "yes"
	}

	// Single-select parent (ADR-0074): the answer names a choice, and the server
	// names the required choice as it stands today — a replaced option's
	// replacement (ADR-0128) — so the condition compares choices, whatever
	// language the form is in.
	return optionFor(parent, parentAnswer.value)?.id === question.dependsOnChoiceId
}

/** The steps currently on the path, in order, with a group's hidden children already filtered out for rendering. */
export function visibleSteps(
	steps: FormStep[],
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment = false,
): FormStep[] {
	return steps.filter((step) => {
		if (step.kind === "intro") return true
		if (step.kind === "question") return isConditionMet(step.question, answers, questionsById, hasAttachment)

		// A group page stays visible while at least one child is visible;
		// grouping and conditional dependency are independent (ADR-0076), so a
		// child can be conditional even though the group itself never is.
		return step.question.children.some((child) => isConditionMet(child, answers, questionsById, hasAttachment))
	})
}

export function visibleChildren(
	question: PublicQuestionView,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment = false,
): PublicQuestionView[] {
	return question.children.filter((child) => isConditionMet(child, answers, questionsById, hasAttachment))
}

function isAnswered(question: PublicQuestionView, answers: AnswerMap): boolean {
	const answer = answers[question.revisionId]
	if (!answer) return false
	if (answer.kind === "value") return answer.value.trim().length > 0
	return answer.values.length > 0
}

/** The visible required questions on `step` that still have no answer — empty when the step may advance. */
export function unansweredRequired(
	step: FormStep,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment = false,
): PublicQuestionView[] {
	if (step.kind === "intro") return []

	// A question that cannot be answered yet never holds the reporter back (ADR-0146).
	const outstanding = (question: PublicQuestionView) =>
		question.isRequired && !isAnswered(question, answers) && !cannotBeAnswered(question, answers, questionsById)

	if (step.kind === "question") {
		if (collectsNoAnswer(step.question)) return []
		return outstanding(step.question) ? [step.question] : []
	}

	return visibleChildren(step.question, answers, questionsById, hasAttachment).filter(outstanding)
}

/**
 * What is wrong with an entered answer, as the catalogue key of its message, or
 * null when nothing is. An email or phone answer must be well formed
 * (ADR-0137); a date must be a real `yyyy-mm-dd` day, and not after the
 * reporter's own today unless the question allows it (ADR-0138). A blank one is
 * never wrong: an optional question may be left empty (REQ-SUB-086).
 */
export function answerProblem(question: PublicQuestionView, answer: DraftAnswer | undefined): string | null {
	if (answer?.kind !== "value" || answer.value.trim().length === 0) return null
	if (question.type === "email") return isValidEmail(answer.value.trim()) ? null : "report.email.invalid"
	if (question.type === "phone") {
		return isValidPhone(answer.country ?? DEFAULT_PHONE_COUNTRY, answer.value) ? null : "report.phone.invalid"
	}
	if (question.type === "date") {
		if (!parseIsoDate(answer.value)) return "report.date.invalid"
		return !question.allowFutureDates && answer.value > localToday() ? "report.date.future" : null
	}
	return null
}

/** Whether an entered answer is malformed; see `answerProblem`. */
export function isMalformed(question: PublicQuestionView, answer: DraftAnswer | undefined): boolean {
	return answerProblem(question, answer) !== null
}

/** The visible questions on `step` whose entered answer is malformed (REQ-SUB-087, REQ-SUB-088, REQ-SUB-101). */
export function malformedAnswers(
	step: FormStep,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment = false,
): PublicQuestionView[] {
	if (step.kind === "intro") return []
	const questions = step.kind === "group" ? visibleChildren(step.question, answers, questionsById, hasAttachment) : [step.question]
	return questions.filter((question) => isMalformed(question, answers[question.revisionId]))
}

/** Everything on `step` that stops the reporter leaving it: an unanswered required question, or a malformed answer. */
export function blockingQuestions(
	step: FormStep,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	hasAttachment = false,
): PublicQuestionView[] {
	return [...unansweredRequired(step, answers, questionsById, hasAttachment), ...malformedAnswers(step, answers, questionsById, hasAttachment)]
}
