import { describe, expect, it } from "vitest"
import { PRIMARY, SECONDARY } from "./privateAttachmentStyles"

describe("private attachment styles", () => {
	it("offers non-empty class strings for both buttons", () => {
		expect(SECONDARY.length).toBeGreaterThan(0)
		expect(PRIMARY.length).toBeGreaterThan(0)
	})

	it("gives both buttons a touch target", () => {
		expect(SECONDARY).toContain("touch-target")
		expect(PRIMARY).toContain("touch-target")
	})
})
