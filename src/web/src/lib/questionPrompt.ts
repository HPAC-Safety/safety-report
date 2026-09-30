import type { Locale } from "../i18n/locales"

/*
 * A question's label is stored without a closing colon, and the interface draws
 * it (ADR-0181, REQ-QB-240): `Label:` in en-CA and `Label :` in fr-CA, where
 * French typography asks for a space before it. It is a no-break space, so the
 * colon never wraps onto a line of its own.
 *
 * A statement and a group are not asked, and a label that already ends in `?`
 * is a question of its own, so neither gets one.
 */

const NO_BREAK_SPACE = " "

/** The question kinds that collect no answer, and so carry no colon. */
function collectsNoAnswer(type: string): boolean {
	return type === "statement" || type === "group"
}

/** The label as a reader in `locale` sees it above the answer. */
export function labelWithColon(label: string, type: string, locale: Locale): string {
	if (collectsNoAnswer(type) || label.trimEnd().endsWith("?")) return label
	return label + (locale === "fr-CA" ? NO_BREAK_SPACE : "") + ":"
}

/**
 * Whether a label, as an administrator typed it, ends in a colon. A new label may
 * not: the form draws the colon itself, and the API refuses the save (REQ-QB-243,
 * REQ-QB-244).
 */
export function endsWithColon(label: string): boolean {
	return label.trimEnd().endsWith(":")
}
