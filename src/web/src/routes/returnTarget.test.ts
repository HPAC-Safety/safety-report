import { describe, expect, it } from "vitest"
import { returnTarget } from "./returnTarget"

describe("returnTarget", () => {
	it("goes home when nothing was requested", () => {
		expect(returnTarget(null)).toBe("/")
		expect(returnTarget("")).toBe("/")
	})

	it("keeps a path on this site", () => {
		expect(returnTarget("/reports/abc?x=1")).toBe("/reports/abc?x=1")
	})

	it("refuses another origin, a protocol-relative URL, and a backslash path", () => {
		expect(returnTarget("https://evil.example")).toBe("/")
		expect(returnTarget("//evil.example")).toBe("/")
		expect(returnTarget("/\\evil.example")).toBe("/")
	})
})
