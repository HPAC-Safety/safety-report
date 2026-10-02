import { cleanup, renderHook } from "@testing-library/react"
import { useContext, type ReactNode } from "react"
import { afterEach, describe, expect, it } from "vitest"
import { ThemeContext, type ThemeContextValue } from "./themeContext"

afterEach(cleanup)

describe("ThemeContext", () => {
	it("defaults to null outside a provider", () => {
		const { result } = renderHook(() => useContext(ThemeContext))
		expect(result.current).toBeNull()
	})

	it("supplies the provided value", () => {
		const value: ThemeContextValue = { theme: "dark", setTheme: () => {} }
		const wrapper = ({ children }: { children: ReactNode }) => (
			<ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
		)
		const { result } = renderHook(() => useContext(ThemeContext), { wrapper })
		expect(result.current).toBe(value)
	})
})
