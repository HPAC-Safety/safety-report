import { describe, expect, it } from "vitest"
import { formatFileSize } from "./formatFileSize"

describe("formatFileSize", () => {
	it("shows bytes below one kilobyte", () => {
		expect(formatFileSize(0)).toBe("0 B")
		expect(formatFileSize(1023)).toBe("1023 B")
	})

	it("shows whole kilobytes below one megabyte", () => {
		expect(formatFileSize(1024)).toBe("1 KB")
		expect(formatFileSize(1536)).toBe("2 KB")
	})

	it("shows megabytes to a tenth from one megabyte", () => {
		expect(formatFileSize(1024 * 1024)).toBe("1.0 MB")
		expect(formatFileSize(5.25 * 1024 * 1024)).toBe("5.3 MB")
	})
})
