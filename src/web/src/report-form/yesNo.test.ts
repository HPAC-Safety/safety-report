import { describe, expect, it } from "vitest"
import { isYesNoType, yesNoValue } from "./yesNo"

describe("isYesNoType", () => {
	it("accepts yes_no and checkbox", () => {
		expect(isYesNoType("yes_no")).toBe(true)
		expect(isYesNoType("checkbox")).toBe(true)
	})

	it("refuses other types", () => {
		expect(isYesNoType("text")).toBe(false)
	})
})

describe("yesNoValue", () => {
	it("maps yes to true", () => {
		expect(yesNoValue("yes")).toBe(true)
	})

	it("maps no to false", () => {
		expect(yesNoValue("no")).toBe(false)
	})

	it("maps anything else to null", () => {
		expect(yesNoValue("")).toBeNull()
		expect(yesNoValue("maybe")).toBeNull()
	})
})
