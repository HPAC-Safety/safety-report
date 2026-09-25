/*
 * A yes/no or checkbox answer is a boolean: the API takes and stores `true` or
 * `false`, never words (ADR-0130). While the reporter answers, the form holds a
 * language-free `yes` or `no` token, so a report saved in the browser before
 * this change still restores; the token becomes the boolean only when the
 * report is sent.
 */

/** Whether a question's answer is a yes/no boolean. */
export function isYesNoType(type: string): boolean {
	return type === "yes_no" || type === "checkbox"
}

/** The form's language-free token as the boolean the API takes, or null for no answer. */
export function yesNoValue(token: string): boolean | null {
	if (token === "yes") return true
	if (token === "no") return false
	return null
}
