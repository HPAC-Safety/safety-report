/*
 * The paging engine: pure functions turning the server's flat/nested question
 * list into an ordered sequence of pages, and deciding which are currently
 * visible. No React, no I/O — easy to reason about and to unit-test through
 * the component that uses it.
 */

import type { Locale } from "../i18n/locales"
import type { PublicOptionView, PublicQuestionView } from "../api/publicQuestions"
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
	locale: Locale,
	hasAttachment = false,
): boolean {
	if (question.role === "consent_media") return isMediaConsentAsked(answers, questionsById, hasAttachment)
	if (!question.dependsOnQuestionId) return true

	const parent = questionsById.get(question.dependsOnQuestionId)
	if (!parent) return true // Defensive: an unresolvable parent never hides a question outright.

	const parentAnswer = answers[parent.revisionId]
	if (!parentAnswer || parentAnswer.kind !== "value") return false

	if (!question.dependsOnOptionCode) {
		// Boolean parent (ADR-0060): the canonical token, never the localized word.
		return parentAnswer.value === "yes"
	}

	// Single-select parent (ADR-0074): compare against the option's label in
	// the language the reporter is currently viewing, because that is what
	// the stored answer literally is (ADR-0072).
	const requiredOption = parent.options.find((option) => option.code === question.dependsOnOptionCode)
	if (!requiredOption) return false

	return parentAnswer.value === optionLabel(requiredOption, locale)
}

/** The steps currently on the path, in order, with a group's hidden children already filtered out for rendering. */
export function visibleSteps(
	steps: FormStep[],
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	locale: Locale,
	hasAttachment = false,
): FormStep[] {
	return steps.filter((step) => {
		if (step.kind === "intro") return true
		if (step.kind === "question") return isConditionMet(step.question, answers, questionsById, locale, hasAttachment)

		// A group page stays visible while at least one child is visible;
		// grouping and conditional dependency are independent (ADR-0076), so a
		// child can be conditional even though the group itself never is.
		return step.question.children.some((child) => isConditionMet(child, answers, questionsById, locale, hasAttachment))
	})
}

export function visibleChildren(
	question: PublicQuestionView,
	answers: AnswerMap,
	questionsById: Map<string, PublicQuestionView>,
	locale: Locale,
	hasAttachment = false,
): PublicQuestionView[] {
	return question.children.filter((child) => isConditionMet(child, answers, questionsById, locale, hasAttachment))
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
	locale: Locale,
	hasAttachment = false,
): PublicQuestionView[] {
	if (step.kind === "intro") return []

	if (step.kind === "question") {
		if (collectsNoAnswer(step.question)) return []
		return step.question.isRequired && !isAnswered(step.question, answers) ? [step.question] : []
	}

	return visibleChildren(step.question, answers, questionsById, locale, hasAttachment).filter(
		(child) => child.isRequired && !isAnswered(child, answers),
	)
}
