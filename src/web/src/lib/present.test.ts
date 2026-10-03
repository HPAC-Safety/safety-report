import { describe, expect, it } from "vitest"
import { present } from "./present"

describe("present", () => {
	it("returns a value that is there, including a falsy one", () => {
		expect(present("text")).toBe("text")
		expect(present(0)).toBe(0)
		expect(present(false)).toBe(false)
		expect(present("")).toBe("")
	})

	it("fails on null and undefined, naming what was expected", () => {
		expect(() => present(null)).toThrow("Expected a value to be present.")
		expect(() => present(undefined, "the lightbox")).toThrow("Expected the lightbox to be present.")
	})
})
