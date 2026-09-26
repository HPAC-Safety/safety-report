import { useEffect, useId, useRef, useState, type FocusEvent, type KeyboardEvent, type MouseEvent } from "react"

import type { Locale } from "../i18n/locales"
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

export interface DateFieldProps {
	fieldId: string
	className: string
	describedBy: string | undefined
	placeholder: string | undefined
	value: string
	allowFutureDates: boolean
	locale: Locale
	onChange: (value: string) => void
	t: (key: string, params?: Record<string, string | number>) => string
}

/** How many years back the year picker reaches. */
const YEARS_BACK = 100
/** How many years ahead it reaches, where the question allows a future date. */
const YEARS_AHEAD = 10

/** Whether the device's main pointer is a finger, where the phone's own picker is used instead. */
function hasCoarsePointer(): boolean {
	return typeof window !== "undefined" && typeof window.matchMedia === "function" && window.matchMedia("(pointer: coarse)").matches
}

/**
 * A date question (REQ-SUB-098..107, ADR-0138). On a touch device, the native
 * `<input type="date">`, so the phone shows its own picker. On a desktop, a
 * text field taking `yyyy-mm-dd` with a one-month calendar popover that opens
 * on click or focus. Either way the value is `yyyy-mm-dd`; a question that
 * does not allow future dates holds the reporter to their own local today.
 */
export function DateField(props: DateFieldProps) {
	const [coarse] = useState(hasCoarsePointer)
	if (coarse) {
		const { fieldId, className, describedBy, value, allowFutureDates, onChange } = props
		return (
			<input
				id={fieldId}
				type="date"
				className={className}
				value={value}
				max={allowFutureDates ? undefined : localToday()}
				aria-describedby={describedBy}
				onChange={(event) => onChange(event.target.value)}
			/>
		)
	}
	return <CalendarDateField {...props} />
}

function CalendarDateField({
	fieldId,
	className,
	describedBy,
	placeholder,
	value,
	allowFutureDates,
	locale,
	onChange,
	t,
}: DateFieldProps) {
	const dialogId = `${fieldId}-calendar`
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
		setOpen(true)
	}

	function close(returnFocus: boolean) {
		setOpen(false)
		setFocusedDay(null)
		if (returnFocus) {
			quietFocus.current = true
			inputRef.current?.focus()
		}
	}

	function choose(iso: string) {
		if (isAfterToday(iso)) return
		onChange(iso)
		setAnnouncement(t("report.date.chosen", { date: longFormat.format(toLocalDate(iso)) }))
		close(true)
	}

	/** Moves the roving day, keeping it on or before today where the future is not allowed. */
	function moveTo(iso: string) {
		const kept = isAfterToday(iso) ? today : iso
		const { year, month } = parseIsoDate(kept)!
		setView({ year, month })
		setFocusedDay(kept)
	}

	function onFieldFocus() {
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
		const moves: Record<string, () => string> = {
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
		if (!event.currentTarget.contains(event.relatedTarget as Node | null)) close(false)
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

	return (
		<div className="relative" onBlur={onWrapperBlur}>
			<input
				ref={inputRef}
				id={fieldId}
				type="text"
				inputMode="numeric"
				autoComplete="off"
				aria-haspopup="dialog"
				aria-expanded={open}
				aria-controls={dialogId}
				className={className}
				value={value}
				aria-describedby={describedBy}
				placeholder={placeholder ?? t("report.date.placeholder")}
				onFocus={onFieldFocus}
				onClick={openCalendar}
				onKeyDown={onFieldKeyDown}
				onChange={(event) => onChange(event.target.value)}
			/>
			<p role="status" className="sr-only">
				{announcement}
			</p>
			<div
				id={dialogId}
				role="dialog"
				aria-label={t("report.date.calendar")}
				hidden={!open}
				className="absolute left-0 top-full z-10 mt-1 w-80 max-w-full rounded border border-rule bg-surface p-3 font-sans text-ink shadow-lg"
				onKeyDown={onDialogKeyDown}
				onMouseDown={onDialogMouseDown}
			>
				{open && (
					<>
						<div className="flex items-center gap-1">
							<button
								type="button"
								className="touch-target rounded px-2 hover:bg-surface-2"
								aria-label={t("report.date.previousMonth")}
								onClick={() => stepMonth(-1)}
							>
								‹
							</button>
							<select
								aria-label={t("report.date.month")}
								className="min-w-0 flex-1 rounded border border-rule bg-surface px-1 py-1"
								value={view.month}
								onChange={(event) => showMonth(view.year, Number(event.target.value))}
							>
								{Array.from({ length: 12 }, (_, index) => index + 1).map((month) => (
									<option key={month} value={month} disabled={isAfterToday(formatIsoDate({ year: view.year, month, day: 1 }))}>
										{monthFormat.format(new Date(2000, month - 1, 1))}
									</option>
								))}
							</select>
							<select
								aria-label={t("report.date.year")}
								className="rounded border border-rule bg-surface px-1 py-1"
								value={view.year}
								onChange={(event) => {
									const year = Number(event.target.value)
									// Where the future is not allowed, a year whose chosen month is still ahead lands on today's month.
									const month = isAfterToday(formatIsoDate({ year, month: view.month, day: 1 })) ? Number(today.slice(5, 7)) : view.month
									showMonth(year, month)
								}}
							>
								{years.map((year) => (
									<option key={year} value={year}>
										{year}
									</option>
								))}
							</select>
							<button
								type="button"
								className="touch-target rounded px-2 hover:bg-surface-2 disabled:opacity-40"
								aria-label={t("report.date.nextMonth")}
								disabled={nextMonthBlocked}
								onClick={() => stepMonth(1)}
							>
								›
							</button>
						</div>
						<p id={headingId} className="sr-only">
							{`${monthFormat.format(new Date(2000, view.month - 1, 1))} ${view.year}`}
						</p>
						<table ref={gridRef} role="grid" aria-labelledby={headingId} className="mt-2 w-full text-center text-sm" onKeyDown={onGridKeyDown}>
							<thead>
								<tr>
									{weekdays.map((weekday) => (
										<th key={weekday.long} scope="col" abbr={weekday.long} className="py-1 text-xs font-medium text-ink-muted">
											{weekday.short}
										</th>
									))}
								</tr>
							</thead>
							<tbody>
								{monthWeeks(view.year, view.month, firstWeekday).map((week, row) => (
									<tr key={row}>
										{week.map((day, column) =>
											day === null ? (
												<td key={column} />
											) : (
												<td key={day} role="gridcell" aria-selected={day === value}>
													<button
														type="button"
														data-day={day}
														tabIndex={day === tabStop ? 0 : -1}
														aria-label={longFormat.format(toLocalDate(day))}
														aria-current={day === today ? "date" : undefined}
														aria-disabled={isAfterToday(day) || undefined}
														className={dayClassName(day === value, day === today, isAfterToday(day))}
														onClick={() => choose(day)}
														onFocus={() => setFocusedDay(day)}
													>
														{Number(day.slice(8))}
													</button>
												</td>
											),
										)}
									</tr>
								))}
							</tbody>
						</table>
					</>
				)}
			</div>
		</div>
	)
}

/** The month a calendar opens on: the chosen date's, or today's. */
function viewOf(value: string, today: string): { year: number; month: number } {
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
