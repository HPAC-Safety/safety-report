import type { Locale } from "../i18n/locales"

/*
 * An answer is stored in a language-neutral form — a date as ISO 8601
 * `YYYY-MM-DD`, a time as `HH:mm`, a yes/no as `yes` or `no` (ADR-0072). That
 * form is for storage only; a person reads it in the interface language
 * (REQ-MOD-075, REQ-SUB-068). A stored value that does not parse is shown as
 * stored rather than as "Invalid Date" (REQ-MOD-076).
 */

const DATE = /^(\d{4})-(\d{2})-(\d{2})$/
const TIME = /^(\d{2}):(\d{2})(?::(\d{2}))?$/

/**
 * Whether a question's answer reads the same in both languages once it is
 * formatted, so a second-language translation of it has nothing to add.
 */
export function isLanguageNeutral(type: string): boolean {
	return type === "date" || type === "time" || type === "yes_no" || type === "checkbox"
}

/** The stored answer as a person reads it in `locale`. */
export function formatAnswer(type: string, value: string, locale: Locale, t: (key: string) => string): string {
	switch (type) {
		case "date":
			return formatDate(value, locale) ?? value
		case "time":
			return formatTime(value, locale) ?? value
		case "yes_no":
		case "checkbox":
			if (value === "yes") return t("report.booleanYes")
			if (value === "no") return t("report.booleanNo")
			return value
		default:
			return value
	}
}

// Both are built and formatted in UTC so the viewer's own time zone never
// shifts a calendar date or a wall-clock time.
function formatDate(value: string, locale: Locale): string | null {
	const match = DATE.exec(value)
	if (!match) return null
	const [year, month, day] = match.slice(1).map(Number)
	const date = new Date(Date.UTC(year, month - 1, day))
	if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) return null
	return new Intl.DateTimeFormat(locale, { dateStyle: "long", timeZone: "UTC" }).format(date)
}

function formatTime(value: string, locale: Locale): string | null {
	const match = TIME.exec(value)
	if (!match) return null
	const [hours, minutes, seconds] = [Number(match[1]), Number(match[2]), Number(match[3] ?? 0)]
	if (hours > 23 || minutes > 59 || seconds > 59) return null
	return new Intl.DateTimeFormat(locale, { timeStyle: "short", timeZone: "UTC" }).format(new Date(Date.UTC(1970, 0, 1, hours, minutes)))
}
