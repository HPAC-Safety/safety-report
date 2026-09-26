/*
 * Calendar-day arithmetic for a date answer, on `yyyy-mm-dd` strings: the only
 * form a date answer is ever stored or sent in (ADR-0072). A calendar day has no
 * time zone, so nothing here goes through an instant except `localToday`, which
 * reads the reporter's own clock (ADR-0138).
 */

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})$/

export interface CalendarDay {
	year: number
	/** 1 to 12. */
	month: number
	day: number
}

function pad(value: number, width: number): string {
	return String(value).padStart(width, "0")
}

export function daysInMonth(year: number, month: number): number {
	return new Date(year, month, 0).getDate()
}

export function formatIsoDate({ year, month, day }: CalendarDay): string {
	return `${pad(year, 4)}-${pad(month, 2)}-${pad(day, 2)}`
}

/** The calendar day `text` names, only when it is written exactly `yyyy-mm-dd` and exists. */
export function parseIsoDate(text: string): CalendarDay | null {
	const match = ISO_DATE.exec(text)
	if (!match) return null
	const [year, month, day] = match.slice(1).map(Number)
	if (month < 1 || day < 1 || month > 12 || day > daysInMonth(year, month)) return null
	return { year, month, day }
}

/** The reporter's own date today, as `yyyy-mm-dd`. */
export function localToday(now: Date = new Date()): string {
	return formatIsoDate({ year: now.getFullYear(), month: now.getMonth() + 1, day: now.getDate() })
}

/** `iso` moved by whole days. */
export function addDays(iso: string, days: number): string {
	const { year, month, day } = parseIsoDate(iso)!
	const moved = new Date(year, month - 1, day + days)
	return formatIsoDate({ year: moved.getFullYear(), month: moved.getMonth() + 1, day: moved.getDate() })
}

/** `iso` moved by whole months, keeping its day where the month has one and its last day otherwise. */
export function addMonths(iso: string, months: number): string {
	const { year, month, day } = parseIsoDate(iso)!
	const index = year * 12 + (month - 1) + months
	const target = { year: Math.floor(index / 12), month: (index % 12) + 1 }
	return formatIsoDate({ ...target, day: Math.min(day, daysInMonth(target.year, target.month)) })
}

/** The weekday a week starts on: Sunday (0) in English, Monday (1) in French. */
export function weekStart(locale: string): 0 | 1 {
	return locale.startsWith("fr") ? 1 : 0
}

/**
 * One month as weeks of seven cells, starting on `firstWeekday`. A cell is a
 * day's `yyyy-mm-dd`, or null where the week reaches into another month.
 */
export function monthWeeks(year: number, month: number, firstWeekday: 0 | 1): (string | null)[][] {
	const lead = (new Date(year, month - 1, 1).getDay() - firstWeekday + 7) % 7
	const cells: (string | null)[] = Array.from({ length: lead }, () => null)
	for (let day = 1; day <= daysInMonth(year, month); day++) cells.push(formatIsoDate({ year, month, day }))
	while (cells.length % 7 !== 0) cells.push(null)
	const weeks: (string | null)[][] = []
	for (let start = 0; start < cells.length; start += 7) weeks.push(cells.slice(start, start + 7))
	return weeks
}

/** The local-midnight `Date` of a calendar day, for `Intl` formatting only. */
export function toLocalDate(iso: string): Date {
	const { year, month, day } = parseIsoDate(iso)!
	return new Date(year, month - 1, day)
}
