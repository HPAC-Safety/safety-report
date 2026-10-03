import { act, cleanup, render, renderHook } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue } from "../auth/AuthContext"
import type { MemberRole } from "../auth/session"
import { ThemeContext } from "../theme/ThemeProvider"
import type { Theme } from "../theme/resolveInitialTheme"
import { Header, useHeader } from "./Header"
import type { HeaderViewProps } from "./Header.view"

const counts = { reportsNeedingAction: 1, typeAheadValuesAwaitingReview: 0 }
const usePendingCounts = vi.fn<(enabled: boolean) => unknown>()
vi.mock("./usePendingCounts", () => ({ usePendingCounts: (enabled: boolean) => usePendingCounts(enabled) }))
vi.mock("../../assets/hpac-light.svg", () => ({ default: "light.svg" }))
vi.mock("../../assets/hpac-dark.svg", () => ({ default: "dark.svg" }))

const view = vi.fn<(props: HeaderViewProps) => void>()
vi.mock("./Header.view", () => ({
	HeaderView: (props: HeaderViewProps) => {
		view(props)
		return <p>the header view</p>
	},
}))

afterEach(() => {
	cleanup()
	view.mockClear()
	usePendingCounts.mockReset()
	vi.unstubAllGlobals()
})

function wrapperFor(signedIn: boolean, role: MemberRole | null, theme: Theme | null = null, signOut = vi.fn()) {
	const auth: AuthContextValue = {
		status: signedIn ? "signedIn" : "signedOut",
		isSignedIn: signedIn,
		role,
		signInWithPassword: vi.fn(),
		signOut,
	}
	return ({ children }: { children: ReactNode }) => (
		<AuthContext.Provider value={auth}>
			<ThemeContext.Provider value={{ theme, setTheme: vi.fn() }}>{children}</ThemeContext.Provider>
		</AuthContext.Provider>
	)
}

function systemPrefersDark(dark: boolean) {
	vi.stubGlobal("matchMedia", () => ({ matches: dark }))
}

describe("useHeader", () => {
	it("picks the dark logo for an explicit dark theme", () => {
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(false, null, "dark") })

		expect(result.current.logo).toBe("dark.svg")
	})

	it("picks the light logo for an explicit light theme", () => {
		systemPrefersDark(true)
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(false, null, "light") })

		expect(result.current.logo).toBe("light.svg")
	})

	it("follows a dark system preference when no theme is chosen", () => {
		systemPrefersDark(true)
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(false, null) })

		expect(result.current.logo).toBe("dark.svg")
	})

	it("follows a light system preference when no theme is chosen", () => {
		systemPrefersDark(false)
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(false, null) })

		expect(result.current.logo).toBe("light.svg")
	})

	it("reads the pending counts for a signed-in reviewer", () => {
		usePendingCounts.mockReturnValue(counts)

		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(true, "safety_officer", "light") })

		expect(usePendingCounts).toHaveBeenCalledWith(true)
		expect(result.current.isReviewer).toBe(true)
		expect(result.current.isSignedIn).toBe(true)
		expect(result.current.pendingCounts).toBe(counts)
	})

	it("is no reviewer when signed in as a plain member", () => {
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(true, "user", "light") })

		expect(usePendingCounts).toHaveBeenCalledWith(false)
		expect(result.current.isReviewer).toBe(false)
	})

	it("is no reviewer when signed out", () => {
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(false, null, "light") })

		expect(result.current.isReviewer).toBe(false)
	})

	it("toggles and closes the menu", () => {
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(false, null, "light") })

		act(() => result.current.onToggleMenu())
		expect(result.current.menuOpen).toBe(true)

		act(() => result.current.onCloseMenu())
		expect(result.current.menuOpen).toBe(false)

		act(() => result.current.onToggleMenu())
		act(() => result.current.onToggleMenu())
		expect(result.current.menuOpen).toBe(false)
	})

	it("signs out, and closes the menu when asked to", () => {
		const signOut = vi.fn()
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(true, "user", "light", signOut) })
		act(() => result.current.onToggleMenu())

		act(() => result.current.onSignOutAndClose())

		expect(signOut).toHaveBeenCalledTimes(1)
		expect(result.current.menuOpen).toBe(false)

		result.current.onSignOut()
		expect(signOut).toHaveBeenCalledTimes(2)
	})

	it("closes the open menu on Escape and returns focus to its button", () => {
		const { result } = renderHook(() => useHeader(), { wrapper: wrapperFor(false, null, "light") })
		const button = document.createElement("button")
		document.body.append(button)
		;(result.current.toggleButtonRef as { current: HTMLButtonElement | null }).current = button
		act(() => result.current.onToggleMenu())

		act(() => {
			document.dispatchEvent(new KeyboardEvent("keydown", { key: "a" }))
		})
		expect(result.current.menuOpen).toBe(true)

		act(() => {
			document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }))
		})

		expect(result.current.menuOpen).toBe(false)
		expect(document.activeElement).toBe(button)
		button.remove()
	})

	it("listens for Escape only while the menu is open", () => {
		const add = vi.spyOn(document, "addEventListener")
		renderHook(() => useHeader(), { wrapper: wrapperFor(false, null, "light") })

		expect(add).not.toHaveBeenCalledWith("keydown", expect.anything())
		add.mockRestore()
	})
})

describe("Header", () => {
	it("renders its view", () => {
		const Wrapper = wrapperFor(false, null, "light")

		render(
			<Wrapper>
				<Header />
			</Wrapper>,
		)

		expect(view.mock.calls[0][0].logo).toBe("light.svg")
	})
})
