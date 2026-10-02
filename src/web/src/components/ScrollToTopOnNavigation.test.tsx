import { act, cleanup, render } from "@testing-library/react"
import { createMemoryRouter, RouterProvider, type RouteObject } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { ScrollToTopOnNavigation } from "./ScrollToTopOnNavigation"

const scrollTo = vi.fn()

beforeEach(() => {
	vi.stubGlobal("scrollTo", scrollTo)
})

afterEach(() => {
	cleanup()
	scrollTo.mockClear()
	vi.unstubAllGlobals()
})

function mount(initial: string) {
	const routes: RouteObject[] = [{ path: "*", element: <ScrollToTopOnNavigation /> }]
	const router = createMemoryRouter(routes, { initialEntries: [initial] })
	const view = render(<RouterProvider router={router} />)
	return { router, view }
}

describe("ScrollToTopOnNavigation", () => {
	it("draws nothing", () => {
		const { view } = mount("/a")

		expect(view.container.innerHTML).toBe("")
	})

	it("leaves the first load to the browser", () => {
		mount("/a")

		expect(scrollTo).not.toHaveBeenCalled()
	})

	it("scrolls to the top when a link goes to a new path", async () => {
		const { router } = mount("/a")

		await act(() => router.navigate("/b"))

		expect(scrollTo).toHaveBeenCalledWith({ top: 0, left: 0, behavior: "instant" })
	})

	it("scrolls to the top when a replace goes to a new path", async () => {
		const { router } = mount("/a")

		await act(() => router.navigate("/b", { replace: true }))

		expect(scrollTo).toHaveBeenCalledTimes(1)
	})

	it("does not scroll when only the query string changes", async () => {
		const { router } = mount("/a")

		await act(() => router.navigate("/a?q=1"))

		expect(scrollTo).not.toHaveBeenCalled()
	})

	it("leaves an in-page anchor to the browser", async () => {
		const { router } = mount("/a")

		await act(() => router.navigate("/b#section"))

		expect(scrollTo).not.toHaveBeenCalled()
	})

	it("leaves Back to the browser", async () => {
		const { router } = mount("/a")
		await act(() => router.navigate("/b"))
		scrollTo.mockClear()

		await act(() => router.navigate(-1))

		expect(scrollTo).not.toHaveBeenCalled()
	})
})
