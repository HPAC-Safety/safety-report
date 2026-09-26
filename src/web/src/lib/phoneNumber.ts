import {
	AsYouType,
	getCountries,
	getCountryCallingCode,
	getExampleNumber,
	isValidPhoneNumber,
	parsePhoneNumberFromString,
	validatePhoneNumberLength,
	type CountryCode,
} from "libphonenumber-js/max"
import examples from "libphonenumber-js/mobile/examples"

import type { Locale } from "../i18n/locales"

/*
 * A phone answer (ADR-0137, `features/report-submission/README.md` "Email and
 * phone answers"): typed against a chosen country, masked by that country's
 * own convention, validated by its rules, and sent in E.164. The `max` build
 * carries the full validation patterns, so the form refuses what the API's
 * libphonenumber-csharp refuses.
 */

/** The country the picker starts on (REQ-SUB-089). */
export const DEFAULT_PHONE_COUNTRY: CountryCode = "CA"

export interface PhoneCountry {
	code: string
	name: string
	callingCode: string
	flag: string
}

function asCountry(code: string | undefined): CountryCode {
	return code && (getCountries() as string[]).includes(code) ? (code as CountryCode) : DEFAULT_PHONE_COUNTRY
}

/** A region's flag, from the two regional-indicator symbols its code spells. */
export function flagOf(code: string): string {
	return String.fromCodePoint(...[...code.toUpperCase()].map((letter) => 0x1f1e6 + letter.charCodeAt(0) - 65))
}

export function callingCodeOf(country: string): string {
	return getCountryCallingCode(asCountry(country))
}

/** Every country, named in the interface language, in that language's alphabetical order. */
export function phoneCountries(locale: Locale): PhoneCountry[] {
	const names = new Intl.DisplayNames([locale], { type: "region" })
	const collator = new Intl.Collator(locale)
	return getCountries()
		.map((code) => ({ code, name: names.of(code) ?? code, callingCode: getCountryCallingCode(code), flag: flagOf(code) }))
		.sort((a, b) => collator.compare(a.name, b.name))
}

/**
 * The digits typed, formatted as the chosen country writes them: nationally
 * where the country formats a number typed without its trunk prefix, as
 * `(604) 555-1234` for +1, and otherwise by its international grouping with
 * the calling code left off, as `20 7946 0018` for +44.
 */
export function maskPhone(country: string, digits: string): string {
	if (!digits) return ""
	const code = asCountry(country)
	const national = new AsYouType(code).input(digits)
	if (/\D/.test(national)) return national
	const callingCode = getCountryCallingCode(code)
	const international = new AsYouType().input(`+${callingCode}${digits}`)
	const prefix = `+${callingCode} `
	return international.startsWith(prefix) ? international.slice(prefix.length) : digits
}

/** The chosen country's pattern, each digit of its example number shown as 5. */
export function phonePlaceholder(country: string): string {
	const code = asCountry(country)
	const example = getExampleNumber(code, examples)
	return example ? maskPhone(code, example.nationalNumber).replace(/\d/g, "5") : ""
}

/** Whether one more digit would make the number longer than the country allows. */
export function isTooLong(country: string, digits: string): boolean {
	return validatePhoneNumberLength(digits, asCountry(country)) === "TOO_LONG"
}

/**
 * What the reporter typed, read as a country and its digits. A number typed or
 * pasted with a leading `+` names its own country, when its calling code does.
 */
export function readTypedPhone(country: string, typed: string): { country: string; digits: string } {
	const digits = typed.replace(/\D/g, "")
	if (!typed.trim().startsWith("+")) return { country, digits }
	const formatter = new AsYouType()
	formatter.input(`+${digits}`)
	const named = formatter.getCountry()
	const national = formatter.getNumber()?.nationalNumber
	return named && national ? { country: named, digits: national } : { country, digits }
}

export function isValidPhone(country: string, national: string): boolean {
	return isValidPhoneNumber(national, asCountry(country))
}

/** The number in E.164, as it is sent and stored, or null when it does not parse. */
export function toE164(country: string, national: string): string | null {
	return parsePhoneNumberFromString(national, asCountry(country))?.number ?? null
}

/** A stored E.164 answer, grouped as its country writes it internationally; anything else as stored. */
export function formatStoredPhone(value: string): string {
	if (!value.startsWith("+")) return value
	return parsePhoneNumberFromString(value)?.formatInternational() ?? value
}
