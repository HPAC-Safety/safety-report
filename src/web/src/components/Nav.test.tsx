import { cleanup, render, renderHook, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { Nav, useNav } from "./Nav"
import type { NavViewProps } from "./Nav.view"

const view = vi.fn()
vi.mock("./Nav.view", () => ({
	NavView: (props: NavViewProps) => {
		view(props)
		return <p>the nav view</p>
	},
}))

afterEach(() => {
	cleanup()
	view.mockClear()
})

describe("useNav", () => {
	it("lays the links out in a row by default", () => {
		const { result } = renderHook(() => useNav({}))

		expect(result.current.navClassName).toBe("flex flex-nowrap items-center gap-1")
		expect(result.current.linkClassName({ isActive: false })).toContain("text-sm font-medium")
		expect(result.current.linkClassName({ isActive: false })).toContain("text-ink hover:underline")
		expect(result.current.linkClassName({ isActive: true })).toContain("text-brand-700 underline")
	})

	it("stacks the links when asked", () => {
		const { result } = renderHook(() => useNav({ stacked: true }))

		expect(result.current.navClassName).toBe("flex flex-col")
		expect(result.current.linkClassName({ isActive: false })).toContain("text-base font-medium")
		expect(result.current.linkClassName({ isActive: false })).toContain("text-ink hover:underline")
		expect(result.current.linkClassName({ isActive: true })).toContain("text-brand-700 underline")
	})
})

describe("Nav", () => {
	it("hands its view the class names and the navigate callback", () => {
		const onNavigate = vi.fn()

		render(<Nav stacked onNavigate={onNavigate} />)

		expect(screen.getByText("the nav view")).toBeTruthy()
		expect(view.mock.calls[0][0].navClassName).toBe("flex flex-col")
		expect(view.mock.calls[0][0].onNavigate).toBe(onNavigate)
	})
})
