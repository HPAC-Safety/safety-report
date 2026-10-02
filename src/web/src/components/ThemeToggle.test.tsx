import { act, cleanup, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { ThemeContext, type ThemeContextValue } from "../theme/ThemeProvider"
import type { Theme } from "../theme/resolveInitialTheme"
import { ThemeToggle, useThemeToggle } from "./ThemeToggle"

vi.mock("./ThemeToggle.view", () => ({
	ThemeToggleView: ({ effectiveDark, onToggle }: { effectiveDark: boolean; onToggle: () => void }) => (
		<button type="button" onClick={onToggle}>
			{effectiveDark ? "dark" : "light"}
		</button>
	),
}))

afterEach(() => {
	cleanup()
	vi.unstubAllGlobals()
})

function systemPrefersDark(dark: boolean) {
	vi.stubGlobal("matchMedia", (query: string) => ({ matches: dark && query === "(prefers-color-scheme: dark)" }))
}

function themeWrapper(theme: Theme | null, setTheme = vi.fn()) {
	const value: ThemeContextValue = { theme, setTheme }
	return ({ children }: { children: ReactNode }) => <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

describe("useThemeToggle", () => {
	it("is dark when the page is explicitly dark", () => {
		const { result } = renderHook(() => useThemeToggle(), { wrapper: themeWrapper("dark") })

		expect(result.current.effectiveDark).toBe(true)
	})

	it("is light when the page is explicitly light, whatever the system prefers", () => {
		systemPrefersDark(true)
		const { result } = renderHook(() => useThemeToggle(), { wrapper: themeWrapper("light") })

		expect(result.current.effectiveDark).toBe(false)
	})

	it("follows a dark system preference when nothing is chosen", () => {
		systemPrefersDark(true)
		const { result } = renderHook(() => useThemeToggle(), { wrapper: themeWrapper(null) })

		expect(result.current.effectiveDark).toBe(true)
	})

	it("follows a light system preference when nothing is chosen", () => {
		systemPrefersDark(false)
		const { result } = renderHook(() => useThemeToggle(), { wrapper: themeWrapper(null) })

		expect(result.current.effectiveDark).toBe(false)
	})

	it("switches to light from dark and to dark from light", () => {
		const setTheme = vi.fn()
		const dark = renderHook(() => useThemeToggle(), { wrapper: themeWrapper("dark", setTheme) })
		const light = renderHook(() => useThemeToggle(), { wrapper: themeWrapper("light", setTheme) })

		act(() => dark.result.current.onToggle())
		act(() => light.result.current.onToggle())

		expect(setTheme.mock.calls).toEqual([["light"], ["dark"]])
	})
})

describe("ThemeToggle", () => {
	it("renders its view with the current state", () => {
		const Wrapper = themeWrapper("dark")

		render(
			<Wrapper>
				<ThemeToggle />
			</Wrapper>,
		)

		expect(screen.getByRole("button").textContent).toBe("dark")
	})
})
