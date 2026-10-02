import { describe, expect, it } from "vitest"
import { STORAGE_KEY, resolveInitialTheme } from "./resolveInitialTheme"

describe("resolveInitialTheme", () => {
	it("returns a stored light or dark theme", () => {
		expect(resolveInitialTheme("light")).toBe("light")
		expect(resolveInitialTheme("dark")).toBe("dark")
	})

	it("returns null for nothing or an invalid value", () => {
		expect(resolveInitialTheme(null)).toBeNull()
		expect(resolveInitialTheme("blue")).toBeNull()
	})

	it("exposes the storage key", () => {
		expect(STORAGE_KEY).toBe("hpac.theme")
	})
})
