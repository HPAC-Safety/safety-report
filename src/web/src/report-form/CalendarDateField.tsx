import { useEffect, useId, useRef, useState, type ChangeEvent, type FocusEvent, type KeyboardEvent, type MouseEvent } from "react"

import {
	addDays,
	addMonths,
	formatIsoDate,
	localToday,
	monthWeeks,
	parseIsoDate,
	toLocalDate,
	weekStart,
} from "../lib/calendarDate"
import { CalendarDateFieldView } from "./CalendarDateField.view"
import type { DateFieldProps } from "./DateField"

/** How many years back the year picker reaches. */
const YEARS_BACK = 100
/** How many years ahead it reaches, where the question allows a future date. */
const YEARS_AHEAD = 10

/** One day of the month grid, with everything the view draws on its button. */
export interface CalendarCell {
	day: string
	/** Whether it is the chosen date. */
	selected: boolean
	tabIndex: 0 | -1
	label: string
	current: "date" | undefined
	disabled: true | undefined
	className: string
	/** The day of the month, as the button shows it. */
	text: number
}

/**
 * The view model of the desktop date field: a text field taking `yyyy-mm-dd`
 * with a one-month calendar popover that opens on click or focus.
 */
export function useCalendarDateField({
	fieldId,
	value,
	allowFutureDates,
	locale,
	onChange,
	t,
}: DateFieldProps) {
	const dialogId = `${fieldId}-calendar`
	const formatId = `${fieldId}-format`
	const headingId = useId()
	const inputRef = useRef<HTMLInputElement>(null)
	const gridRef = useRef<HTMLTableElement>(null)
	// Set just before focus is handed back to the field, so that focus does not
	// open the calendar again.
	const quietFocus = useRef(false)

	const today = localToday()
	const [open, setOpen] = useState(false)
	const [view, setView] = useState(() => viewOf(value, today))
	const [focusedDay, setFocusedDay] = useState<string | null>(null)
	const [announcement, setAnnouncement] = useState("")
	// Whether focus has gone into the calendar. Until it has, none of its controls
	// is in the Tab order, so a reporter typing a date tabs straight past it;
	// ArrowDown from the field goes in.
	const [entered, setEntered] = useState(false)
	const announceTimer = useRef<number | undefined>(undefined)
	useEffect(() => () => window.clearTimeout(announceTimer.current), [])

	// Moves real focus onto the roving day once it is rendered.
	useEffect(() => {
		if (!open || !focusedDay) return
		gridRef.current?.querySelector<HTMLButtonElement>(`button[data-day="${focusedDay}"]`)?.focus()
	}, [open, focusedDay, view])

	const isAfterToday = (iso: string) => !allowFutureDates && iso > today
	const latestYear = Number(today.slice(0, 4)) + (allowFutureDates ? YEARS_AHEAD : 0)
	const earliestYear = Number(today.slice(0, 4)) - YEARS_BACK
	const monthFormat = new Intl.DateTimeFormat(locale, { month: "long" })
	const longFormat = new Intl.DateTimeFormat(locale, { dateStyle: "full" })
	const firstWeekday = weekStart(locale)
	const viewMonthKey = formatIsoDate({ ...view, day: 1 })
	const nextMonthBlocked = isAfterToday(addMonths(viewMonthKey, 1))

	function openCalendar() {
		if (open) return
		setView(viewOf(value, today))
		setFocusedDay(null)
		setEntered(false)
		setOpen(true)
	}

	function close(returnFocus: boolean) {
		setOpen(false)
		setFocusedDay(null)
		setEntered(false)
		if (returnFocus) {
			quietFocus.current = true
			inputRef.current?.focus()
		}
	}

	function choose(iso: string) {
		if (isAfterToday(iso)) return
		onChange(iso)
		// Cleared first, so choosing the same day again is announced again.
		const words = t("report.date.chosen", { date: longFormat.format(toLocalDate(iso)) })
		setAnnouncement("")
		window.clearTimeout(announceTimer.current)
		announceTimer.current = window.setTimeout(() => setAnnouncement(words), 50)
		close(true)
	}

	/** Moves the roving day, keeping it on or before today where the future is not allowed. */
	function moveTo(iso: string) {
		const kept = isAfterToday(iso) ? today : iso
		// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- kept is today or an ISO date already validated
		const { year, month } = parseIsoDate(kept)!
		setView({ year, month })
		setFocusedDay(kept)
	}

	function onFieldFocus() {
		// Focus is back on the field, so Tab from here skips the calendar again.
		setEntered(false)
		if (quietFocus.current) {
			quietFocus.current = false
			return
		}
		openCalendar()
	}

	function onFieldKeyDown(event: KeyboardEvent<HTMLInputElement>) {
		if (event.key === "ArrowDown") {
			event.preventDefault()
			const start = parseIsoDate(value) ? value : today
			if (!open) setOpen(true)
			moveTo(start)
			return
		}
		if (event.key === "Escape" && open) {
			event.preventDefault()
			close(false)
		}
	}

	function onGridKeyDown(event: KeyboardEvent<HTMLTableElement>) {
		if (!focusedDay) return
		const moves: Partial<Record<string, () => string>> = {
			ArrowLeft: () => addDays(focusedDay, -1),
			ArrowRight: () => addDays(focusedDay, 1),
			ArrowUp: () => addDays(focusedDay, -7),
			ArrowDown: () => addDays(focusedDay, 7),
			PageUp: () => addMonths(focusedDay, -1),
			PageDown: () => addMonths(focusedDay, 1),
		}
		const move = moves[event.key]
		if (move) {
			event.preventDefault()
			moveTo(move())
			return
		}
		if (event.key === "Enter" || event.key === " ") {
			event.preventDefault()
			choose(focusedDay)
		}
	}

	function onDialogKeyDown(event: KeyboardEvent<HTMLDivElement>) {
		if (event.key === "Escape") {
			event.preventDefault()
			event.stopPropagation()
			close(true)
		}
	}

	// Closes once focus leaves the field and its calendar together.
	function onWrapperBlur(event: FocusEvent<HTMLDivElement>) {
		if (!event.currentTarget.contains(event.relatedTarget)) close(false)
	}

	// A press on the calendar's background keeps focus where it is, so the
	// calendar does not close under the pointer.
	function onDialogMouseDown(event: MouseEvent<HTMLDivElement>) {
		if (!(event.target as HTMLElement).closest("select, button")) event.preventDefault()
	}

	function showMonth(year: number, month: number) {
		setView({ year, month })
		setFocusedDay(null)
	}

	function stepMonth(months: number) {
		// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- addMonths of the valid view month is a valid ISO date
		const moved = parseIsoDate(addMonths(viewMonthKey, months))!
		showMonth(moved.year, moved.month)
	}

	const years: number[] = []
	for (let year = Math.max(latestYear, view.year); year >= Math.min(earliestYear, view.year); year--) years.push(year)

	const weekdays = Array.from({ length: 7 }, (_, index) => {
		// 4 January 1970 was a Sunday.
		const day = new Date(1970, 0, 4 + ((firstWeekday + index) % 7))
		return {
			short: new Intl.DateTimeFormat(locale, { weekday: "short" }).format(day),
			long: new Intl.DateTimeFormat(locale, { weekday: "long" }).format(day),
		}
	})

	// The day a Tab into the grid lands on: the chosen day, today, or the 1st.
	const tabStop =
		focusedDay ??
		(value.startsWith(viewMonthKey.slice(0, 8)) && parseIsoDate(value)
			? value
			: today.startsWith(viewMonthKey.slice(0, 8))
				? today
				: viewMonthKey)

	const monthOptions = Array.from({ length: 12 }, (_, index) => index + 1).map((month) => ({
		month,
		label: monthFormat.format(new Date(2000, month - 1, 1)),
		disabled: isAfterToday(formatIsoDate({ year: view.year, month, day: 1 })),
	}))

	const weeks: (CalendarCell | null)[][] = monthWeeks(view.year, view.month, firstWeekday).map((week) =>
		week.map((day) =>
			day === null
				? null
				: {
						day,
						selected: day === value,
						tabIndex: entered && day === tabStop ? 0 : -1,
						label: longFormat.format(toLocalDate(day)),
						current: day === today ? "date" : undefined,
						disabled: isAfterToday(day) || undefined,
						className: dayClassName(day === value, day === today, isAfterToday(day)),
						text: Number(day.slice(8)),
					},
		),
	)

	function onMonthChange(event: ChangeEvent<HTMLSelectElement>) {
		showMonth(view.year, Number(event.target.value))
	}

	function onYearChange(event: ChangeEvent<HTMLSelectElement>) {
		const year = Number(event.target.value)
		// Where the future is not allowed, a year whose chosen month is still ahead lands on today's month.
		const month = isAfterToday(formatIsoDate({ year, month: view.month, day: 1 })) ? Number(today.slice(5, 7)) : view.month
		showMonth(year, month)
	}

	return {
		dialogId,
		formatId,
		headingId,
		inputRef,
		gridRef,
		open,
		entered,
		announcement,
		view,
		years,
		weekdays,
		monthOptions,
		weeks,
		nextMonthBlocked,
		headingText: `${monthFormat.format(new Date(2000, view.month - 1, 1))} ${view.year}`,
		onWrapperBlur,
		onFieldFocus,
		onFieldClick: openCalendar,
		onFieldKeyDown,
		onDialogKeyDown,
		onDialogMouseDown,
		onDialogFocus: () => setEntered(true),
		onPreviousMonth: () => stepMonth(-1),
		onNextMonth: () => stepMonth(1),
		onMonthChange,
		onYearChange,
		onGridKeyDown,
		onDayClick: choose,
		onDayFocus: (day: string) => setFocusedDay(day),
	}
}

export type CalendarDateFieldModel = ReturnType<typeof useCalendarDateField>

/** The desktop date field; the logic is `useCalendarDateField`. */
export function CalendarDateField(props: DateFieldProps) {
	return <CalendarDateFieldView {...props} {...useCalendarDateField(props)} />
}

/** The month a calendar opens on: the chosen date's, or today's. */
function viewOf(value: string, today: string): { year: number; month: number } {
	// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- today is always a valid ISO date
	const { year, month } = parseIsoDate(value) ?? parseIsoDate(today)!
	return { year, month }
}

function dayClassName(chosen: boolean, isToday: boolean, blocked: boolean): string {
	const base = "mx-auto flex h-9 w-9 items-center justify-center rounded-full"
	if (blocked) return `${base} cursor-not-allowed text-ink-muted opacity-40`
	if (chosen) return `${base} bg-brand-700 font-semibold text-ink-inverse`
	if (isToday) return `${base} border border-brand-700 font-semibold hover:bg-surface-2`
	return `${base} hover:bg-surface-2`
}
