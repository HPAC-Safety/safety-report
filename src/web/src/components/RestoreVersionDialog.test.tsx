import type { RefObject } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import { RestoreVersionDialog, useRestoreVersionDialog } from "./RestoreVersionDialog"

let startOpen = false

vi.mock("./RestoreVersionDialog.view", () => ({
	RestoreVersionDialogView: (props: { dialogRef: RefObject<HTMLDialogElement>; keepButtonRef: RefObject<HTMLButtonElement>; onKeep: () => void }) => (
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

describe("RestoreVersionDialog", () => {
	it("opens the dialog as a modal and focuses its safe choice when it mounts", () => {
		render(<RestoreVersionDialog sequence={3} isLive={false} onConfirm={() => {}} onKeep={() => {}} />)

		expect(showModal).toHaveBeenCalledTimes(1)
		expect(document.activeElement).toBe(screen.getByRole("button", { hidden: true }))
	})

	it("does not open the dialog again when it is already open", () => {
		startOpen = true

		render(<RestoreVersionDialog sequence={3} isLive={false} onConfirm={() => {}} onKeep={() => {}} />)

		expect(showModal).not.toHaveBeenCalled()
		expect(document.activeElement).toBe(screen.getByRole("button", { hidden: true }))
	})

	it("hands its own props on to the view", () => {
		const onKeep = vi.fn()

		render(<RestoreVersionDialog sequence={3} isLive onConfirm={() => {}} onKeep={onKeep} />)
		fireEvent.click(screen.getByRole("button"))

		expect(onKeep).toHaveBeenCalledTimes(1)
	})
})

describe("useRestoreVersionDialog", () => {
	it("returns the refs the view attaches, empty until it is rendered", () => {
		const { result } = renderHook(() => useRestoreVersionDialog())

		expect(result.current.dialogRef.current).toBeNull()
		expect(result.current.keepButtonRef.current).toBeNull()
	})
})
