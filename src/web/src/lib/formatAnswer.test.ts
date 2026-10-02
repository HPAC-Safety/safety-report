import { describe, expect, it } from "vitest"

import { formatAnswer, isLanguageNeutral } from "./formatAnswer"

const t = (key: string) => `t:${key}`

describe("isLanguageNeutral", () => {
	it("is true for answers that read the same in both languages", () => {
		for (const type of ["date", "time", "yes_no", "checkbox", "phone"]) expect(isLanguageNeutral(type)).toBe(true)
	})

	it("is false for other kinds", () => {
		expect(isLanguageNeutral("text")).toBe(false)
		expect(isLanguageNeutral("paragraph")).toBe(false)
	})
})

describe("formatAnswer", () => {
	describe("booleans", () => {
		it("renders true and false through the translator whatever the type", () => {
			expect(formatAnswer("yes_no", true, "en-CA", t)).toBe("t:report.booleanYes")
			expect(formatAnswer("checkbox", false, "fr-CA", t)).toBe("t:report.booleanNo")
		})
	})

	describe("dates", () => {
		it("formats a stored date in the interface language", () => {
			expect(formatAnswer("date", "2025-06-15", "en-CA", t)).toBe("June 15, 2025")
			expect(formatAnswer("date", "2025-06-15", "fr-CA", t)).toBe("15 juin 2025")
		})

		it("shows an unparsable or impossible date as stored", () => {
			expect(formatAnswer("date", "June 15", "en-CA", t)).toBe("June 15")
			expect(formatAnswer("date", "2025-02-30", "en-CA", t)).toBe("2025-02-30")
			expect(formatAnswer("date", "2025-13-01", "en-CA", t)).toBe("2025-13-01")
			expect(formatAnswer("date", "2025-01-32", "en-CA", t)).toBe("2025-01-32")
		})
	})

	describe("times", () => {
		it("formats a stored time in the interface language", () => {
			expect(formatAnswer("time", "14:05", "en-CA", t)).toMatch(/2:05\s?p\.m\./)
			expect(formatAnswer("time", "14:05", "fr-CA", t)).toMatch(/14\s?h\s?05/)
		})

		it("accepts seconds", () => {
			expect(formatAnswer("time", "09:30:15", "en-CA", t)).toMatch(/9:30\s?a\.m\./)
		})

		it("shows an unparsable or out-of-range time as stored", () => {
			expect(formatAnswer("time", "noon", "en-CA", t)).toBe("noon")
			expect(formatAnswer("time", "24:00", "en-CA", t)).toBe("24:00")
			expect(formatAnswer("time", "12:60", "en-CA", t)).toBe("12:60")
			expect(formatAnswer("time", "12:00:60", "en-CA", t)).toBe("12:00:60")
		})
	})

	describe("phones", () => {
		it("groups an E.164 number internationally", () => {
			expect(formatAnswer("phone", "+16045551234", "en-CA", t)).toBe("+1 604 555 1234")
		})

		it("leaves a number without a plus as stored", () => {
			expect(formatAnswer("phone", "6045551234", "en-CA", t)).toBe("6045551234")
		})
	})

	describe("yes/no tokens", () => {
		it("renders the form's yes and no tokens", () => {
			expect(formatAnswer("yes_no", "yes", "en-CA", t)).toBe("t:report.booleanYes")
			expect(formatAnswer("checkbox", "no", "en-CA", t)).toBe("t:report.booleanNo")
		})

		it("leaves any other value as stored", () => {
			expect(formatAnswer("yes_no", "maybe", "en-CA", t)).toBe("maybe")
		})
	})

	describe("other types", () => {
		it("returns the value unchanged", () => {
			expect(formatAnswer("text", "hello", "en-CA", t)).toBe("hello")
		})
	})
})
