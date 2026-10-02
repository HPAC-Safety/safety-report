import { cleanup, renderHook } from "@testing-library/react"
import { useContext, type ReactNode } from "react"
import { afterEach, describe, expect, it } from "vitest"
import { LocaleContext, type LocaleContextValue } from "./localeContext"

afterEach(cleanup)

describe("LocaleContext", () => {
	it("defaults to null outside a provider", () => {
		const { result } = renderHook(() => useContext(LocaleContext))
		expect(result.current).toBeNull()
	})

	it("supplies the provided value", () => {
		const value: LocaleContextValue = { locale: "fr-CA", setLocale: () => {}, t: (key) => key }
		const wrapper = ({ children }: { children: ReactNode }) => (
			<LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
		)
		const { result } = renderHook(() => useContext(LocaleContext), { wrapper })
		expect(result.current).toBe(value)
	})
})
