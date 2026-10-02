import { useMemo, type ChangeEvent } from "react"

import type { Locale } from "../i18n/locales"
import { DEFAULT_PHONE_COUNTRY, isTooLong, maskPhone, phoneCountries, readTypedPhone } from "../lib/phoneNumber"
import type { DraftAnswer } from "./draft"
import { PhoneFieldView } from "./PhoneField.view"

export interface PhoneFieldProps {
	fieldId: string
	describedBy: string | undefined
	answer: DraftAnswer | undefined
	onChange: (answer: DraftAnswer | undefined) => void
	locale: Locale
	t: (key: string) => string
}

/**
 * The view model of a phone question (REQ-SUB-085..091, ADR-0137): a country
 * picker, starting on Canada and showing the chosen country's flag and calling
 * code, before a telephone field masked as that country writes its numbers.
 */
export function usePhoneField({ answer, onChange, locale }: PhoneFieldProps) {
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

	return {
		value,
		country,
		countries,
		onInputChange: (event: ChangeEvent<HTMLInputElement>) => onType(event.target.value),
		onCountryChange: (event: ChangeEvent<HTMLSelectElement>) => onChooseCountry(event.target.value),
	}
}

export type PhoneFieldModel = ReturnType<typeof usePhoneField>

/** A phone question; the logic is `usePhoneField`. */
export function PhoneField(props: PhoneFieldProps) {
	return <PhoneFieldView {...props} {...usePhoneField(props)} />
}
