import type { RefObject } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import { UnsavedChangesDialog, useUnsavedChangesDialog } from "./UnsavedChangesDialog"

let startOpen = false

vi.mock("./UnsavedChangesDialog.view", () => ({
	UnsavedChangesDialogView: (props: { dialogRef: RefObject<HTMLDialogElement>; keepButtonRef: RefObject<HTMLButtonElement>; onKeep: () => void }) => (
		<dialog ref={props.dialogRef} open={startOpen}>
			<button ref={props.keepButtonRef} type="button" onClick={props.onKeep}>
				focus target
			</button>
		</dialog>
	),
}))

const showModal = vi.fn(function (this: HTMLDialogElement) {
	this.setAttribute("open", "")
})

beforeEach(() => {
	startOpen = false
	showModal.mockClear()
	HTMLDialogElement.prototype.showModal = showModal
	HTMLDialogElement.prototype.close = vi.fn()
})

afterEach(() => {
	cleanup()
	vi.restoreAllMocks()
})

describe("UnsavedChangesDialog", () => {
	it("opens the dialog as a modal and focuses its safe choice when it mounts", () => {
		render(<UnsavedChangesDialog onConfirm={() => {}} onKeep={() => {}} t={(key) => key} />)

		expect(showModal).toHaveBeenCalledTimes(1)
		expect(document.activeElement).toBe(screen.getByRole("button", { hidden: true }))
	})

	it("does not open the dialog again when it is already open", () => {
		startOpen = true

		render(<UnsavedChangesDialog onConfirm={() => {}} onKeep={() => {}} t={(key) => key} />)

		expect(showModal).not.toHaveBeenCalled()
		expect(document.activeElement).toBe(screen.getByRole("button", { hidden: true }))
	})

	it("hands its own props on to the view", () => {
		const onKeep = vi.fn()

		render(<UnsavedChangesDialog onConfirm={() => {}} onKeep={onKeep} t={(key) => key} />)
		fireEvent.click(screen.getByRole("button"))

		expect(onKeep).toHaveBeenCalledTimes(1)
	})
})

describe("useUnsavedChangesDialog", () => {
	it("returns the refs the view attaches, empty until it is rendered", () => {
		const { result } = renderHook(() => useUnsavedChangesDialog())

		expect(result.current.dialogRef.current).toBeNull()
		expect(result.current.keepButtonRef.current).toBeNull()
	})
})
