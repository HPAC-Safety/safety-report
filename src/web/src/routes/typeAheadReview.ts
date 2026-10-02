import { sortChoices } from "../lib/sortChoices"
import type { TranslationDirection } from "../components/translationDirection"
import type { TypeAheadValueView } from "../api/adminQuestions"

/**
 * A correction in progress. `baselineEn`/`baselineFr` are the wording as the
 * correction view opened, or as Translate last drafted from — the same
 * "written and differs from the source" rule a question's choice uses
 * (ADR-0141, ADR-0144). Editing the source again after an earlier translate,
 * or after the target already had text, re-enables Translate.
 */
export type Draft = {
	labelEn: string
	labelFr: string
	baselineEn: string
	baselineFr: string
	direction: TranslationDirection
	translating: boolean
	translationError: string | null
}

export type QuestionGroup = { id: string; heading: string; values: TypeAheadValueView[] }

export type Wording = (value: { labelEn: string | null; labelFr: string | null }) => string

/** A value's wording in the reader's language, falling back to the other language, then to nothing. */
export function wordingIn(locale: string, value: { labelEn: string | null; labelFr: string | null }): string {
	return (locale === "fr-CA" ? (value.labelFr ?? value.labelEn) : (value.labelEn ?? value.labelFr)) ?? ""
}

/**
 * Whether Translate is offered for a draft: its source language has text, and
 * either its target is empty or its source differs from the baseline — the
 * wording when correction opened, or when it was last translated
 * (ADR-0141/ADR-0144, same rule as `QuestionEditor`'s `canTranslateChoice`).
 */
export function canTranslateDraft(draft: Draft): boolean {
	const source = draft.direction === "toFrench" ? draft.labelEn : draft.labelFr
	const target = draft.direction === "toFrench" ? draft.labelFr : draft.labelEn
	const baseline = draft.direction === "toFrench" ? draft.baselineEn : draft.baselineFr
	return source.trim().length > 0 && (target.trim().length === 0 || source !== baseline)
}

/** The queue grouped by question: headings and the values under each in `locale`'s alphabetical order (ADR-0136). */
export function groupByQuestion(values: readonly TypeAheadValueView[], locale: string, wording: Wording): QuestionGroup[] {
	const byQuestion = new Map<string, TypeAheadValueView[]>()
	for (const value of values) byQuestion.set(value.questionId, [...(byQuestion.get(value.questionId) ?? []), value])
	const groups = [...byQuestion].map(([id, questionValues]) => ({
		id,
		heading: locale === "fr-CA" ? questionValues[0].questionLabelFr : questionValues[0].questionLabelEn,
		values: sortChoices(questionValues, locale, wording),
	}))
	return sortChoices(groups, locale, (group) => group.heading)
}
