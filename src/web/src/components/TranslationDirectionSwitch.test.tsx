import { act, fireEvent, render, renderHook, screen, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import {
	DEFAULT_TRANSLATION_DIRECTION,
	TranslationDirectionSwitch,
	translationLocales,
	useTranslationDirectionSwitch,
} from "./TranslationDirectionSwitch"
import type { ReactNode } from "react"
import { LocaleContext, type LocaleContextValue } from "../i18n/LocaleProvider"

afterEach(cleanup)

const value: LocaleContextValue = {
	locale: "en-CA",
	setLocale: () => {},
	t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
}

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}

describe("useTranslationDirectionSwitch", () => {
	it("announces nothing until it is flipped, then flips toward English and says so", () => {
		const onChange = vi.fn()
		const { result } = renderHook(() => useTranslationDirectionSwitch({ direction: "toFrench", onChange }), { wrapper })
		expect(result.current.toFrench).toBe(true)
		expect(result.current.announcement).toBe("")
		act(() => result.current.onFlip())
		expect(onChange).toHaveBeenCalledWith("toEnglish")
		expect(result.current.announcement).toBe("translationDirection.toEnglish")
	})

	it("flips toward French from English", () => {
		const onChange = vi.fn()
		const { result } = renderHook(() => useTranslationDirectionSwitch({ direction: "toEnglish", onChange }), { wrapper })
		expect(result.current.toFrench).toBe(false)
		act(() => result.current.onFlip())
		expect(onChange).toHaveBeenCalledWith("toFrench")
	})

	it("re-exports the direction helpers", () => {
		expect(DEFAULT_TRANSLATION_DIRECTION).toBe("toFrench")
		expect(translationLocales("toFrench").to).toBe("fr-CA")
	})
})

describe("TranslationDirectionSwitch", () => {
	it("names the current direction on its button and announces a flip in the live region", () => {
		const onChange = vi.fn()
		const { container } = render(<TranslationDirectionSwitch direction="toFrench" onChange={onChange} />, { wrapper })
		fireEvent.click(screen.getByRole("button", { name: "translationDirection.toFrench" }))
		expect(onChange).toHaveBeenCalledWith("toEnglish")
		expect(container.querySelector("[aria-live='polite']")?.textContent).toBe("translationDirection.toEnglish")
	})
})
