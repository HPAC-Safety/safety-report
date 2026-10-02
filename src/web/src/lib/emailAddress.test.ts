import { describe, expect, it } from "vitest"

import { emailSuggestions, isValidEmail, SUGGESTED_EMAIL_DOMAINS } from "./emailAddress"

describe("isValidEmail", () => {
	it("accepts a well-formed address", () => {
		expect(isValidEmail("pilot@example.com")).toBe(true)
		expect(isValidEmail("a.b+c@mail.example.co")).toBe(true)
	})

	it("rejects malformed addresses", () => {
		expect(isValidEmail("")).toBe(false)
		expect(isValidEmail("pilot")).toBe(false)
		expect(isValidEmail("pilot@example")).toBe(false)
		expect(isValidEmail("pilot@example.c")).toBe(false)
		expect(isValidEmail("pilot @example.com")).toBe(false)
		expect(isValidEmail("a@@example.com")).toBe(false)
	})

	it("enforces the 254 character limit", () => {
		const domain = "@example.com"
		expect(isValidEmail("a".repeat(254 - domain.length) + domain)).toBe(true)
		expect(isValidEmail("a".repeat(255 - domain.length) + domain)).toBe(false)
	})
})

describe("emailSuggestions", () => {
	it("offers every domain before the at sign", () => {
		expect(emailSuggestions("pilot")).toEqual(SUGGESTED_EMAIL_DOMAINS.map((domain) => `pilot@${domain}`))
		expect(emailSuggestions("pilot@")).toEqual(SUGGESTED_EMAIL_DOMAINS.map((domain) => `pilot@${domain}`))
	})

	it("offers only domains beginning with what follows the at sign, ignoring case", () => {
		expect(emailSuggestions("pilot@H")).toEqual(["pilot@hotmail.com"])
		expect(emailSuggestions("pilot@gm")).toEqual(["pilot@gmail.com"])
		expect(emailSuggestions("pilot@zzz")).toEqual([])
	})

	it("offers nothing for an empty local part, a second at sign or whitespace", () => {
		expect(emailSuggestions("")).toEqual([])
		expect(emailSuggestions("@gm")).toEqual([])
		expect(emailSuggestions("a@b@c")).toEqual([])
		expect(emailSuggestions("a b")).toEqual([])
	})

	it("offers nothing once a suggestion is already typed in full", () => {
		expect(emailSuggestions("pilot@gmail.com")).toEqual([])
	})
})
