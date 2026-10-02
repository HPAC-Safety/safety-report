import { cleanup, renderHook } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { LocaleContext, type LocaleContextValue } from "./LocaleProvider"
import { useLocale } from "./useLocale"

afterEach(cleanup)

describe("useLocale", () => {
	it("returns the context value inside a provider", () => {
		const value: LocaleContextValue = { locale: "en-CA", setLocale: () => {}, t: (key) => key }
		const wrapper = ({ children }: { children: ReactNode }) => (
			<LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
		)
		const { result } = renderHook(() => useLocale(), { wrapper })
		expect(result.current).toBe(value)
	})

	it("throws outside a provider", () => {
		const spy = vi.spyOn(console, "error").mockImplementation(() => {})
		expect(() => renderHook(() => useLocale())).toThrow("useLocale must be used within a LocaleProvider")
		spy.mockRestore()
	})
})
