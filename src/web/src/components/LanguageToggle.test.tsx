import { cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { LocaleContext, type LocaleContextValue } from "../i18n/LocaleProvider"
import type { Locale } from "../i18n/locales"
import { LanguageToggle, useLanguageToggle } from "./LanguageToggle"

afterEach(cleanup)

function localeWrapper(locale: Locale, setLocale = vi.fn()) {
	const value: LocaleContextValue = {
		locale,
		setLocale,
		t: (key, params) => (params ? `${key}:${JSON.stringify(params)}` : key),
	}
	return ({ children }: { children: ReactNode }) => <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}

describe("useLanguageToggle", () => {
	it("offers French from English", () => {
		const setLocale = vi.fn()
		const { result } = renderHook(() => useLanguageToggle(), { wrapper: localeWrapper("en-CA", setLocale) })

		expect(result.current.code).toBe("en")
		expect(result.current.label).toBe('language.switchTo:{"language":"language.optionFrench"}')

		result.current.onToggle()
		expect(setLocale).toHaveBeenCalledWith("fr-CA")
	})

	it("offers English from French", () => {
		const setLocale = vi.fn()
		const { result } = renderHook(() => useLanguageToggle(), { wrapper: localeWrapper("fr-CA", setLocale) })

		expect(result.current.code).toBe("fr")
		expect(result.current.label).toBe('language.switchTo:{"language":"language.optionEnglish"}')

		result.current.onToggle()
		expect(setLocale).toHaveBeenCalledWith("en-CA")
	})
})

describe("LanguageToggle", () => {
	it("switches the language when pressed", () => {
		const setLocale = vi.fn()
		const Wrapper = localeWrapper("en-CA", setLocale)

		render(
			<Wrapper>
				<LanguageToggle />
			</Wrapper>,
		)
		fireEvent.click(screen.getByRole("button"))

		expect(setLocale).toHaveBeenCalledWith("fr-CA")
	})
})
