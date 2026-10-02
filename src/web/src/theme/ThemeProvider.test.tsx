import { act, cleanup, render, renderHook, screen } from "@testing-library/react"
import { useContext } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { ThemeContext, ThemeProvider, useThemeProvider } from "./ThemeProvider"
import { STORAGE_KEY } from "./resolveInitialTheme"

beforeEach(() => {
	localStorage.clear()
	delete document.documentElement.dataset.theme
})

afterEach(() => {
	cleanup()
	vi.restoreAllMocks()
})

describe("useThemeProvider", () => {
	it("starts from the theme the page already carries", () => {
		document.documentElement.dataset.theme = "dark"

		const { result } = renderHook(() => useThemeProvider())

		expect(result.current.theme).toBe("dark")
	})

	it("starts without a theme when the page carries none", () => {
		const { result } = renderHook(() => useThemeProvider())

		expect(result.current.theme).toBeNull()
	})

	it("starts without a theme when the page carries one it does not know", () => {
		document.documentElement.dataset.theme = "sepia"

		const { result } = renderHook(() => useThemeProvider())

		expect(result.current.theme).toBeNull()
	})

	it("sets and remembers a theme", () => {
		const { result } = renderHook(() => useThemeProvider())

		act(() => result.current.setTheme("light"))

		expect(result.current.theme).toBe("light")
		expect(document.documentElement.dataset.theme).toBe("light")
		expect(localStorage.getItem(STORAGE_KEY)).toBe("light")
	})

	it("clears the theme and forgets it", () => {
		document.documentElement.dataset.theme = "dark"
		localStorage.setItem(STORAGE_KEY, "dark")
		const { result } = renderHook(() => useThemeProvider())

		act(() => result.current.setTheme(null))

		expect(result.current.theme).toBeNull()
		expect(document.documentElement.dataset.theme).toBeUndefined()
		expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
	})

	it("applies a theme even when storage cannot be written", () => {
		vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
			throw new Error("quota")
		})
		const { result } = renderHook(() => useThemeProvider())

		act(() => result.current.setTheme("dark"))

		expect(result.current.theme).toBe("dark")
	})
})

describe("ThemeProvider", () => {
	function Reader() {
		const value = useContext(ThemeContext)
		return <p>{value?.theme ?? "none"}</p>
	}

	it("provides the context to its children", () => {
		document.documentElement.dataset.theme = "light"

		render(
			<ThemeProvider>
				<Reader />
			</ThemeProvider>,
		)

		expect(screen.getByText("light")).toBeTruthy()
	})
})
