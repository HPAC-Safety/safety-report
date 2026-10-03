import { act, cleanup, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue } from "../auth/AuthContext"
import type { MemberRole } from "../auth/session"
import { AdminMenu, useAdminMenu } from "./AdminMenu"
import type { AdminMenuViewProps } from "./AdminMenu.view"

const view = vi.fn<(props: AdminMenuViewProps) => void>()
vi.mock("./AdminMenu.view", () => ({
	AdminMenuView: (props: AdminMenuViewProps) => {
		view(props)
		return <p>the admin menu view</p>
	},
}))

afterEach(() => {
	cleanup()
	view.mockClear()
})

function authWrapper(role: MemberRole | null) {
	const value: AuthContextValue = {
		status: "signedIn",
		isSignedIn: true,
		role,
		signInWithPassword: vi.fn(),
		signOut: vi.fn(),
	}
	return ({ children }: { children: ReactNode }) => <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

describe("useAdminMenu", () => {
	it("starts closed with no counts", () => {
		const { result } = renderHook(() => useAdminMenu({}), { wrapper: authWrapper("safety_officer") })

		expect(result.current.open).toBe(false)
		expect(result.current.reports).toBe(0)
		expect(result.current.typeAheadValues).toBe(0)
		expect(result.current.isAdministrator).toBe(false)
	})

	it("reads the counts it is given", () => {
		const counts = { reportsNeedingAction: 3, typeAheadValuesAwaitingReview: 2, answersAwaitingTranslation: 0 }

		const { result } = renderHook(() => useAdminMenu({ counts }), { wrapper: authWrapper("administrator") })

		expect(result.current.reports).toBe(3)
		expect(result.current.typeAheadValues).toBe(2)
		expect(result.current.isAdministrator).toBe(true)
	})

	it("treats missing roles and null counts as nothing to show", () => {
		const { result } = renderHook(() => useAdminMenu({ counts: null }), { wrapper: authWrapper(null) })

		expect(result.current.reports).toBe(0)
		expect(result.current.isAdministrator).toBe(false)
	})

	it("toggles open and closed", () => {
		const { result } = renderHook(() => useAdminMenu({}), { wrapper: authWrapper("administrator") })

		act(() => result.current.onToggle())
		expect(result.current.open).toBe(true)

		act(() => result.current.onToggle())
		expect(result.current.open).toBe(false)
	})

	it("closes on Escape and returns focus to the button", () => {
		const { result } = renderHook(() => useAdminMenu({}), { wrapper: authWrapper("administrator") })
		const button = document.createElement("button")
		document.body.append(button)
		;(result.current.buttonRef as { current: HTMLButtonElement | null }).current = button
		act(() => result.current.onToggle())

		act(() => {
			document.dispatchEvent(new KeyboardEvent("keydown", { key: "a" }))
		})
		expect(result.current.open).toBe(true)

		act(() => {
			document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }))
		})

		expect(result.current.open).toBe(false)
		expect(document.activeElement).toBe(button)
		button.remove()
	})

	it("closes on a pointer press outside and stays open on one inside", () => {
		const { result } = renderHook(() => useAdminMenu({}), { wrapper: authWrapper("administrator") })
		const inside = document.createElement("div")
		const outside = document.createElement("div")
		document.body.append(inside, outside)
		;(result.current.containerRef as { current: HTMLDivElement | null }).current = inside
		act(() => result.current.onToggle())

		act(() => {
			inside.dispatchEvent(new Event("pointerdown", { bubbles: true }))
		})
		expect(result.current.open).toBe(true)

		act(() => {
			outside.dispatchEvent(new Event("pointerdown", { bubbles: true }))
		})
		expect(result.current.open).toBe(false)
		inside.remove()
		outside.remove()
	})

	it("treats a pointer press as outside when the container is not mounted", () => {
		const { result } = renderHook(() => useAdminMenu({}), { wrapper: authWrapper("administrator") })
		act(() => result.current.onToggle())

		act(() => {
			document.body.dispatchEvent(new Event("pointerdown", { bubbles: true }))
		})

		expect(result.current.open).toBe(false)
	})

	it("closes and tells the caller when an item is chosen", () => {
		const onNavigate = vi.fn()
		const { result } = renderHook(() => useAdminMenu({ onNavigate }), { wrapper: authWrapper("administrator") })
		act(() => result.current.onToggle())

		act(() => result.current.onSelectItem())

		expect(result.current.open).toBe(false)
		expect(onNavigate).toHaveBeenCalledTimes(1)
	})

	it("closes when an item is chosen and nobody is listening", () => {
		const { result } = renderHook(() => useAdminMenu({}), { wrapper: authWrapper("administrator") })
		act(() => result.current.onToggle())

		act(() => result.current.onSelectItem())

		expect(result.current.open).toBe(false)
	})
})

describe("AdminMenu", () => {
	it("passes stacked and the view model to its view", () => {
		const Wrapper = authWrapper("administrator")

		render(
			<Wrapper>
				<AdminMenu stacked />
			</Wrapper>,
		)

		expect(screen.getByText("the admin menu view")).toBeTruthy()
		expect(view.mock.calls[0][0].stacked).toBe(true)
		expect(view.mock.calls[0][0].isAdministrator).toBe(true)
	})
})
