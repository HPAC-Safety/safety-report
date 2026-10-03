import type { CalendarDateFieldModel } from "./CalendarDateField"
import type { DateFieldProps } from "./DateField"

export type CalendarDateFieldViewProps = DateFieldProps & CalendarDateFieldModel

/** The desktop date field: a text field taking `yyyy-mm-dd` and a one-month calendar popover (ADR-0138). */
export function CalendarDateFieldView({
	fieldId,
	className,
	describedBy,
	placeholder,
	value,
	onChange,
	t,
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
	headingText,
	onWrapperBlur,
	onFieldFocus,
	onFieldClick,
	onFieldKeyDown,
	onDialogKeyDown,
	onDialogMouseDown,
	onDialogFocus,
	onPreviousMonth,
	onNextMonth,
	onMonthChange,
	onYearChange,
	onGridKeyDown,
	onDayClick,
	onDayFocus,
}: CalendarDateFieldViewProps) {
	return (
		// Presentational: it only sees the focus leaving, and a press inside the calendar's own background.
		<div className="relative" role="presentation" onBlur={onWrapperBlur} onMouseDown={onDialogMouseDown}>
			{/* The date-picker combobox pattern (WAI-ARIA 1.2): the text field is the combobox, the calendar its dialog popup (REQ-SUB-131). */}
			<input
				ref={inputRef}
				id={fieldId}
				type="text"
				role="combobox"
				inputMode="numeric"
				autoComplete="off"
				aria-haspopup="dialog"
				aria-expanded={open}
				aria-controls={dialogId}
				className={className}
				value={value}
				aria-describedby={[formatId, describedBy].filter(Boolean).join(" ")}
				placeholder={placeholder ?? t("report.date.placeholder")}
				onFocus={onFieldFocus}
				onClick={onFieldClick}
				onKeyDown={onFieldKeyDown}
				onChange={(event) => onChange(event.target.value)}
			/>
			{/* The format, even when the question's own placeholder replaces it. */}
			<span id={formatId} className="sr-only">
				{t("report.date.placeholder")}
			</span>
			<p role="status" className="sr-only">
				{announcement}
			</p>
			{/* A native, non-modal <dialog>: the element that takes the keys and the focus bubbling up from the controls inside it. */}
			<dialog
				id={dialogId}
				aria-label={t("report.date.calendar")}
				open={open}
				className="absolute left-0 top-full z-40 mt-1 w-80 max-w-full rounded border border-rule bg-surface p-3 font-sans text-ink shadow-lg"
				onKeyDown={onDialogKeyDown}
				onFocus={onDialogFocus}
			>
				{open && (
					<>
						<div className="flex items-center gap-1">
							<button
								type="button"
								className="touch-target rounded px-2 hover:bg-surface-2"
								tabIndex={entered ? 0 : -1}
								aria-label={t("report.date.previousMonth")}
								onClick={onPreviousMonth}
							>
								‹
							</button>
							<select
								aria-label={t("report.date.month")}
								tabIndex={entered ? 0 : -1}
								className="min-w-0 flex-1 rounded border border-rule bg-surface px-1 py-1"
								value={view.month}
								onChange={onMonthChange}
							>
								{monthOptions.map((option) => (
									<option key={option.month} value={option.month} disabled={option.disabled}>
										{option.label}
									</option>
								))}
							</select>
							<select
								aria-label={t("report.date.year")}
								tabIndex={entered ? 0 : -1}
								className="rounded border border-rule bg-surface px-1 py-1"
								value={view.year}
								onChange={onYearChange}
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
								tabIndex={entered ? 0 : -1}
								disabled={nextMonthBlocked}
								onClick={onNextMonth}
							>
								›
							</button>
						</div>
						<p id={headingId} className="sr-only">
							{headingText}
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
								{weeks.map((week, row) => (
									<tr key={row}>
										{week.map((cell, column) =>
											cell === null ? (
												<td key={column} />
											) : (
												<td key={cell.day} role="gridcell" aria-selected={cell.selected}>
													<button
														type="button"
														data-day={cell.day}
														tabIndex={cell.tabIndex}
														aria-label={cell.label}
														aria-current={cell.current}
														aria-disabled={cell.disabled}
														className={cell.className}
														onClick={() => onDayClick(cell.day)}
														onFocus={() => onDayFocus(cell.day)}
													>
														{cell.text}
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
			</dialog>
		</div>
	)
}
