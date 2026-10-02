import { act, cleanup, render, screen } from "@testing-library/react"
import { LocaleContext } from "../i18n/LocaleProvider"
import { createMemoryRouter, RouterProvider } from "react-router-dom"
import { afterEach, describe, expect, it, vi } from "vitest"
import { UnsavedChangesGuardRoot } from "./UnsavedChangesGuardRoot"
import { useUnsavedChangesGuard, type UnsavedChangesOptions } from "./useUnsavedChangesGuard"

vi.mock("../components/UnsavedChangesDialog", () => ({
	UnsavedChangesDialog: () => <p>blocked</p>,
}))

afterEach(cleanup)

const locale = { locale: "en-CA" as const, setLocale: vi.fn(), t: (key: string) => key }

function Form({ dirty, options }: { dirty: boolean; options?: UnsavedChangesOptions }) {
	useUnsavedChangesGuard(dirty, options)
	return <p>form</p>
}

function mount(element: React.ReactNode, initial = "/form/one") {
	const router = createMemoryRouter([{ path: "*", element }], { initialEntries: [initial] })
	render(
		<LocaleContext.Provider value={locale}>
			<RouterProvider router={router} />
		</LocaleContext.Provider>,
	)
	return router
}

function dispatchBeforeUnload() {
	const event = new Event("beforeunload", { cancelable: true }) as BeforeUnloadEvent
	window.dispatchEvent(event)
	return event
}

describe("useUnsavedChangesGuard", () => {
	it("prompts the browser on unload while dirty", () => {
		mount(<Form dirty />)

		const event = dispatchBeforeUnload()

		expect(event.defaultPrevented).toBe(true)
	})

	it("stays quiet on unload while clean", () => {
		mount(<Form dirty={false} />)

		expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
	})

	it("stays quiet on unload when the form keeps its changes anyway", () => {
		mount(<Form dirty options={{ unloadPrompt: false }} />)

		expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
	})

	it("stops prompting once the form is gone", () => {
		const router = mount(<Form dirty />)
		cleanup()

		expect(router).toBeDefined()
		expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
	})

	it("registers nothing, and so blocks nothing, outside a guard root", async () => {
		const router = mount(<Form dirty />)

		await act(() => router.navigate("/elsewhere"))

		expect(router.state.location.pathname).toBe("/elsewhere")
	})

	it("lets navigation within its own path proceed", async () => {
		const router = mount(
			<UnsavedChangesGuardRoot>
				<Form dirty options={{ withinPath: "/form" }} />
			</UnsavedChangesGuardRoot>,
		)

		await act(() => router.navigate("/form/two"))

		expect(router.state.location.pathname).toBe("/form/two")
		expect(screen.queryByText("blocked")).toBeNull()
	})

	it("blocks a move from inside its path to outside it", async () => {
		const router = mount(
			<UnsavedChangesGuardRoot>
				<Form dirty options={{ withinPath: "/form" }} />
			</UnsavedChangesGuardRoot>,
		)

		await act(() => router.navigate("/formal"))

		expect(screen.getByText("blocked")).toBeTruthy()
	})

	it("blocks a move from outside its path to inside it", async () => {
		const router = mount(
			<UnsavedChangesGuardRoot>
				<Form dirty options={{ withinPath: "/form" }} />
			</UnsavedChangesGuardRoot>,
			"/other",
		)

		await act(() => router.navigate("/form/two"))

		expect(screen.getByText("blocked")).toBeTruthy()
	})

	it("treats the path itself as within it", async () => {
		const router = mount(
			<UnsavedChangesGuardRoot>
				<Form dirty options={{ withinPath: "/form" }} />
			</UnsavedChangesGuardRoot>,
			"/form",
		)

		await act(() => router.navigate("/form/two"))

		expect(screen.queryByText("blocked")).toBeNull()
	})

	it("unregisters when the form becomes clean", async () => {
		const router = createMemoryRouter(
			[{ path: "*", element: <UnsavedChangesGuardRoot><Toggler /></UnsavedChangesGuardRoot> }],
			{ initialEntries: ["/a"] },
		)
		render(
		<LocaleContext.Provider value={locale}>
			<RouterProvider router={router} />
		</LocaleContext.Provider>,
	)
		await act(async () => screen.getByText("clean").click())

		await act(() => router.navigate("/b"))

		expect(router.state.location.pathname).toBe("/b")
	})
})

import { useState } from "react"
function Toggler() {
	const [dirty, setDirty] = useState(true)
	useUnsavedChangesGuard(dirty)
	return (
		<button type="button" onClick={() => setDirty(false)}>
			clean
		</button>
	)
}
