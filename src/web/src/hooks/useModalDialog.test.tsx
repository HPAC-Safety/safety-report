import { cleanup, render, screen } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { useModalDialog } from "./useModalDialog"
import { present } from "../lib/present"

const showModal = vi.fn(function (this: HTMLDialogElement) {
	this.setAttribute("open", "")
})

beforeEach(() => {
	showModal.mockClear()
	HTMLDialogElement.prototype.showModal = showModal
})
afterEach(cleanup)

function Probe({ focus = true, dialog = true }: { focus?: boolean; dialog?: boolean }) {
	const { dialogRef, focusRef } = useModalDialog()
	return dialog ? (
		<dialog ref={dialogRef} aria-label="probe">
			<button type="button">first</button>
			<button type="button" ref={focus ? focusRef : undefined}>
				keep
			</button>
		</dialog>
	) : (
		<p>no dialog</p>
	)
}

describe("useModalDialog", () => {
	it("opens the dialog as a modal on mount and focuses the control it was given", () => {
		render(<Probe />)
		expect(showModal).toHaveBeenCalledTimes(1)
		expect(document.activeElement).toBe(screen.getByRole("button", { name: "keep", hidden: true }))
	})

	it("leaves focus to the browser when no control was given", () => {
		render(<Probe focus={false} />)
		expect(showModal).toHaveBeenCalledTimes(1)
		expect(document.activeElement).toBe(document.body)
	})

	it("does not open a dialog that is already open", () => {
		const open = present(Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, "open"))
		Object.defineProperty(HTMLDialogElement.prototype, "open", { get: () => true, configurable: true })
		try {
			render(<Probe />)
			expect(showModal).not.toHaveBeenCalled()
		} finally {
			Object.defineProperty(HTMLDialogElement.prototype, "open", open)
		}
	})

	it("does nothing when the dialog is not rendered", () => {
		render(<Probe dialog={false} />)
		expect(showModal).not.toHaveBeenCalled()
	})
})
