import { cleanup, renderHook } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { ThemeContext, type ThemeContextValue } from "./ThemeProvider"
import { useTheme } from "./useTheme"

afterEach(cleanup)

describe("useTheme", () => {
	it("returns the context value inside a provider", () => {
		const value: ThemeContextValue = { theme: "light", setTheme: () => {} }
		const wrapper = ({ children }: { children: ReactNode }) => (
			<ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
		)
		const { result } = renderHook(() => useTheme(), { wrapper })
		expect(result.current).toBe(value)
	})

	it("throws outside a provider", () => {
		const spy = vi.spyOn(console, "error").mockImplementation(() => {})
		expect(() => renderHook(() => useTheme())).toThrow("useTheme must be used within a ThemeProvider")
		spy.mockRestore()
	})
})
