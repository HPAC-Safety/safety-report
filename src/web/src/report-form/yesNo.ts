import type { Locale } from "../i18n/locales"

/*
 * A yes/no or checkbox answer is stored in the report's language: `yes`/`no`
 * in English, `oui`/`non` in French (ADR-0127). While the reporter answers,
 * the form holds a language-free `yes` or `no` token, so switching language
 * mid-form loses nothing; the word is written only when the report is sent.
 */

/** Whether a question's answer is one of the yes/no words. */
export function isYesNoType(type: string): boolean {
	return type === "yes_no" || type === "checkbox"
}

/** The form's language-free token, written as the submission language's word. */
export function yesNoWord(token: string, locale: Locale): string {
	if (token === "yes") return locale === "fr-CA" ? "oui" : "yes"
	if (token === "no") return locale === "fr-CA" ? "non" : "no"
	return token
}
