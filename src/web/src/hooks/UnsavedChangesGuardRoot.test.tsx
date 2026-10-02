import { act, cleanup, fireEvent, render, screen } from "@testing-library/react"
import { LocaleContext } from "../i18n/LocaleProvider"
import { createMemoryRouter, RouterProvider } from "react-router-dom"
import { afterEach, describe, expect, it, vi } from "vitest"
import { UnsavedChangesGuardRoot } from "./UnsavedChangesGuardRoot"
import { useUnsavedChangesGuard, type UnsavedChangesCopy, type UnsavedChangesOptions } from "./useUnsavedChangesGuard"

vi.mock("../components/UnsavedChangesDialog", () => ({
	UnsavedChangesDialog: ({ onConfirm, onKeep, copy }: { onConfirm: () => void; onKeep: () => void; copy?: UnsavedChangesCopy }) => (
		<div>
			<p>{copy?.title ?? "shared wording"}</p>
			<button type="button" onClick={onConfirm}>
				leave
			</button>
			<button type="button" onClick={onKeep}>
				stay
			</button>
		</div>
	),
}))

afterEach(cleanup)

const locale = { locale: "en-CA" as const, setLocale: vi.fn(), t: (key: string) => key }

function Form({ dirty, options }: { dirty: boolean; options?: UnsavedChangesOptions }) {
	useUnsavedChangesGuard(dirty, options)
	return <p>form</p>
}

function mount(dirty: boolean, options?: UnsavedChangesOptions) {
	const router = createMemoryRouter(
		[
			{
				path: "/",
				element: (
					<UnsavedChangesGuardRoot>
						<Form dirty={dirty} options={options} />
					</UnsavedChangesGuardRoot>
				),
				children: [{ path: "*", element: null }],
			},
		],
		{ initialEntries: ["/form/one"] },
	)
	render(
		<LocaleContext.Provider value={locale}>
			<RouterProvider router={router} />
		</LocaleContext.Provider>,
	)
	return router
}

describe("UnsavedChangesGuardRoot", () => {
	it("lets a clean form navigate away", async () => {
		const router = mount(false)

		await act(() => router.navigate("/elsewhere"))

		expect(router.state.location.pathname).toBe("/elsewhere")
		expect(screen.queryByText("leave")).toBeNull()
	})

	it("asks before a dirty form navigates away, and leaves on confirm", async () => {
		const router = mount(true)

		await act(() => router.navigate("/elsewhere"))
		expect(screen.getByText("shared wording")).toBeTruthy()
		expect(router.state.location.pathname).toBe("/form/one")

		fireEvent.click(screen.getByText("leave"))

		await act(async () => {})
		expect(router.state.location.pathname).toBe("/elsewhere")
	})

	it("stays on the form when the person keeps it", async () => {
		const router = mount(true)
		await act(() => router.navigate("/elsewhere"))

		fireEvent.click(screen.getByText("stay"))

		await act(async () => {})
		expect(screen.queryByText("leave")).toBeNull()
		expect(router.state.location.pathname).toBe("/form/one")
	})

	it("shows the dirty form's own wording", async () => {
		const copy: UnsavedChangesCopy = { title: "Saved for 15 days", body: "b", leave: "l", stay: "s" }
		const router = mount(true, { copy: () => copy })

		await act(() => router.navigate("/elsewhere"))

		expect(screen.getByText("Saved for 15 days")).toBeTruthy()
	})

	it("does nothing when asked to answer with no navigation waiting", async () => {
		const router = mount(true)

		await act(() => router.navigate("/form/one"))

		expect(screen.queryByText("leave")).toBeNull()
	})
})
