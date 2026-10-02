import { describe, expect, it } from "vitest"

import { addDays, addMonths, daysInMonth, formatIsoDate, localToday, monthWeeks, parseIsoDate, toLocalDate, weekStart } from "./calendarDate"

describe("daysInMonth", () => {
	it("returns the length of ordinary months", () => {
		expect(daysInMonth(2025, 1)).toBe(31)
		expect(daysInMonth(2025, 4)).toBe(30)
	})

	it("handles February in leap and non-leap years", () => {
		expect(daysInMonth(2024, 2)).toBe(29)
		expect(daysInMonth(2025, 2)).toBe(28)
		expect(daysInMonth(1900, 2)).toBe(28)
		expect(daysInMonth(2000, 2)).toBe(29)
	})
})

describe("formatIsoDate", () => {
	it("pads year, month and day", () => {
		expect(formatIsoDate({ year: 5, month: 3, day: 9 })).toBe("0005-03-09")
		expect(formatIsoDate({ year: 2025, month: 12, day: 31 })).toBe("2025-12-31")
	})
})

describe("parseIsoDate", () => {
	it("parses a valid date", () => {
		expect(parseIsoDate("2025-06-15")).toEqual({ year: 2025, month: 6, day: 15 })
	})

	it("accepts a leap day only in a leap year", () => {
		expect(parseIsoDate("2024-02-29")).toEqual({ year: 2024, month: 2, day: 29 })
		expect(parseIsoDate("2025-02-29")).toBeNull()
	})

	it("rejects text that is not written yyyy-mm-dd", () => {
		expect(parseIsoDate("")).toBeNull()
		expect(parseIsoDate("2025-6-15")).toBeNull()
		expect(parseIsoDate("15/06/2025")).toBeNull()
		expect(parseIsoDate(" 2025-06-15")).toBeNull()
	})

	it("rejects out-of-range months and days", () => {
		expect(parseIsoDate("2025-00-10")).toBeNull()
		expect(parseIsoDate("2025-13-10")).toBeNull()
		expect(parseIsoDate("2025-05-00")).toBeNull()
		expect(parseIsoDate("2025-04-31")).toBeNull()
	})
})

describe("localToday", () => {
	it("reads the given clock in local time", () => {
		expect(localToday(new Date(2025, 0, 5, 23, 59))).toBe("2025-01-05")
	})

	it("defaults to the current moment", () => {
		expect(localToday()).toMatch(/^\d{4}-\d{2}-\d{2}$/)
	})
})

describe("addDays", () => {
	it("moves forward and backward", () => {
		expect(addDays("2025-01-30", 3)).toBe("2025-02-02")
		expect(addDays("2025-03-01", -1)).toBe("2025-02-28")
	})

	it("crosses year and leap-day boundaries", () => {
		expect(addDays("2025-12-31", 1)).toBe("2026-01-01")
		expect(addDays("2024-02-28", 1)).toBe("2024-02-29")
		expect(addDays("2025-06-15", 0)).toBe("2025-06-15")
	})
})

describe("addMonths", () => {
	it("keeps the day where the month has one", () => {
		expect(addMonths("2025-01-15", 1)).toBe("2025-02-15")
	})

	it("clamps to the last day of a shorter month", () => {
		expect(addMonths("2025-01-31", 1)).toBe("2025-02-28")
		expect(addMonths("2024-01-31", 1)).toBe("2024-02-29")
	})

	it("crosses year boundaries in both directions", () => {
		expect(addMonths("2025-12-10", 1)).toBe("2026-01-10")
		expect(addMonths("2025-01-10", -1)).toBe("2024-12-10")
		expect(addMonths("2025-06-10", 24)).toBe("2027-06-10")
	})
})

describe("weekStart", () => {
	it("starts on Sunday in English and Monday in French", () => {
		expect(weekStart("en-CA")).toBe(0)
		expect(weekStart("fr-CA")).toBe(1)
		expect(weekStart("fr")).toBe(1)
	})
})

describe("monthWeeks", () => {
	it("lays out a Sunday-first month with leading and trailing blanks", () => {
		const weeks = monthWeeks(2025, 6, 0)
		expect(weeks[0]).toEqual(["2025-06-01", "2025-06-02", "2025-06-03", "2025-06-04", "2025-06-05", "2025-06-06", "2025-06-07"])
		expect(weeks[weeks.length - 1]).toEqual(["2025-06-29", "2025-06-30", null, null, null, null, null])
		expect(weeks.every((week) => week.length === 7)).toBe(true)
	})

	it("pads the first week for a Monday-first month", () => {
		const weeks = monthWeeks(2025, 6, 1)
		expect(weeks[0]).toEqual([null, null, null, null, null, null, "2025-06-01"])
	})

	it("adds no trailing blanks when the month ends on the last cell", () => {
		const weeks = monthWeeks(2026, 2, 0)
		expect(weeks).toHaveLength(4)
		expect(weeks[3][6]).toBe("2026-02-28")
		expect(weeks.flat().includes(null)).toBe(false)
	})
})

describe("toLocalDate", () => {
	it("builds the local midnight of the day", () => {
		const date = toLocalDate("2025-06-15")
		expect([date.getFullYear(), date.getMonth(), date.getDate(), date.getHours()]).toEqual([2025, 5, 15, 0])
	})
})
