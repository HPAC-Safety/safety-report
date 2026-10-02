import { describe, expect, it } from "vitest"
import { byteSizeLabel } from "./byteSizeLabel"

describe("byteSizeLabel", () => {
	it("shows whole bytes below a kilobyte", () => {
		expect(byteSizeLabel(512, "en-CA")).toBe("512 bytes")
	})

	it("shows kilobytes and megabytes to a tenth", () => {
		expect(byteSizeLabel(1536, "en-CA")).toBe("1.5 kB")
		expect(byteSizeLabel(2.4 * 1024 * 1024, "en-CA")).toBe("2.4 MB")
	})

	it("speaks the reader's language", () => {
		expect(byteSizeLabel(2.4 * 1024 * 1024, "fr-CA")).toMatch(/^2,4\s*Mo$/)
	})

	it("stops at gigabytes", () => {
		expect(byteSizeLabel(5 * 1024 ** 4, "en-CA")).toBe("5,120 GB")
	})
})
