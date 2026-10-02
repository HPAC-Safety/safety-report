import type { ReportAnswer, ReportAnswerValue } from "../api/adminReports"
import type { Locale } from "../i18n/locales"
import { sortChoices } from "../lib/sortChoices"

/**
 * An answer's values as the page lists them. A multi-select answer's are listed
 * as the form lists its choices: pinned first, then alphabetically in the
 * reader's language, then pinned last (ADR-0136). Any other answer's are kept
 * as stored.
 */
export function listedValues(answer: ReportAnswer, locale: Locale): ReportAnswerValue[] {
	if (answer.type !== "multi_select") return answer.values
	const inReadersLanguage = (value: ReportAnswerValue) =>
		String(value.locale === locale ? value.value : (value.translatedValue ?? value.value))
	return sortChoices(
		answer.values.map((value, index) => ({ ...value, id: String(index).padStart(4, "0") })),
		locale,
		inReadersLanguage,
	)
}
