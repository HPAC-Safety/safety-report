/*
 * An email answer (ADR-0137, `features/report-submission/README.md` "Email and
 * phone answers"). The API holds it to the same rule, in
 * `src/HpacSafety.Core/Features/Reporting/ContactAnswer.cs`.
 */

const EMAIL_MAX_LENGTH = 254
const EMAIL = /^[^\s@]+@(?:[^\s@.]+\.)+[^\s@.]{2,}$/

/** The domains suggested below the field, in the order they are offered (REQ-SUB-092). */
export const SUGGESTED_EMAIL_DOMAINS = ["gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "icloud.com", "mail.com"]

/** Whether `value` is one email address. */
export function isValidEmail(value: string): boolean {
	return value.length <= EMAIL_MAX_LENGTH && EMAIL.test(value)
}

/**
 * The addresses to suggest for what has been typed: before `@`, every domain
 * completing it; after `@`, only the domains beginning with what follows it.
 * None for an empty local part, a second `@`, or an address a suggestion
 * already equals.
 */
export function emailSuggestions(typed: string): string[] {
	const [local, domain, ...rest] = typed.split("@")
	if (!local || rest.length > 0 || /\s/.test(typed)) return []
	const lowered = (domain ?? "").toLowerCase()
	const offered = SUGGESTED_EMAIL_DOMAINS.filter((candidate) => candidate.startsWith(lowered)).map((candidate) => `${local}@${candidate}`)
	return offered.includes(typed) ? [] : offered
}
