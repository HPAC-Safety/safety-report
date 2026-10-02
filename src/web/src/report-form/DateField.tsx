import { useState } from "react"

import type { Locale } from "../i18n/locales"
import { localToday } from "../lib/calendarDate"
import { DateFieldView } from "./DateField.view"

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

/** Whether the device's main pointer is a finger, where the phone's own picker is used instead. */
function hasCoarsePointer(): boolean {
	return typeof window !== "undefined" && typeof window.matchMedia === "function" && window.matchMedia("(pointer: coarse)").matches
}

/**
 * The view model of a date question (REQ-SUB-098..107, ADR-0138). On a touch
 * device, the native `<input type="date">`, so the phone shows its own picker;
 * otherwise the calendar field. Either way the value is `yyyy-mm-dd`; a
 * question that does not allow future dates holds the reporter to their own
 * local today.
 */
export function useDateField({ allowFutureDates }: DateFieldProps) {
	const [coarse] = useState(hasCoarsePointer)
	return { coarse, max: coarse && !allowFutureDates ? localToday() : undefined }
}

export type DateFieldModel = ReturnType<typeof useDateField>

/** A date question; the logic is `useDateField`. */
export function DateField(props: DateFieldProps) {
	return <DateFieldView {...props} {...useDateField(props)} />
}
