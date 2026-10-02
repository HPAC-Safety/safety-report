import type { RefObject } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import { ResumeDraftDialog, useResumeDraftDialog } from "./ResumeDraftDialog"

let startOpen = false

vi.mock("./ResumeDraftDialog.view", () => ({
	ResumeDraftDialogView: (props: { dialogRef: RefObject<HTMLDialogElement>; continueButtonRef: RefObject<HTMLButtonElement>; onContinue: () => void }) => (
		<dialog ref={props.dialogRef} open={startOpen}>
			<button ref={props.continueButtonRef} type="button" onClick={props.onContinue}>
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
	HTMLDialogElement.prototype.showModal = showModal as unknown as () => void
	HTMLDialogElement.prototype.close = vi.fn()
})

afterEach(() => {
	cleanup()
	vi.restoreAllMocks()
})

describe("ResumeDraftDialog", () => {
	it("opens the dialog as a modal and focuses its safe choice when it mounts", () => {
		render(<ResumeDraftDialog rows={[]} onContinue={() => {}} onStartOver={() => {}} t={(key) => key} />)

		expect(showModal).toHaveBeenCalledTimes(1)
		expect(document.activeElement).toBe(screen.getByRole("button", { hidden: true }))
	})

	it("does not open the dialog again when it is already open", () => {
		startOpen = true

		render(<ResumeDraftDialog rows={[]} onContinue={() => {}} onStartOver={() => {}} t={(key) => key} />)

		expect(showModal).not.toHaveBeenCalled()
		expect(document.activeElement).toBe(screen.getByRole("button", { hidden: true }))
	})

	it("hands its own props on to the view", () => {
		const onContinue = vi.fn()

		render(<ResumeDraftDialog rows={[]} onContinue={onContinue} onStartOver={() => {}} t={(key) => key} />)
		fireEvent.click(screen.getByRole("button"))

		expect(onContinue).toHaveBeenCalledTimes(1)
	})
})

describe("useResumeDraftDialog", () => {
	it("returns the refs the view attaches, empty until it is rendered", () => {
		const { result } = renderHook(() => useResumeDraftDialog())

		expect(result.current.dialogRef.current).toBeNull()
		expect(result.current.continueButtonRef.current).toBeNull()
	})
})
