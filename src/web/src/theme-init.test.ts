import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

beforeEach(() => {
	vi.resetModules()
	localStorage.clear()
	delete document.documentElement.dataset.theme
})

afterEach(() => {
	vi.restoreAllMocks()
	delete document.documentElement.dataset.theme
})

describe("theme-init", () => {
	it("applies a stored dark theme", async () => {
		localStorage.setItem("hpac.theme", "dark")
		await import("./theme-init")
		expect(document.documentElement.dataset.theme).toBe("dark")
	})

	it("applies a stored light theme", async () => {
		localStorage.setItem("hpac.theme", "light")
		await import("./theme-init")
		expect(document.documentElement.dataset.theme).toBe("light")
	})

	it("leaves data-theme unset for invalid storage", async () => {
		localStorage.setItem("hpac.theme", "blue")
		await import("./theme-init")
		expect(document.documentElement.dataset.theme).toBeUndefined()
	})

	it("leaves data-theme unset when storage is unavailable", async () => {
		vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
			throw new Error("denied")
		})
		await import("./theme-init")
		expect(document.documentElement.dataset.theme).toBeUndefined()
	})
})
