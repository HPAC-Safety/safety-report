import { useMemo } from "react"

import type { Locale } from "../i18n/locales"
import {
	DEFAULT_PHONE_COUNTRY,
	callingCodeOf,
	flagOf,
	isTooLong,
	maskPhone,
	phoneCountries,
	phonePlaceholder,
	readTypedPhone,
} from "../lib/phoneNumber"
import type { DraftAnswer } from "./draft"

export interface PhoneFieldProps {
	fieldId: string
	describedBy: string | undefined
	answer: DraftAnswer | undefined
	onChange: (answer: DraftAnswer | undefined) => void
	locale: Locale
	t: (key: string) => string
}

/**
 * A phone question (REQ-SUB-085..091, ADR-0137): a country picker, starting on
 * Canada and showing the chosen country's flag and calling code, before a
 * telephone field masked as that country writes its numbers. The picker is a
 * native `select`, laid invisibly over what it shows, so it keeps the
 * platform's own keyboard and screen-reader behaviour.
 */
export function PhoneField({ fieldId, describedBy, answer, onChange, locale, t }: PhoneFieldProps) {
	const value = answer?.kind === "value" ? answer.value : ""
	const country = (answer?.kind === "value" ? answer.country : undefined) ?? DEFAULT_PHONE_COUNTRY
	const countries = useMemo(() => phoneCountries(locale), [locale])

	function change(nextCountry: string, masked: string) {
		// A country other than Canada is kept even before a digit is typed, so
		// the picker does not jump back to Canada.
		onChange(masked || nextCountry !== DEFAULT_PHONE_COUNTRY ? { kind: "value", value: masked, country: nextCountry } : undefined)
	}

	function onType(typed: string) {
		const previousDigits = value.replace(/\D/g, "")
		const read = readTypedPhone(country, typed)
		let digits = read.digits
		// Deleting a mask character, such as the ")" in "(604)", deletes the digit before it.
		if (typed.length < value.length && digits === previousDigits) digits = digits.slice(0, -1)
		if (digits.length > previousDigits.length && isTooLong(read.country, digits)) return
		change(read.country, maskPhone(read.country, digits))
	}

	function onChooseCountry(nextCountry: string) {
		change(nextCountry, maskPhone(nextCountry, value.replace(/\D/g, "")))
	}

	return (
		<div className="mt-1 flex gap-2">
			<div className="touch-target relative flex shrink-0 items-center rounded border border-rule bg-surface px-3 font-sans text-ink focus-within:outline focus-within:outline-2 focus-within:outline-brand-700">
				<span id={`${fieldId}-country-shown`} aria-hidden="true" className="whitespace-nowrap">
					{flagOf(country)} +{callingCodeOf(country)}
				</span>
				<select
					id={`${fieldId}-country`}
					aria-label={t("report.phone.country")}
					className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
					value={country}
					onChange={(event) => onChooseCountry(event.target.value)}
				>
					{countries.map((entry) => (
						<option key={entry.code} value={entry.code}>
							{`${entry.flag} ${entry.name} (+${entry.callingCode})`}
						</option>
					))}
				</select>
			</div>
			<input
				id={fieldId}
				type="tel"
				inputMode="tel"
				autoComplete="tel"
				className="w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"
				value={value}
				aria-describedby={describedBy}
				placeholder={phonePlaceholder(country)}
				onChange={(event) => onType(event.target.value)}
			/>
		</div>
	)
}
